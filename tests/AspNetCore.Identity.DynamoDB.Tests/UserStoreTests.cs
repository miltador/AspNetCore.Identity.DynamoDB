using System.Threading;
using System.Threading.Tasks;
using AspNetCore.Identity.DynamoDB.Tests.Common;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace AspNetCore.Identity.DynamoDB.Tests
{
    public class UserStoreTests
    {
        [Fact]
        public async Task CreateAsync_ShouldCreateUser()
        {
            // ARRANGE
            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var userStore = await DynamoUserStoreFactory.New<DynamoIdentityUser>(dbProvider.Client,
	                dbProvider.Context, TestUtils.NewTableName()) as IUserStore<DynamoIdentityUser>;
                var user = new DynamoIdentityUser(TestUtils.RandomString(10));

                // ACT
                await userStore.CreateAsync(user, CancellationToken.None);

                // ASSERT
                var retrievedUser = await dbProvider.Context.LoadAsync(user);

                Assert.NotNull(retrievedUser);
                Assert.Equal(user.UserName, retrievedUser.UserName);
                Assert.Equal(user.NormalizedUserName, retrievedUser.NormalizedUserName);
            }
        }
    }
}