using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.Identity.DynamoDB
{
	public class DynamoDBIdentityBuilder<TUser, TRole>
		where TUser : DynamoIdentityUser
		where TRole : DynamoIdentityRole
	{
		public DynamoDBIdentityBuilder(IServiceCollection services)
		{
			Services = services;
			UserType = typeof(TUser);
			RoleType = typeof(TRole);
		}

		public IServiceCollection Services { get; set; }
		public Type UserType { get; set; }
		public Type RoleType { get; set; }

		private DynamoDBIdentityBuilder<TUser, TRole> AddScoped(Type serviceType, Type concreteType)
		{
			Services.AddScoped(serviceType, concreteType);
			return this;
		}

		private DynamoDBIdentityBuilder<TUser, TRole> AddSingleton(Type serviceType, Type concreteType)
		{
			Services.AddSingleton(serviceType, concreteType);
			return this;
		}

		public DynamoDBIdentityBuilder<TUser, TRole> AddUserStore<T>() where T : class
		{
			return AddSingleton(typeof(IUserStore<>).MakeGenericType(UserType), typeof(T));
		}

		public DynamoDBIdentityBuilder<TUser, TRole> AddUserStore()
		{
			return AddUserStore<DynamoUserStore<TUser, TRole>>();
		}

		public DynamoDBIdentityBuilder<TUser, TRole> AddRoleStore<T>() where T : class
		{
			return AddSingleton(typeof(IRoleStore<>).MakeGenericType(RoleType), typeof(T));
		}

		public DynamoDBIdentityBuilder<TUser, TRole> AddRoleStore()
		{
			return AddRoleStore<DynamoRoleStore<TRole>>();
		}

		public DynamoDBIdentityBuilder<TUser, TRole> AddRoleUsersStore<T>() where T : class
		{
			return AddSingleton(typeof(T), typeof(T));
		}

		public DynamoDBIdentityBuilder<TUser, TRole> AddRoleUsersStore()
		{
			return AddRoleUsersStore<DynamoRoleUsersStore<TRole, TUser>>();
		}
	}
}
