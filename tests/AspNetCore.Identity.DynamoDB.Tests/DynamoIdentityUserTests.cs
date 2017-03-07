using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AspNetCore.Identity.DynamoDB.Extensions;
using AspNetCore.Identity.DynamoDB.Tests.Common;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace AspNetCore.Identity.DynamoDB.Tests
{
	public class DynamoIdentityUserTests
	{
		public class MyIdentityUser : DynamoIdentityUser
		{
			public MyIdentityUser() {}

			public MyIdentityUser(string userName) : base(userName) {}

			public string MyCustomThing { get; set; }
		}

		[Fact]
		public async Task DynamoIdentityUser_CanBeSavedAndRetrieved_WhenItBecomesTheSubclass()
		{
			var username = TestUtils.RandomString(10);
			var email = TestUtils.RandomString(10);
			var countryName = TestUtils.RandomString(10);
			var loginProvider = TestUtils.RandomString(5);
			var providerKey = TestUtils.RandomString(5);
			var displayName = TestUtils.RandomString(5);
			var myCustomThing = TestUtils.RandomString(10);
			var user = new MyIdentityUser(username) {MyCustomThing = myCustomThing};
			var claim = new Claim(ClaimTypes.Country, countryName);
			user.AddClaim(claim);
			var login = new UserLoginInfo(loginProvider, providerKey, displayName);
			user.AddLogin(login);
			user.SetEmail(email);

			using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
			{
				var roleUsersStore = new DynamoRoleUsersStore<DynamoIdentityRole, MyIdentityUser>();
				await roleUsersStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context, TestUtils.NewTableName());

				var store = new DynamoUserStore<MyIdentityUser, DynamoIdentityRole>(roleUsersStore);
				await store.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context, TestUtils.NewTableName());

				// ACT, ASSERT
				var result = await store.CreateAsync(user, CancellationToken.None);
				Assert.True(result.Succeeded);

				// ACT, ASSERT
				var retrievedUser = await store.FindByIdAsync(user.Id, CancellationToken.None);
				Assert.NotNull(retrievedUser);
				Assert.Equal(username, retrievedUser.UserName);
				Assert.Equal(myCustomThing, retrievedUser.MyCustomThing);

				var countryClaim = retrievedUser.GetClaims().FirstOrDefault(x => x.Type == ClaimTypes.Country);
				Assert.NotNull(countryClaim);
				Assert.Equal(countryName, countryClaim.Value);

				var retrievedLoginProvider = retrievedUser.GetLogins().FirstOrDefault(x => x.LoginProvider == loginProvider);
				Assert.NotNull(retrievedLoginProvider);
				Assert.Equal(providerKey, retrievedLoginProvider.ProviderKey);
				Assert.Equal(displayName, retrievedLoginProvider.ProviderDisplayName);

				var userByLogin = await store.FindByLoginAsync(loginProvider, providerKey, CancellationToken.None);
				Assert.NotNull(userByLogin);
				var retrivedLogins = userByLogin.GetLogins();
				Assert.NotEmpty(retrivedLogins);
				Assert.True(retrivedLogins[0].EqualsTo(login));

				var userByEmail = await store.FindByEmailAsync(user.NormalizedEmail, CancellationToken.None);
				Assert.NotNull(userByEmail);
				var retrievedEmail = userByEmail.Email.Value;
				Assert.NotEmpty(retrievedEmail);
				Assert.True(retrievedEmail.Equals(email));

				var userByName = await store.FindByNameAsync(user.NormalizedUserName, CancellationToken.None);
				Assert.NotNull(userByName);
				var retrievedUserName = userByName.UserName;
				Assert.NotEmpty(retrievedUserName);
				Assert.True(retrievedUserName.Equals(username));

				var usersForClaims = await store.GetUsersForClaimAsync(claim, CancellationToken.None);
				Assert.NotNull(usersForClaims);
				Assert.NotEmpty(usersForClaims);
				var userByClaim = usersForClaims[0];
				Assert.NotNull(userByClaim);
				var userClaims = userByClaim.GetClaims();
				Assert.NotNull(userClaims);
				Assert.NotEmpty(userClaims);
				Assert.Equal(userClaims[0].Type, claim.Type);
				Assert.Equal(userClaims[0].Value, claim.Value);
			}
		}

		[Fact]
		public async Task DynamoIdentityUser_ShouldSaveAndRetrieveTheFutureOccuranceCorrectly()
		{
			var lockoutEndDate = new DateTime(2018, 2, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(8996910);
			var user = new DynamoIdentityUser(TestUtils.RandomString(10));
			user.LockUntil(lockoutEndDate);

			using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
			{
				var roleUsers = new DynamoRoleUsersStore<DynamoIdentityRole, DynamoIdentityUser>();
				await roleUsers.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context, TestUtils.NewTableName());

				var store = new DynamoUserStore<DynamoIdentityUser, DynamoIdentityRole>(roleUsers);
				await store.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context, TestUtils.NewTableName());

				// ACT
				var result = await store.CreateAsync(user, CancellationToken.None);

				// ASSERT
				Assert.True(result.Succeeded);
				var retrievedUser = await dbProvider.Context.LoadAsync(user);
				Assert.Equal(user.LockoutEndDate, retrievedUser.LockoutEndDate);
			}
		}
	}
}