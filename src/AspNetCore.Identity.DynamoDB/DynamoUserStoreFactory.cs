using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;

namespace AspNetCore.Identity.DynamoDB
{
	public static class DynamoUserStoreFactory
	{
		[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
		private static bool _initialized;

		[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
		private static object _initializationLock = new object();

		[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
		private static object _initializationTarget;

		public static async Task<DynamoUserStore<TUser>> New<TUser>(IAmazonDynamoDB client, IDynamoDBContext context,
			string userTableName = Constants.DefaultTableName) where TUser : DynamoIdentityUser
		{
			if(context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}

			if (userTableName != Constants.DefaultTableName)
			{
				AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(userTableName, Constants.DefaultTableName));
			}

			await EnsureInitializedAsync(client, userTableName);
			return new DynamoUserStore<TUser>(context);
		}

		private static Task EnsureInitializedAsync(IAmazonDynamoDB client, string userTableName)
		{
			var obj = LazyInitializer.EnsureInitialized(ref _initializationTarget, ref _initialized,
				ref _initializationLock,() => EnsureInitializedImplAsync(client, userTableName));

			return (Task)obj;
		}

		private static async Task EnsureInitializedImplAsync(IAmazonDynamoDB client, string userTableName)
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
					IndexName = "NormalizedUserName-DeletedOn-index",
					KeySchema = new List<KeySchemaElement>
					{
						new KeySchemaElement("NormalizedUserName", KeyType.HASH),
						new KeySchemaElement("DeletedOn", KeyType.RANGE)
					},
					ProvisionedThroughput = defaultProvisionThroughput,
					Projection = new Projection
					{
						ProjectionType = ProjectionType.ALL
					}
				},
				new GlobalSecondaryIndex
				{
					IndexName = "Email.NormalizedValue-DeletedOn-index",
					KeySchema = new List<KeySchemaElement>
					{
						new KeySchemaElement("Email.NormalizedValue", KeyType.HASH),
						new KeySchemaElement("DeletedOn", KeyType.RANGE)
					},
					ProvisionedThroughput = defaultProvisionThroughput,
					Projection = new Projection
					{
						ProjectionType = ProjectionType.ALL
					}
				}
			};

			var tablesResponse = await client.ListTablesAsync();
			if (tablesResponse.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception("Couldn't get list of tables");
			}
			var tableNames = tablesResponse.TableNames;

			if (!tableNames.Contains(userTableName))
			{
				await CreateTableAsync(client, userTableName, defaultProvisionThroughput, globalSecondaryIndexes);
				return;
			}

			var response = await client.DescribeTableAsync(new DescribeTableRequest { TableName = userTableName });
			var table = response.Table;

			var indexesToAdd = globalSecondaryIndexes.Where(g => !table.GlobalSecondaryIndexes.Exists(gd =>
				gd.IndexName.Equals(g.IndexName)));
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
				await UpdateTableAsync(client, userTableName, indexUpdates);
			}
		}

		private static async Task CreateTableAsync(IAmazonDynamoDB client, string userTableName,
			ProvisionedThroughput provisionedThroughput, List<GlobalSecondaryIndex> globalSecondaryIndexes)
		{
			await client.CreateTableAsync(new CreateTableRequest
			{
				TableName = userTableName,
				ProvisionedThroughput = provisionedThroughput,
				KeySchema = new List<KeySchemaElement>
				{
					new KeySchemaElement
					{
						AttributeName = "Id",
						KeyType = KeyType.HASH
					},
					new KeySchemaElement
					{
						AttributeName = "DeletedOn",
						KeyType = KeyType.RANGE
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
						AttributeName = "NormalizedUserName",
						AttributeType = ScalarAttributeType.S

					},
					new AttributeDefinition
					{
						AttributeName = "Email.NormalizedValue",
						AttributeType = ScalarAttributeType.S
					}
				},
				GlobalSecondaryIndexes = globalSecondaryIndexes
			});

			await WaitForActiveTableAsync(client, userTableName);
		}

		private static async Task WaitForActiveTableAsync(IAmazonDynamoDB client, string userTableName)
		{
			bool active;
			do
			{
				Console.WriteLine("Waiting for the table to become active and indexes got populated...");
				Thread.Sleep(TimeSpan.FromSeconds(5));
				active = true;
				var response = await client.DescribeTableAsync(new DescribeTableRequest { TableName = userTableName });
				if (!Equals(response.Table.TableStatus, TableStatus.ACTIVE) ||
				    !response.Table.GlobalSecondaryIndexes.TrueForAll(g => Equals(g.IndexStatus, IndexStatus.ACTIVE)))
				{
					active = false;
				}
			} while (!active);
		}

		private static async Task UpdateTableAsync(IAmazonDynamoDB client, string userTableName,
			List<GlobalSecondaryIndexUpdate> indexUpdates)
		{
			await client.UpdateTableAsync(new UpdateTableRequest
			{
				TableName = userTableName,
				GlobalSecondaryIndexUpdates = indexUpdates
			});

			await WaitForActiveTableAsync(client, userTableName);
		}
	}
}