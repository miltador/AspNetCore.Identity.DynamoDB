namespace AspNetCore.Identity.DynamoDB.Models
{
	public class DynamoUserPhoneNumber : DynamoUserContactRecord
	{
		public DynamoUserPhoneNumber() {}

		public DynamoUserPhoneNumber(string phoneNumber) : base(phoneNumber) {}
	}
}