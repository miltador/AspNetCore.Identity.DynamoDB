using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using AspNetCore.Identity.DynamoDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentitySample
{
	public class DynamoDbSettings
	{
		public string ServiceUrl { get; set; }
		public string UsersTableName { get; set; }
		public string RolesTableName { get; set; }
		public string RoleUsersTableName { get; set; }
	}

	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			services.Configure<DynamoDbSettings>(Configuration.GetSection("DynamoDB"));

			services.Configure<CookiePolicyOptions>(options =>
			{
				// This lambda determines whether user consent for non-essential cookies is needed for a given request.
				options.CheckConsentNeeded = context => true;
				options.MinimumSameSitePolicy = SameSiteMode.None;
			});

			// Services used by identity
			services.AddAuthentication()
				.AddGoogle(options =>
				{
					options.ClientId = "609695036148-vacck6ur8cf5sk0uv145pa962qfsdk6c.apps.googleusercontent.com";
					options.ClientSecret = "VqJhjh9w_tvSahjzeOkWzv3n";
				})
				.AddFacebook(options =>
				{
					options.AppId = "901611409868059";
					options.AppSecret = "4aa3c530297b1dcebc8860334b39668b";
				}).AddTwitter(options =>
				{
					options.ConsumerKey = "BSdJJ0CrDuvEhpkchnukXZBUv";
					options.ConsumerSecret = "xKUNuKhsRdHD03eLn67xhPAyE1wFFEndFo1X2UJaK2m1jdAxf4";
				});

			services.AddDefaultIdentity<DynamoIdentityUser>()
				.AddRoles<DynamoIdentityRole>()
				.AddDefaultTokenProviders();


			services.AddSingleton<DynamoRoleUsersStore<DynamoIdentityRole, DynamoIdentityUser>, DynamoRoleUsersStore<DynamoIdentityRole, DynamoIdentityUser>>();
			services.AddSingleton<IUserStore<DynamoIdentityUser>, DynamoUserStore<DynamoIdentityUser, DynamoIdentityRole>>();
			services.AddSingleton<IRoleStore<DynamoIdentityRole>, DynamoRoleStore<DynamoIdentityRole>>();

			services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseDatabaseErrorPage();
			}
			else
			{
				app.UseExceptionHandler("/Error");
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();
			app.UseCookiePolicy();
			// To configure external authentication please see http://go.microsoft.com/fwlink/?LinkID=532715
			app.UseAuthentication();

			app.UseMvc();

			var options = app.ApplicationServices.GetService<IOptions<DynamoDbSettings>>();
			var client = env.IsDevelopment()
				? new AmazonDynamoDBClient(new AmazonDynamoDBConfig
				{
					ServiceURL = options.Value.ServiceUrl
				})
				: new AmazonDynamoDBClient();
			var context = new DynamoDBContext(client);

			var userStore = app.ApplicationServices
					.GetService<IUserStore<DynamoIdentityUser>>()
				as DynamoUserStore<DynamoIdentityUser, DynamoIdentityRole>;
			var roleStore = app.ApplicationServices
					.GetService<IRoleStore<DynamoIdentityRole>>()
				as DynamoRoleStore<DynamoIdentityRole>;
			var roleUsersStore = app.ApplicationServices
				.GetService<DynamoRoleUsersStore<DynamoIdentityRole, DynamoIdentityUser>>();

			userStore.EnsureInitializedAsync(client, context, options.Value.UsersTableName).Wait();
			roleStore.EnsureInitializedAsync(client, context, options.Value.RolesTableName).Wait();
			roleUsersStore.EnsureInitializedAsync(client, context, options.Value.RoleUsersTableName).Wait();
		}
	}
}