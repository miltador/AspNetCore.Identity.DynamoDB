using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.DynamoDB.Extensions
{
	public static class UserLoginInfoExtensions
	{
		public static bool EqualsTo(this UserLoginInfo userLoginInfo, UserLoginInfo otherUserLoginInfo)
		{
			return userLoginInfo.LoginProvider.Equals(otherUserLoginInfo.LoginProvider) &&
					userLoginInfo.ProviderKey.Equals(otherUserLoginInfo.ProviderKey) &&
					userLoginInfo.ProviderDisplayName.Equals(otherUserLoginInfo.ProviderDisplayName);
		}
	}
}