using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Amazon.DynamoDBv2.DataModel;
using AspNetCore.Identity.DynamoDB.Converters;

namespace AspNetCore.Identity.DynamoDB
{
	[DynamoDBTable(Constants.DefaultRolesTableName)]
	public class DynamoIdentityRole
	{
		public DynamoIdentityRole()
		{
			Id = Guid.NewGuid().ToString();
			CreatedOn = DateTimeOffset.Now;
		}

		public DynamoIdentityRole(string name) : this()
		{
			Name = name;
			NormalizedName = name.ToUpper();
		}

		[DynamoDBHashKey]
		public string Id { get; set; }

		public string Name { get; set; }

		[DynamoDBGlobalSecondaryIndexHashKey("NormalizedName-DeletedOn-index")]
		public string NormalizedName { get; set; }

		public List<string> ClaimTypes { get; set; } = new List<string>();
		public List<string> ClaimValues { get; set; } = new List<string>();

		[DynamoDBGlobalSecondaryIndexRangeKey("NormalizedName-DeletedOn-index",
			Converter = typeof(DateTimeOffsetConverter))]
		public DateTimeOffset DeletedOn { get; set; }

		[DynamoDBProperty(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset CreatedOn { get; set; }

		[DynamoDBVersion]
		public int? VersionNumber { get; set; }

		public void Delete()
		{
			if (DeletedOn != default(DateTimeOffset))
			{
				throw new InvalidOperationException($"Role '{Id}' has already been deleted.");
			}

			DeletedOn = DateTimeOffset.Now;
		}

		public virtual void AddClaim(Claim claim)
		{
			if (claim == null)
			{
				throw new ArgumentNullException(nameof(claim));
			}

			ClaimTypes.Add(claim.Type);
			ClaimValues.Add(claim.Value);
		}

		public virtual IList<Claim> GetClaims()
		{
			return ClaimTypes.Select((t, i) => new Claim(t, ClaimValues[i])).ToList();
		}

		public virtual void RemoveClaim(Claim claim)
		{
			if (claim == null)
			{
				throw new ArgumentNullException(nameof(claim));
			}

			var index = ClaimTypes.IndexOf(claim.Type);
			ClaimTypes.Remove(claim.Type);
			ClaimValues.RemoveAt(index);
		}
	}
}