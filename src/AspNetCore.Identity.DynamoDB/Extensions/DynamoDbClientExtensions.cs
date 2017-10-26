using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.DynamoDB.Extensions
{
	public static class DynamoDbClientExtensions
    {
		public static async Task<List<string>> ListAllTablesAsync(this IAmazonDynamoDB client)
		{
			var tables = new List<string>();

			var tableResponse = await ListTablesAsync(client, null);
			tables.AddRange(tableResponse.TableNames);
			while (tableResponse.LastEvaluatedTableName != null)
			{
				tableResponse = await ListTablesAsync(client, tableResponse.LastEvaluatedTableName);
				tables.AddRange(tableResponse.TableNames);
			}

			return tables;
		}

		private static async Task<ListTablesResponse> ListTablesAsync(IAmazonDynamoDB client, string lastTableName)
		{
			var tablesResponse = await client.ListTablesAsync(lastTableName);
			if (tablesResponse.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception("Couldn't get list of tables");
			}

			return tablesResponse;
		}
    }
}
