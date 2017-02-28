using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AspNetCore.Identity.DynamoDB
{
    public class DynamoUtils
    {
        public static async Task WaitForActiveTableAsync(IAmazonDynamoDB client, string userTableName)
        {
            bool active;
            do
            {
                active = true;
                var response = await client.DescribeTableAsync(new DescribeTableRequest { TableName = userTableName });
                if (!Equals(response.Table.TableStatus, TableStatus.ACTIVE) ||
                    !response.Table.GlobalSecondaryIndexes.TrueForAll(g => Equals(g.IndexStatus, IndexStatus.ACTIVE)))
                {
                    active = false;
                }
                Console.WriteLine($"Waiting for table {userTableName} to become active...");
                await Task.Delay(TimeSpan.FromSeconds(5));
            } while (!active);
        }
    }
}
