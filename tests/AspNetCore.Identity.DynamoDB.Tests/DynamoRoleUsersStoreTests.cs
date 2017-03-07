using AspNetCore.Identity.DynamoDB.Tests.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCore.Identity.DynamoDB.Tests
{
    public class DynamoRoleUsersStoreTests
    {
        [Fact]
        public async Task DynamoRoleUsersStore_AddToRole_CreatesRoleUserEntry()
        {
            var user = new DynamoIdentityUser(TestUtils.RandomString(10));
            var roleName = TestUtils.RandomString(10);

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var roleStore = new DynamoRoleUsersStore<DynamoIdentityRole, DynamoIdentityUser>();
                await roleStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context);
                
                // ACT
                await roleStore.AddToRoleAsync(user, roleName, CancellationToken.None);

                // ASSERT
                var result = await roleStore.IsInRoleAsync(user, roleName, CancellationToken.None);
                Assert.True(result);
            }
        }

        [Fact]
        public async Task DynamoRoleUsersStore_AddToRole_HandlesDuplicateRoleUserEntry()
        {
            var user = new DynamoIdentityUser(TestUtils.RandomString(10));
            var roleName = TestUtils.RandomString(10);

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var roleStore = new DynamoRoleUsersStore<DynamoIdentityRole, DynamoIdentityUser>();
                await roleStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context);
                await roleStore.AddToRoleAsync(user, roleName, CancellationToken.None);
                var result = await roleStore.IsInRoleAsync(user, roleName, CancellationToken.None);
                Assert.True(result);

                // ACT
                await roleStore.AddToRoleAsync(user, roleName, CancellationToken.None);

                // ASSERT
                var roles = await roleStore.GetRolesAsync(user, CancellationToken.None);
                Assert.Equal(1, roles.Count);
            }
        }

        [Fact]
        public async Task DynamoRoleUsersStore_RemoveFromRole_RemovesUserFromRole()
        {
            var user = new DynamoIdentityUser(TestUtils.RandomString(10));
            var roleName = TestUtils.RandomString(10);

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var roleStore = new DynamoRoleUsersStore<DynamoIdentityRole, DynamoIdentityUser>();
                await roleStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context);
                await roleStore.AddToRoleAsync(user, roleName, CancellationToken.None);
                var result = await roleStore.IsInRoleAsync(user, roleName, CancellationToken.None);
                Assert.True(result);

                // ACT
                await roleStore.RemoveFromRoleAsync(user, roleName, CancellationToken.None);

                // ASSERT
                var result2 = await roleStore.IsInRoleAsync(user, roleName, CancellationToken.None);
                Assert.False(result2);
            }
        }

        [Fact]
        public async Task DynamoRoleUsersStore_GetUserIdsInRole_GetsUsers()
        {
            var user1 = new DynamoIdentityUser(TestUtils.RandomString(10));
            var user2 = new DynamoIdentityUser(TestUtils.RandomString(10));
            var roleName = TestUtils.RandomString(10);

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var roleStore = new DynamoRoleUsersStore<DynamoIdentityRole, DynamoIdentityUser>();
                await roleStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context);

                await roleStore.AddToRoleAsync(user1, roleName, CancellationToken.None);
                Assert.True(await roleStore.IsInRoleAsync(user1, roleName, CancellationToken.None));

                await roleStore.AddToRoleAsync(user2, roleName, CancellationToken.None);
                Assert.True(await roleStore.IsInRoleAsync(user2, roleName, CancellationToken.None));

                // ACT
                var result = await roleStore.GetUserIdsInRoleAsync(roleName, CancellationToken.None);

                // ASSERT
                Assert.Contains(user1.Id, result);
                Assert.Contains(user2.Id, result);
                Assert.Equal(2, result.Count);
            }
        }
    }
}
