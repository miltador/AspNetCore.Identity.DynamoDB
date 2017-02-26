using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IdentitySample.Services;
using Microsoft.AspNetCore.Identity;
using System.IO;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using AspNetCore.Identity.DynamoDB;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;

namespace IdentitySample
{
    public class DynamoDbSettings
    {
        public string ServiceUrl { get; set; }
        public string TableName { get; set; }
    }

    public class Startup
    {
        private readonly IHostingEnvironment _env;

        public Startup(IHostingEnvironment env)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment() )
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets("userSecretsId"); // https://github.com/aspnet/UserSecrets/issues/62
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
            _env = env;
        }

        public IConfigurationRoot Configuration { get; set; }

        /// <summary>
        /// see: https://github.com/aspnet/Identity/blob/79dbed5a924e96a22b23ae6c84731e0ac806c2b5/src/Microsoft.AspNetCore.Identity/IdentityServiceCollectionExtensions.cs#L46-L68
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<DynamoDbSettings>(Configuration.GetSection("DynamoDB"));
            services.AddSingleton<IUserStore<DynamoIdentityUser>>(p => new DynamoUserStore<DynamoIdentityUser>());

            services.Configure<IdentityOptions>(options =>
            {
                var dataProtectionPath = Path.Combine(_env.WebRootPath, "identity-artifacts");
                options.Cookies.ApplicationCookie.AuthenticationScheme = "ApplicationCookie";
                options.Cookies.ApplicationCookie.DataProtectionProvider = DataProtectionProvider.Create(dataProtectionPath);
                options.Lockout.AllowedForNewUsers = true;
            });

            // Services used by identity
            services.AddAuthentication(options =>
            {
                // This is the Default value for ExternalCookieAuthenticationScheme
                options.SignInScheme = new IdentityCookieOptions().ExternalCookieAuthenticationScheme;
            });

            // Hosting doesn't add IHttpContextAccessor by default
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddOptions();
            services.AddDataProtection();

            services.TryAddSingleton<IdentityMarkerService>();
            services.TryAddSingleton<IUserValidator<DynamoIdentityUser>, UserValidator<DynamoIdentityUser>>();
            services.TryAddSingleton<IPasswordValidator<DynamoIdentityUser>, PasswordValidator<DynamoIdentityUser>>();
            services.TryAddSingleton<IPasswordHasher<DynamoIdentityUser>, PasswordHasher<DynamoIdentityUser>>();
            services.TryAddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();
            services.TryAddSingleton<IdentityErrorDescriber>();
            services.TryAddSingleton<ISecurityStampValidator, SecurityStampValidator<DynamoIdentityUser>>();
            services.TryAddSingleton<IUserClaimsPrincipalFactory<DynamoIdentityUser>, UserClaimsPrincipalFactory<DynamoIdentityUser>>();
            services.TryAddSingleton<UserManager<DynamoIdentityUser>, UserManager<DynamoIdentityUser>>();
            services.TryAddScoped<SignInManager<DynamoIdentityUser>, SignInManager<DynamoIdentityUser>>();

            AddDefaultTokenProviders(services);

            services.AddMvc();

            // Add application services.
            services.AddTransient<IEmailSender, AuthMessageSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            // To configure external authentication please see http://go.microsoft.com/fwlink/?LinkID=532715

            app.UseIdentity()
               .UseFacebookAuthentication(new FacebookOptions
               {
                   AppId = "901611409868059",
                   AppSecret = "4aa3c530297b1dcebc8860334b39668b"
               })
                .UseGoogleAuthentication(new GoogleOptions
                {
                    ClientId = "609695036148-vacck6ur8cf5sk0uv145pa962qfsdk6c.apps.googleusercontent.com",
                    ClientSecret = "VqJhjh9w_tvSahjzeOkWzv3n"
                })
                .UseTwitterAuthentication(new TwitterOptions
                {
                    ConsumerKey = "BSdJJ0CrDuvEhpkchnukXZBUv",
                    ConsumerSecret = "xKUNuKhsRdHD03eLn67xhPAyE1wFFEndFo1X2UJaK2m1jdAxf4"
                });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

	        // Initialize store: create table, add global indexes, wait for table to become active
	        var store = (DynamoUserStore<DynamoIdentityUser>)app.ApplicationServices.GetService<IUserStore<DynamoIdentityUser>>();
	        var options = app.ApplicationServices.GetService<IOptions<DynamoDbSettings>>();
	        var client = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
	        {
		        ServiceURL = options.Value.ServiceUrl
	        });
	        var context = new DynamoDBContext(client);
	        store.InitializeTableAsync(client, context, options.Value.TableName).GetAwaiter().GetResult();
        }

        private void AddDefaultTokenProviders(IServiceCollection services)
        {
            var dataProtectionProviderType = typeof(DataProtectorTokenProvider<>).MakeGenericType(typeof(DynamoIdentityUser));
            var phoneNumberProviderType = typeof(PhoneNumberTokenProvider<>).MakeGenericType(typeof(DynamoIdentityUser));
            var emailTokenProviderType = typeof(EmailTokenProvider<>).MakeGenericType(typeof(DynamoIdentityUser));
            AddTokenProvider(services, TokenOptions.DefaultProvider, dataProtectionProviderType);
            AddTokenProvider(services, TokenOptions.DefaultEmailProvider, emailTokenProviderType);
            AddTokenProvider(services, TokenOptions.DefaultPhoneProvider, phoneNumberProviderType);
        }

        private void AddTokenProvider(IServiceCollection services, string providerName, Type provider)
        {
            services.Configure<IdentityOptions>(options =>
            {
                options.Tokens.ProviderMap[providerName] = new TokenProviderDescriptor(provider);
            });

            services.AddSingleton(provider);
        }

        public class UserClaimsPrincipalFactory<TUser> : IUserClaimsPrincipalFactory<TUser>
            where TUser : class
        {
            public UserClaimsPrincipalFactory(
                UserManager<TUser> userManager,
                IOptions<IdentityOptions> optionsAccessor)
            {
                if (userManager == null)
                {
                    throw new ArgumentNullException(nameof(userManager));
                }
                if (optionsAccessor == null || optionsAccessor.Value == null)
                {
                    throw new ArgumentNullException(nameof(optionsAccessor));
                }

                UserManager = userManager;
                Options = optionsAccessor.Value;
            }

            public UserManager<TUser> UserManager { get; private set; }

            public IdentityOptions Options { get; private set; }

            public virtual async Task<ClaimsPrincipal> CreateAsync(TUser user)
            {
                if (user == null)
                {
                    throw new ArgumentNullException(nameof(user));
                }

                var userId = await UserManager.GetUserIdAsync(user);
                var userName = await UserManager.GetUserNameAsync(user);
                var id = new ClaimsIdentity(Options.Cookies.ApplicationCookieAuthenticationScheme,
                    Options.ClaimsIdentity.UserNameClaimType,
                    Options.ClaimsIdentity.RoleClaimType);
                id.AddClaim(new Claim(Options.ClaimsIdentity.UserIdClaimType, userId));
                id.AddClaim(new Claim(Options.ClaimsIdentity.UserNameClaimType, userName));
                if (UserManager.SupportsUserSecurityStamp)
                {
                    id.AddClaim(new Claim(Options.ClaimsIdentity.SecurityStampClaimType,
                        await UserManager.GetSecurityStampAsync(user)));
                }
                if (UserManager.SupportsUserRole)
                {
                    var roles = await UserManager.GetRolesAsync(user);
                    foreach (var roleName in roles)
                    {
                        id.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, roleName));
                    }
                }
                if (UserManager.SupportsUserClaim)
                {
                    id.AddClaims(await UserManager.GetClaimsAsync(user));
                }

                return new ClaimsPrincipal(id);
            }
        }
    }
}

