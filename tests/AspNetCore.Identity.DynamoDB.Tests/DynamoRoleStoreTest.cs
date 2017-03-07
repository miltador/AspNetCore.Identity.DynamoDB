using AspNetCore.Identity.DynamoDB.Tests.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Xunit;

namespace AspNetCore.Identity.DynamoDB.Tests
{
    public class DynamoRoleStoreTests
    {
        [Fact]
        public async Task DynamoRoleStore_Create_CreatesRole()
        {
            var roleName = TestUtils.RandomString(10);
            var role = new DynamoIdentityRole(roleName);

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var roleStore = new DynamoRoleStore<DynamoIdentityRole>();
                await roleStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context);
                
                // ACT
                await roleStore.CreateAsync(role, CancellationToken.None);

                // ASSERT
                var result = await roleStore.FindByIdAsync(role.Id, CancellationToken.None);
                Assert.Equal(role.Id, result.Id);
            }
        }

        [Fact]
        public async Task DynamoRoleStore_Update_UpdatesRole()
        {
            var roleName = TestUtils.RandomString(10);
            var role = new DynamoIdentityRole(roleName);

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var roleStore = new DynamoRoleStore<DynamoIdentityRole>();
                await roleStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context);
                await roleStore.CreateAsync(role, CancellationToken.None);
                role.AddClaim(new Claim("test", "test"));

                // ACT
                await roleStore.UpdateAsync(role, CancellationToken.None);

                // ASSERT
                var result = await roleStore.FindByIdAsync(role.Id, CancellationToken.None);
                Assert.Equal(role.Id, result.Id);
                Assert.Contains("test", result.ClaimTypes);
                Assert.Contains("test", result.ClaimValues);
            }
        }

        [Fact]
        public async Task DynamoRoleStore_Delete_DeletesRole()
        {
            var roleName = TestUtils.RandomString(10);
            var role = new DynamoIdentityRole(roleName);

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var roleStore = new DynamoRoleStore<DynamoIdentityRole>();
                await roleStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context);
                await roleStore.CreateAsync(role, CancellationToken.None);
                await Task.Delay(2000);
                role = await roleStore.FindByIdAsync(role.Id, CancellationToken.None);

                // ACT
                await roleStore.DeleteAsync(role, CancellationToken.None);

                // ASSERT
                var result = await roleStore.FindByIdAsync(role.Id, CancellationToken.None);
                Assert.Null(result);
            }
        }

        [Fact]
        public async Task DynamoRoleStore_FindByName_FindsRole()
        {
            var roleName = TestUtils.RandomString(10);
            var role = new DynamoIdentityRole(roleName);
            Assert.Equal(roleName.ToUpper(), role.NormalizedName);

            using (var dbProvider = DynamoDbServerTestUtils.CreateDatabase())
            {
                var roleStore = new DynamoRoleStore<DynamoIdentityRole>();
                await roleStore.EnsureInitializedAsync(dbProvider.Client, dbProvider.Context);
                await roleStore.CreateAsync(role, CancellationToken.None);

                // ACT
                var result = await roleStore.FindByNameAsync(roleName.ToUpper(), CancellationToken.None);

                // ASSERT
                Assert.NotNull(result);
                Assert.Equal(roleName, result.Name);
                Assert.Equal(role.Id, result.Id);
            }
        }
    }
}
