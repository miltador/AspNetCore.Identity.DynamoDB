using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AspNetCore.Identity.DynamoDB.Tests.Common;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace AspNetCore.Identity.DynamoDB.Tests
{
    public class DynamoIdentityUserTests
    {
        [Fact]
        public async Task DynamoIdentityUser_CanBeSavedAndRetrieved_WhenItBecomesTheSubclass()
        {
            var username = TestUtils.RandomString(10);
            var countryName = TestUtils.RandomString(10);
            var loginProvider = TestUtils.RandomString(5);
            var providerKey = TestUtils.RandomString(5);
            var displayName = TestUtils.RandomString(5);
            var myCustomThing = TestUtils.RandomString(10);
            var user = new MyIdentityUser(username) { MyCustomThing = myCustomThing };
            user.AddClaim(new Claim(ClaimTypes.Country, countryName));
            user.AddLogin(new UserLoginInfo(loginProvider, providerKey, displayName));

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var store = new DynamoUserStore<MyIdentityUser>(dbProvider.Client, dbProvider.Context, TestUtils.NewTableName());

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
                var store = new DynamoUserStore<DynamoIdentityUser>(dbProvider.Client, dbProvider.Context, TestUtils.NewTableName());

                // ACT
                var result = await store.CreateAsync(user, CancellationToken.None);

                // ASSERT
                Assert.True(result.Succeeded);
                var retrievedUser = await dbProvider.Context.LoadAsync(user);
                Assert.Equal(user.LockoutEndDate, retrievedUser.LockoutEndDate);
            }
        }

        public class MyIdentityUser : DynamoIdentityUser
        {
            public MyIdentityUser() { }

            public MyIdentityUser(string userName) : base(userName)
            {
            }

            public string MyCustomThing { get; set; }
        }
    }
}