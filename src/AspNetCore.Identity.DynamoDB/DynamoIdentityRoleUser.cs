using System;
using Amazon.DynamoDBv2.DataModel;
using AspNetCore.Identity.DynamoDB.Converters;

namespace AspNetCore.Identity.DynamoDB
{
	[DynamoDBTable(Constants.DefaultRoleUsersTableName)]
	public class DynamoIdentityRoleUser
	{
		public DynamoIdentityRoleUser()
		{
			Id = Guid.NewGuid().ToString();
			CreatedOn = DateTimeOffset.Now;
		}

		public DynamoIdentityRoleUser(string normalisedRoleName, string userId) : this()
		{
			NormalizedRoleName = normalisedRoleName;
			UserId = userId;
		}

		[DynamoDBHashKey]
		public string Id { get; set; }

		[DynamoDBGlobalSecondaryIndexHashKey("NormalizedRoleName-UserId-index")]
		[DynamoDBGlobalSecondaryIndexRangeKey("UserId-NormalizedRoleName-index")]
		public string NormalizedRoleName { get; set; }

		[DynamoDBGlobalSecondaryIndexRangeKey("NormalizedRoleName-UserId-index")]
		[DynamoDBGlobalSecondaryIndexHashKey("UserId-NormalizedRoleName-index")]
		public string UserId { get; set; }

		[DynamoDBProperty(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset CreatedOn { get; set; }

		[DynamoDBVersion]
		public int? VersionNumber { get; set; }
	}
}