namespace AspNetCore.Identity.DynamoDB.Models
{
	public class DynamoUserEmail : DynamoUserContactRecord
	{
		public DynamoUserEmail() {}

		public DynamoUserEmail(string email) : base(email) {}
	}
}