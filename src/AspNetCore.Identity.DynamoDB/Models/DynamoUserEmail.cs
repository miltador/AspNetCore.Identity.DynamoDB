using System;

namespace AspNetCore.Identity.DynamoDB.Models
{
	public class DynamoUserEmail : DynamoUserContactRecord
	{
		public DynamoUserEmail() {}

		public DynamoUserEmail(string email) : base(email) {}

		public string NormalizedValue { get; set; }

		public virtual void SetNormalizedEmail(string normalizedEmail)
		{
			if (normalizedEmail == null)
			{
				throw new ArgumentNullException(nameof(normalizedEmail));
			}

			NormalizedValue = normalizedEmail;
		}
	}
}