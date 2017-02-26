using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Net;
using System.Security.Claims;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;
using AspNetCore.Identity.DynamoDB.Extensions;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.DynamoDB
{
    public class DynamoUserStore<TUser> : IUserLoginStore<TUser>,
        IUserClaimStore<TUser>,
        IUserPasswordStore<TUser>,
        IUserSecurityStampStore<TUser>,
        IUserTwoFactorStore<TUser>,
        IUserEmailStore<TUser>,
        IUserLockoutStore<TUser>,
        IUserPhoneNumberStore<TUser>
        where TUser : DynamoIdentityUser
    {
        private IDynamoDBContext _context;

	    public Task InitializeTableAsync(IAmazonDynamoDB client, IDynamoDBContext context,
		    string userTableName = Constants.DefaultTableName)
        {
            if(context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (userTableName != Constants.DefaultTableName)
            {
                AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(userTableName, Constants.DefaultTableName));
            }
            _context = context;

            return EnsureInitializedAsync(client, userTableName);
        }

        public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _context.SaveAsync(user, cancellationToken);

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            cancellationToken.ThrowIfCancellationRequested();

            user.Delete();

            await _context.SaveAsync(user, cancellationToken);

            return IdentityResult.Success;
        }

        public async Task<TUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var user = await _context.LoadAsync<TUser>(userId, default(DateTimeOffset), cancellationToken);
            return user.DeletedOn == default(DateTimeOffset) ? user : null;
        }

        public async Task<TUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            if (normalizedUserName == null)
            {
                throw new ArgumentNullException(nameof(normalizedUserName));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var user = await _context.LoadAsync<TUser>(normalizedUserName, default(DateTimeOffset), new DynamoDBOperationConfig
            {
                IndexName = "NormalizedUserName-DeletedOn-index"
            }, cancellationToken);
            return user;
        }

        public Task<string> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.NormalizedUserName);
        }

        public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.Id);
        }

        public Task<string> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.UserName);
        }

        public Task SetNormalizedUserNameAsync(TUser user, string normalizedName, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (normalizedName == null)
            {
                throw new ArgumentNullException(nameof(normalizedName));
            }

            user.SetNormalizedUserName(normalizedName);

            return Task.FromResult(0);
        }

        public Task SetUserNameAsync(TUser user, string userName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Changing the username is not supported.");
        }

        public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _context.SaveAsync(user, cancellationToken);

            return IdentityResult.Success;
        }

        public Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (login == null)
            {
                throw new ArgumentNullException(nameof(login));
            }

            // NOTE: Not the best way to ensure uniquness.
            if (user.GetLogins().Any(x => x.EqualsTo(login)))
            {
                throw new InvalidOperationException("Login already exists.");
            }

            user.AddLogin(login);

            return Task.FromResult(0);
        }

        public Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (loginProvider == null)
            {
                throw new ArgumentNullException(nameof(loginProvider));
            }

            if (providerKey == null)
            {
                throw new ArgumentNullException(nameof(providerKey));
            }

            var login = new UserLoginInfo(loginProvider, providerKey, string.Empty);
            var loginToRemove = user.GetLogins().FirstOrDefault(x => x.EqualsTo(login));

            if (loginToRemove != null)
            {
                user.RemoveLogin(login);
            }

            return Task.FromResult(0);
        }

        public Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.GetLogins());
        }

        public async Task<TUser> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            if (loginProvider == null)
            {
                throw new ArgumentNullException(nameof(loginProvider));
            }

            if (providerKey == null)
            {
                throw new ArgumentNullException(nameof(providerKey));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // it's a waste to do a scan but how would the query look like if not like this?
            var usersSearch = _context.ScanAsync<TUser>(new List<ScanCondition>
            {
                new ScanCondition("LoginProviders", ScanOperator.Contains, loginProvider),
                new ScanCondition("LoginProviderKeys", ScanOperator.Contains, providerKey)
            });
            // well, we guarantee that there will be only one record so the scan will not be so expensive
            var users = await usersSearch.GetRemainingAsync(cancellationToken);
            return users.FirstOrDefault(u => u.DeletedOn == default(DateTimeOffset));
        }

        public Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.GetClaims());
        }

        public Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            foreach (var claim in claims)
            {
                user.AddClaim(claim);
            }

            return Task.FromResult(0);
        }

        public Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            if (newClaim == null)
            {
                throw new ArgumentNullException(nameof(newClaim));
            }

            user.RemoveClaim(claim);
            user.AddClaim(newClaim);

            return Task.FromResult(0);
        }

        public Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            foreach (var claim in claims)
            {
                user.RemoveClaim(claim);
            }

            return Task.FromResult(0);
        }

        public async Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
        {
            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var usersSearch = _context.ScanAsync<TUser>(new List<ScanCondition>
            {
                new ScanCondition("ClaimTypes", ScanOperator.Contains, claim.Type),
                new ScanCondition("ClaimValues", ScanOperator.Contains, claim.Value)
            });
            var users = await usersSearch.GetRemainingAsync(cancellationToken);

            return users.Where(u => u.DeletedOn == default(DateTimeOffset)).ToList();
        }

        public Task SetPasswordHashAsync(TUser user, string passwordHash, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.SetPasswordHash(passwordHash);

            return Task.FromResult(0);
        }

        public Task<string> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.PasswordHash != null);
        }

        public Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (stamp == null)
            {
                throw new ArgumentNullException(nameof(stamp));
            }

            user.SetSecurityStamp(stamp);

            return Task.FromResult(0);
        }

        public Task<string> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.SecurityStamp);
        }

        public Task SetTwoFactorEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (enabled)
            {
                user.EnableTwoFactorAuthentication();
            }
            else
            {
                user.DisableTwoFactorAuthentication();
            }

            return Task.FromResult(0);
        }

        public Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.IsTwoFactorEnabled);
        }

        public Task SetEmailAsync(TUser user, string email, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            user.SetEmail(email);

            return Task.FromResult(0);
        }

        public Task<string> GetEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var email = user.Email?.Value;

            return Task.FromResult(email);
        }

        public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.Email == null)
            {
                throw new InvalidOperationException("Cannot get the confirmation status of the e-mail since the user doesn't have an e-mail.");
            }

            return Task.FromResult(user.Email.IsConfirmed());
        }

        public Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.Email == null)
            {
                throw new InvalidOperationException("Cannot set the confirmation status of the e-mail because user doesn't have an e-mail.");
            }

            if (confirmed)
            {
                user.Email.SetConfirmed();
            }
            else
            {
                user.Email.SetUnconfirmed();
            }

            return Task.FromResult(0);
        }

        public async Task<TUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            if (normalizedEmail == null)
            {
                throw new ArgumentNullException(nameof(normalizedEmail));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var user = await _context.LoadAsync<TUser>(normalizedEmail, default(DateTimeOffset), new DynamoDBOperationConfig
            {
                IndexName = "Email.NormalizedValue-DeletedOn-index"
            }, cancellationToken);
            return user;
        }

        public Task<string> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var normalizedEmail = user.Email?.NormalizedValue;

            return Task.FromResult(normalizedEmail);
        }

        public Task SetNormalizedEmailAsync(TUser user, string normalizedEmail, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            // This method can be called even if user doesn't have an e-mail.
            // Act cool in this case and gracefully handle.
            // More info: https://github.com/aspnet/Identity/issues/645

            if(normalizedEmail != null)
            {
                user.Email?.SetNormalizedEmail(normalizedEmail);
            }

            return Task.FromResult(0);
        }

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var lockoutEndDate = new DateTimeOffset?(user.LockoutEndDate);

            return Task.FromResult(lockoutEndDate);
        }

        public Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(lockoutEnd != null)
            {
                user.LockUntil(lockoutEnd.Value);
            }

            return Task.FromResult(0);
        }

        public async Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var newCount = user.AccessFailedCount + 1;
            user.SetAccessFailedCount(newCount);

            await _context.SaveAsync(user, cancellationToken);

            return newCount;
        }

        public Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.ResetAccessFailedCount();

            return Task.FromResult(0);
        }

        public Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.AccessFailedCount);
        }

        public Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.IsLockoutEnabled);
        }

        public Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (enabled)
            {
                user.EnableLockout();
            }
            else
            {
                user.DisableLockout();
            }

            return Task.FromResult(0);
        }

        public Task SetPhoneNumberAsync(TUser user, string phoneNumber, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (phoneNumber == null)
            {
                throw new ArgumentNullException(nameof(phoneNumber));
            }

            user.SetPhoneNumber(phoneNumber);

            return Task.FromResult(0);
        }

        public Task<string> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.PhoneNumber?.Value);
        }

        public Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.PhoneNumber == null)
            {
                throw new InvalidOperationException("Cannot get the confirmation status of the phone number since the user doesn't have a phone number.");
            }

            return Task.FromResult(user.PhoneNumber.IsConfirmed());
        }

        public Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.PhoneNumber == null)
            {
                throw new InvalidOperationException("Cannot set the confirmation status of the phone number since the user doesn't have a phone number.");
            }

            user.PhoneNumber.SetConfirmed();

            return Task.FromResult(0);
        }

        public void Dispose()
        {
        }

        private async Task EnsureInitializedAsync(IAmazonDynamoDB client, string userTableName)
        {
            var defaultProvisionThroughput = new ProvisionedThroughput
            {
                ReadCapacityUnits = 5,
                WriteCapacityUnits = 5
            };
            var globalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new GlobalSecondaryIndex
                {
                    IndexName = "NormalizedUserName-DeletedOn-index",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement("NormalizedUserName", KeyType.HASH),
                        new KeySchemaElement("DeletedOn", KeyType.RANGE)
                    },
                    ProvisionedThroughput = defaultProvisionThroughput,
                    Projection = new Projection
                    {
                        ProjectionType = ProjectionType.ALL
                    }
                },
                new GlobalSecondaryIndex
                {
                    IndexName = "Email.NormalizedValue-DeletedOn-index",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement("Email.NormalizedValue", KeyType.HASH),
                        new KeySchemaElement("DeletedOn", KeyType.RANGE)
                    },
                    ProvisionedThroughput = defaultProvisionThroughput,
                    Projection = new Projection
                    {
                        ProjectionType = ProjectionType.ALL
                    }
                }
            };

            var tablesResponse = await client.ListTablesAsync();
	        if (tablesResponse.HttpStatusCode != HttpStatusCode.OK)
	        {
		        throw new Exception("Couldn't get list of tables");
	        }
            var tableNames = tablesResponse.TableNames;

            if (!tableNames.Contains(userTableName))
            {
	            await CreateTableAsync(client, userTableName, defaultProvisionThroughput, globalSecondaryIndexes);
	            return;
            }

            var response = await client.DescribeTableAsync(new DescribeTableRequest { TableName = userTableName });
            var table = response.Table;

            var indexesToAdd = globalSecondaryIndexes.Where(g => !table.GlobalSecondaryIndexes.Exists(gd => gd.IndexName.Equals(g.IndexName)));
            var indexUpdates = indexesToAdd.Select(index => new GlobalSecondaryIndexUpdate
            {
                Create = new CreateGlobalSecondaryIndexAction
                {
                    IndexName = index.IndexName, KeySchema = index.KeySchema, ProvisionedThroughput = index.ProvisionedThroughput, Projection = index.Projection
                }
            }).ToList();

            if (indexUpdates.Count > 0)
            {
                await UpdateTableAsync(client, userTableName, indexUpdates);
            }
        }

        private async Task CreateTableAsync(IAmazonDynamoDB client, string userTableName, ProvisionedThroughput provisionedThroughput, List<GlobalSecondaryIndex> globalSecondaryIndexes)
        {
            await client.CreateTableAsync(new CreateTableRequest
            {
                TableName = userTableName,
                ProvisionedThroughput = provisionedThroughput,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = "Id",
                        KeyType = KeyType.HASH
                    },
                    new KeySchemaElement
                    {
                        AttributeName = "DeletedOn",
                        KeyType = KeyType.RANGE
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "Id",
                        AttributeType = ScalarAttributeType.S
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "DeletedOn",
                        AttributeType = ScalarAttributeType.S
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "NormalizedUserName",
                        AttributeType = ScalarAttributeType.S

                    },
                    new AttributeDefinition
                    {
                        AttributeName = "Email.NormalizedValue",
                        AttributeType = ScalarAttributeType.S
                    }
                },
                GlobalSecondaryIndexes = globalSecondaryIndexes
            });

            await WaitForActiveTableAsync(client, userTableName);
        }

        private async Task WaitForActiveTableAsync(IAmazonDynamoDB client, string userTableName)
        {
            do
            {
                var response = await client.DescribeTableAsync(new DescribeTableRequest { TableName = userTableName });

	            if (Equals(response.Table.TableStatus, TableStatus.ACTIVE) &&
	                response.Table.GlobalSecondaryIndexes.TrueForAll(g => Equals(g.IndexStatus, IndexStatus.ACTIVE)))
		            break;

	            Console.WriteLine($"Waiting for the table ${userTableName} to become active and indexes got populated...");
	            await Task.Delay(TimeSpan.FromSeconds(5));
            } while (true);
        }

        private async Task UpdateTableAsync(IAmazonDynamoDB client, string userTableName, List<GlobalSecondaryIndexUpdate> indexUpdates)
        {
            await client.UpdateTableAsync(new UpdateTableRequest
            {
                TableName = userTableName,
                GlobalSecondaryIndexUpdates = indexUpdates
            });

            await WaitForActiveTableAsync(client, userTableName);
        }
    }
}
