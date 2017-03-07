using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.Identity.DynamoDB
{
	public static class DynamoDBServiceCollectionExtensions
	{
		public static DynamoDBIdentityBuilder<TUser, TRole> AddDynamoDBIdentity<TUser, TRole>(this IServiceCollection services)
			where TUser : DynamoIdentityUser
			where TRole : DynamoIdentityRole
		{
			return new DynamoDBIdentityBuilder<TUser, TRole>(services);
		}
	}
}