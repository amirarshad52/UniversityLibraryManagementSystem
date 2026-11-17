using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySql.Data.EntityFramework;
using ULMS.Web.Data;
using ULMS.Web.Services;

namespace ULMS.Web
{
    public class Startup
    {
        private const string ExternalCookieScheme = "ULMS.External";
        private const string AdminEmail = "amir522289@gmail.com";
        private const int ApprovedAdminUserId = 1;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            DbConfiguration.SetConfiguration(
                new ULMSMySqlEFConfiguration()
            );
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            services.AddScoped<ULMSDbContext>(serviceProvider =>
                new ULMSDbContext(
                    Configuration.GetConnectionString("ULMSConnection")
                )
            );

            services.AddScoped<BorrowingService>();

            services
                .AddAuthentication(
                    CookieAuthenticationDefaults.AuthenticationScheme
                )
                .AddCookie(options =>
                {
                    options.Cookie.Name = "ULMS.Application";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy =
                        CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Lax;

                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";

                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;

                    options.Events = new CookieAuthenticationEvents
                    {
                        OnValidatePrincipal = async context =>
                        {
                            var principal = context.Principal;

                            if (principal?.Identity?.IsAuthenticated != true)
                            {
                                await RejectApplicationCookie(context);
                                return;
                            }

                            string userIdValue = principal
                                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                            string role = principal
                                .FindFirst(ClaimTypes.Role)?.Value;

                            string googleSubject = principal
                                .FindFirst("ulms:google_sub")?.Value;

                            string email = principal
                                .FindFirst(ClaimTypes.Email)?.Value;

                            int userId;

                            if (!int.TryParse(userIdValue, out userId) ||
                                userId <= 0 ||
                                string.IsNullOrWhiteSpace(role) ||
                                string.IsNullOrWhiteSpace(googleSubject) ||
                                string.IsNullOrWhiteSpace(email))
                            {
                                await RejectApplicationCookie(context);
                                return;
                            }

                            var db = context.HttpContext.RequestServices
                                .GetRequiredService<ULMSDbContext>();

                            var account = db.UserAccounts
                                .AsNoTracking()
                                .FirstOrDefault(x =>
                                    x.UserId == userId &&
                                    x.Role == role &&
                                    x.IsActive &&
                                    x.GoogleSubject == googleSubject);

                            if (account == null ||
                                !string.Equals(
                                    account.Email,
                                    email,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                await RejectApplicationCookie(context);
                                return;
                            }

                            bool profileIsValid = false;

                            if (role == "Admin")
                            {
                                int adminCount = db.UserAccounts
                                    .AsNoTracking()
                                    .Count(x => x.Role == "Admin");

                                profileIsValid =
                                    adminCount == 1 &&
                                    account.UserId == ApprovedAdminUserId &&
                                    string.Equals(
                                        account.Email,
                                        AdminEmail,
                                        StringComparison.OrdinalIgnoreCase
                                    );
                            }
                            else if (role == "Staff")
                            {
                                profileIsValid = db.StaffProfiles
                                    .AsNoTracking()
                                    .Any(x =>
                                        x.UserId == userId &&
                                        x.Library != null);
                            }
                            else if (role == "Student")
                            {
                                profileIsValid = db.StudentProfiles
                                    .AsNoTracking()
                                    .Any(x =>
                                        x.UserId == userId);
                            }

                            if (!profileIsValid)
                            {
                                await RejectApplicationCookie(context);
                            }
                        }
                    };
                })
                .AddCookie(ExternalCookieScheme, options =>
                {
                    options.Cookie.Name = "ULMS.External";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy =
                        CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Lax;

                    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                    options.SlidingExpiration = false;
                });

            // Google Authentication configuration.
            string googleClientId =
                Configuration["Authentication:Google:ClientId"];

            string googleClientSecret =
                Configuration["Authentication:Google:ClientSecret"];

            if (!string.IsNullOrWhiteSpace(googleClientId) &&
                !string.IsNullOrWhiteSpace(googleClientSecret))
            {
                services.AddAuthentication()
                    .AddGoogle(options =>
                    {
                        options.ClientId = googleClientId;
                        options.ClientSecret = googleClientSecret;

                        options.SignInScheme = ExternalCookieScheme;
                        options.CallbackPath = "/signin-google";
                        options.SaveTokens = false;

                        options.ClaimActions.MapJsonKey(
                            "email_verified",
                            "verified_email"
                        );
                    });
            }

            // Role-based authorization policies.
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("Admin");
                });

                options.AddPolicy("StaffOnly", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("Staff");
                });

                options.AddPolicy("StudentOnly", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("Student");
                });
            });
        }

        private static async Task RejectApplicationCookie(
            CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}"
                );
            });
        }
    }
}