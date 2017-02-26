using System.Threading;
using System.Threading.Tasks;
using AspNetCore.Identity.DynamoDB.Tests.Common;
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
	            var store = new DynamoUserStore<DynamoIdentityUser>();
	            await store.InitializeTableAsync(dbProvider.Client, dbProvider.Context, TestUtils.NewTableName());
                var user = new DynamoIdentityUser(TestUtils.RandomString(10));

                // ACT
                await store.CreateAsync(user, CancellationToken.None);

                // ASSERT
                var retrievedUser = await dbProvider.Context.LoadAsync(user);

                Assert.NotNull(retrievedUser);
                Assert.Equal(user.UserName, retrievedUser.UserName);
                Assert.Equal(user.NormalizedUserName, retrievedUser.NormalizedUserName);
            }
        }
    }
}