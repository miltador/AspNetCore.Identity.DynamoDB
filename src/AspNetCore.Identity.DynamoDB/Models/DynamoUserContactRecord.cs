using System;
using Amazon.DynamoDBv2.DataModel;
using AspNetCore.Identity.DynamoDB.Converters;

namespace AspNetCore.Identity.DynamoDB.Models
{
	public abstract class DynamoUserContactRecord : IEquatable<DynamoUserEmail>
	{
		protected DynamoUserContactRecord() {}

		protected DynamoUserContactRecord(string value) : this()
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			Value = value;
		}

		public string Value { get; set; }

		[DynamoDBProperty(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset ConfirmedOn { get; set; }

		public bool Equals(DynamoUserEmail other)
		{
			return other != null && other.Value.Equals(Value);
		}

		public bool IsConfirmed()
		{
			return ConfirmedOn != default(DateTimeOffset);
		}

		public void SetConfirmed()
		{
			SetConfirmed(DateTimeOffset.Now);
		}

		public void SetConfirmed(DateTimeOffset confirmationTime)
		{
			if (ConfirmedOn == default(DateTimeOffset))
			{
				ConfirmedOn = confirmationTime;
			}
		}

		public void SetUnconfirmed()
		{
			ConfirmedOn = default(DateTimeOffset);
		}
	}
}