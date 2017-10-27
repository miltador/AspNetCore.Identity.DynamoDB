using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;
using AspNetCore.Identity.DynamoDB.Extensions;

namespace AspNetCore.Identity.DynamoDB
{
	public class DynamoRoleUsersStore<TRole, TUser>
		where TRole : DynamoIdentityRole
		where TUser : DynamoIdentityUser
	{
		private IAmazonDynamoDB _client;
		private IDynamoDBContext _context;

		public async Task AddToRoleAsync(TUser user, string normalisedRoleName, CancellationToken cancellationToken)
		{
			if (user == null)
			{
				throw new ArgumentNullException(nameof(user));
			}

			cancellationToken.ThrowIfCancellationRequested();

			if (!await IsInRoleAsync(user, normalisedRoleName, cancellationToken))
			{
				var roleUser = new DynamoIdentityRoleUser(normalisedRoleName, user.Id);

				await _context.SaveAsync(roleUser, cancellationToken);
			}
		}

		public async Task RemoveFromRoleAsync(TUser user, string normalisedRoleName, CancellationToken cancellationToken)
		{
			if (user == null)
			{
				throw new ArgumentNullException(nameof(user));
			}

			cancellationToken.ThrowIfCancellationRequested();

			var roleUsers = await QueryRoleUsers(normalisedRoleName, user.Id, cancellationToken);

			foreach (var roleUser in roleUsers)
			{
				await _context.DeleteAsync(roleUser, cancellationToken);
			}
		}

		public async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken)
		{
			if (user == null)
			{
				throw new ArgumentNullException(nameof(user));
			}

			cancellationToken.ThrowIfCancellationRequested();

			var roleUser = await QueryRoleUsers(null, user.Id, cancellationToken);

			return roleUser.Select(r => r.NormalizedRoleName).Distinct().ToList();
		}

		public async Task<bool> IsInRoleAsync(TUser user, string normalisedRoleName, CancellationToken cancellationToken)
		{
			if (user == null)
			{
				throw new ArgumentNullException(nameof(user));
			}

			cancellationToken.ThrowIfCancellationRequested();

			var roleUser = await QueryRoleUsers(normalisedRoleName, user.Id, cancellationToken);

			return roleUser.Count != 0;
		}

		public async Task<IList<string>> GetUserIdsInRoleAsync(string normalisedRoleName, CancellationToken cancellationToken)
		{
			var roleUsers = await QueryRoleUsers(normalisedRoleName, null, cancellationToken);

			return roleUsers.Select(r => r.UserId).Distinct().ToList();
		}

		internal async Task<IList<DynamoIdentityRoleUser>> QueryRoleUsers(string normalisedRoleName, string userId,
			CancellationToken cancellationToken)
		{
			if (normalisedRoleName == null && userId == null)
			{
				throw new ArgumentNullException(nameof(normalisedRoleName));
			}

			cancellationToken.ThrowIfCancellationRequested();

			var expressions = new List<string>();
			var index = "UserId-NormalizedRoleName-index";
			var values = new Dictionary<string, DynamoDBEntry>();

			if (normalisedRoleName != null)
			{
				index = "NormalizedRoleName-UserId-index";
				expressions.Add("NormalizedRoleName = :normalizedRoleName");
				values.Add(":normalizedRoleName", normalisedRoleName);
			}

			if (userId != null)
			{
				expressions.Add("UserId = :userId");
				values.Add(":userId", userId);
			}

			var expression = string.Join(" AND ", expressions);

			var search = _context.FromQueryAsync<DynamoIdentityRoleUser>(new QueryOperationConfig
			{
				IndexName = index,
				KeyExpression = new Expression
				{
					ExpressionStatement = expression,
					ExpressionAttributeValues = values
				}
			});

			return await search.GetRemainingAsync(cancellationToken);
		}

		public Task EnsureInitializedAsync(IAmazonDynamoDB client, IDynamoDBContext context,
			string roleUsersTableName = Constants.DefaultRoleUsersTableName)
		{
			if (client == null)
			{
				throw new ArgumentNullException(nameof(client));
			}

			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}

			_context = context;
			_client = client;

			if (roleUsersTableName != Constants.DefaultRoleUsersTableName)
			{
				AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(roleUsersTableName, Constants.DefaultRoleUsersTableName));
			}

			return EnsureInitializedImplAsync(client, roleUsersTableName);
		}

		private async Task EnsureInitializedImplAsync(IAmazonDynamoDB client, string roleUsersTableName)
		{
			var defaultProvisionThroughput = new ProvisionedThroughput
			{
				ReadCapacityUnits = 5,
				WriteCapacityUnits = 5
			};
			var globalSecondaryIndexes = new List<GlobalSecondaryIndex>
			{
				new GlobalSecondaryIndex
				{
					IndexName = "NormalizedRoleName-UserId-index",
					KeySchema = new List<KeySchemaElement>
					{
						new KeySchemaElement("NormalizedRoleName", KeyType.HASH),
						new KeySchemaElement("UserId", KeyType.RANGE)
					},
					ProvisionedThroughput = defaultProvisionThroughput,
					Projection = new Projection
					{
						ProjectionType = ProjectionType.ALL
					}
				},
				new GlobalSecondaryIndex
				{
					IndexName = "UserId-NormalizedRoleName-index",
					KeySchema = new List<KeySchemaElement>
					{
						new KeySchemaElement("UserId", KeyType.HASH),
						new KeySchemaElement("NormalizedRoleName", KeyType.RANGE)
					},
					ProvisionedThroughput = defaultProvisionThroughput,
					Projection = new Projection
					{
						ProjectionType = ProjectionType.ALL
					}
				}
			};

			var tableNames = await client.ListAllTablesAsync();

			if (!tableNames.Contains(roleUsersTableName))
			{
				await CreateTableAsync(client, roleUsersTableName, defaultProvisionThroughput, globalSecondaryIndexes);
				return;
			}

			var response = await client.DescribeTableAsync(new DescribeTableRequest {TableName = roleUsersTableName});
			var table = response.Table;

			var indexesToAdd =
				globalSecondaryIndexes.Where(
					g => !table.GlobalSecondaryIndexes.Exists(gd => gd.IndexName.Equals(g.IndexName)));
			var indexUpdates = indexesToAdd.Select(index => new GlobalSecondaryIndexUpdate
			{
				Create = new CreateGlobalSecondaryIndexAction
				{
					IndexName = index.IndexName,
					KeySchema = index.KeySchema,
					ProvisionedThroughput = index.ProvisionedThroughput,
					Projection = index.Projection
				}
			}).ToList();

			if (indexUpdates.Count > 0)
			{
				await UpdateTableAsync(client, roleUsersTableName, indexUpdates);
			}
		}

		private async Task CreateTableAsync(IAmazonDynamoDB client, string roleUsersTableName,
			ProvisionedThroughput provisionedThroughput, List<GlobalSecondaryIndex> globalSecondaryIndexes)
		{
			var response = await client.CreateTableAsync(new CreateTableRequest
			{
				TableName = roleUsersTableName,
				ProvisionedThroughput = provisionedThroughput,
				KeySchema = new List<KeySchemaElement>
				{
					new KeySchemaElement
					{
						AttributeName = "Id",
						KeyType = KeyType.HASH
					}
				},
				AttributeDefinitions = new List<AttributeDefinition>
				{
					new AttributeDefinition
					{
						AttributeName = "Id",
						AttributeType = ScalarAttributeType.S
					},
					new AttributeDefinition
					{
						AttributeName = "NormalizedRoleName",
						AttributeType = ScalarAttributeType.S
					},
					new AttributeDefinition
					{
						AttributeName = "UserId",
						AttributeType = ScalarAttributeType.S
					}
				},
				GlobalSecondaryIndexes = globalSecondaryIndexes
			});

			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception($"Couldn't create table {roleUsersTableName}");
			}

			await DynamoUtils.WaitForActiveTableAsync(client, roleUsersTableName);
		}

		private async Task UpdateTableAsync(IAmazonDynamoDB client, string roleUsersTableName,
			List<GlobalSecondaryIndexUpdate> indexUpdates)
		{
			await client.UpdateTableAsync(new UpdateTableRequest
			{
				TableName = roleUsersTableName,
				GlobalSecondaryIndexUpdates = indexUpdates
			});

			await DynamoUtils.WaitForActiveTableAsync(client, roleUsersTableName);
		}
	}
}