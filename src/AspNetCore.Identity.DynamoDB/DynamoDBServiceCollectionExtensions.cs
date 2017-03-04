using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace AspNetCore.Identity.DynamoDB
{
    public static class DynamoDBServiceCollectionExtensions
    {
        public static DynamoDBIdentityBuilder<TUser,TRole> AddDynamoDBIdentity<TUser, TRole>(this IServiceCollection services)
            where TUser: DynamoIdentityUser
            where TRole: DynamoIdentityRole
        {
            return new DynamoDBIdentityBuilder<TUser,TRole>(services);
        }
    }
}
