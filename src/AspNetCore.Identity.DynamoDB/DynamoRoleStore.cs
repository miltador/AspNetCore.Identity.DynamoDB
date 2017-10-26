using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;
using AspNetCore.Identity.DynamoDB.Extensions;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.DynamoDB
{
	public class DynamoRoleStore<TRole> : IRoleClaimStore<TRole>
		where TRole : DynamoIdentityRole
	{
		private IDynamoDBContext _context;

		public Task<IList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			return Task.FromResult(role.GetClaims());
		}

		public Task AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			if (claim == null)
			{
				throw new ArgumentNullException(nameof(claim));
			}

			role.AddClaim(claim);

			return Task.FromResult(0);
		}

		public Task RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			if (claim == null)
			{
				throw new ArgumentNullException(nameof(claim));
			}

			role.RemoveClaim(claim);

			return Task.FromResult(0);
		}

		public async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken)
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			cancellationToken.ThrowIfCancellationRequested();

			await _context.SaveAsync(role, cancellationToken);

			return IdentityResult.Success;
		}

		public async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken)
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			cancellationToken.ThrowIfCancellationRequested();

			await _context.SaveAsync(role, cancellationToken);

			return IdentityResult.Success;
		}

		public async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken)
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			cancellationToken.ThrowIfCancellationRequested();

			role.Delete();

			await _context.SaveAsync(role, cancellationToken);

			return IdentityResult.Success;
		}

		public Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken)
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			return Task.FromResult(role.Id);
		}

		public Task<string> GetRoleNameAsync(TRole role, CancellationToken cancellationToken)
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			return Task.FromResult(role.Name);
		}

		public Task SetRoleNameAsync(TRole role, string roleName, CancellationToken cancellationToken)
		{
			throw new NotSupportedException("Changing the role name is not supported.");
		}

		public Task<string> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken)
		{
			if (role == null)
			{
				throw new ArgumentNullException(nameof(role));
			}

			return Task.FromResult(role.NormalizedName);
		}

		public Task SetNormalizedRoleNameAsync(TRole role, string normalizedName, CancellationToken cancellationToken)
		{
			throw new NotSupportedException("Changing the role normalized name is not supported.");
		}

		public async Task<TRole> FindByIdAsync(string roleId, CancellationToken cancellationToken)
		{
			if (roleId == null)
			{
				throw new ArgumentNullException(nameof(roleId));
			}

			cancellationToken.ThrowIfCancellationRequested();

			var role = await _context.LoadAsync<TRole>(roleId, default(DateTimeOffset), cancellationToken);
			return role?.DeletedOn == default(DateTimeOffset) ? role : null;
		}

		public async Task<TRole> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
		{
			if (normalizedRoleName == null)
			{
				throw new ArgumentNullException(nameof(normalizedRoleName));
			}

			cancellationToken.ThrowIfCancellationRequested();

			var search = _context.FromQueryAsync<TRole>(new QueryOperationConfig
			{
				IndexName = "NormalizedName-DeletedOn-index",
				KeyExpression = new Expression
				{
					ExpressionStatement = "NormalizedName = :name AND DeletedOn = :deletedOn",
					ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
					{
						{":name", normalizedRoleName},
						{":deletedOn", default(DateTimeOffset).ToString("o")}
					}
				},
				Limit = 1
			});
			var roles = await search.GetRemainingAsync(cancellationToken);
			return roles?.FirstOrDefault();
		}

		public void Dispose() {}

		public Task EnsureInitializedAsync(IAmazonDynamoDB client, IDynamoDBContext context,
			string rolesTableName = Constants.DefaultRolesTableName)
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

			if (rolesTableName != Constants.DefaultRolesTableName)
			{
				AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(rolesTableName, Constants.DefaultRolesTableName));
			}

			return EnsureInitializedImplAsync(client, rolesTableName);
		}

		private async Task EnsureInitializedImplAsync(IAmazonDynamoDB client, string rolesTableName)
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
					IndexName = "NormalizedName-DeletedOn-index",
					KeySchema = new List<KeySchemaElement>
					{
						new KeySchemaElement("NormalizedName", KeyType.HASH),
						new KeySchemaElement("DeletedOn", KeyType.RANGE)
					},
					ProvisionedThroughput = defaultProvisionThroughput,
					Projection = new Projection
					{
						ProjectionType = ProjectionType.ALL
					}
				}
			};

			var tableNames = await client.ListAllTablesAsync();

			if (!tableNames.Contains(rolesTableName))
			{
				await CreateTableAsync(client, rolesTableName, defaultProvisionThroughput, globalSecondaryIndexes);
				return;
			}

			var response = await client.DescribeTableAsync(new DescribeTableRequest {TableName = rolesTableName});
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
				await UpdateTableAsync(client, rolesTableName, indexUpdates);
			}
		}

		private async Task CreateTableAsync(IAmazonDynamoDB client, string rolesTableName,
			ProvisionedThroughput provisionedThroughput, List<GlobalSecondaryIndex> globalSecondaryIndexes)
		{
			var response = await client.CreateTableAsync(new CreateTableRequest
			{
				TableName = rolesTableName,
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
						AttributeName = "DeletedOn",
						AttributeType = ScalarAttributeType.S
					},
					new AttributeDefinition
					{
						AttributeName = "NormalizedName",
						AttributeType = ScalarAttributeType.S
					}
				},
				GlobalSecondaryIndexes = globalSecondaryIndexes
			});

			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception($"Couldn't create table {rolesTableName}");
			}

			await DynamoUtils.WaitForActiveTableAsync(client, rolesTableName);
		}

		private async Task UpdateTableAsync(IAmazonDynamoDB client, string rolesTableName,
			List<GlobalSecondaryIndexUpdate> indexUpdates)
		{
			await client.UpdateTableAsync(new UpdateTableRequest
			{
				TableName = rolesTableName,
				GlobalSecondaryIndexUpdates = indexUpdates
			});

			await DynamoUtils.WaitForActiveTableAsync(client, rolesTableName);
		}
	}
}