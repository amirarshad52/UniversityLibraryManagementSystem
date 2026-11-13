using System;
using System.Data.Entity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySql.Data.EntityFramework;
using ULMS.Web.Data;
using ULMS.Web.Services;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ULMS.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            DbConfiguration.SetConfiguration(
                new MySqlEFConfiguration()
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

            // Application Cookie Authentication
            services.AddAuthentication(
                CookieAuthenticationDefaults.AuthenticationScheme
            )
            .AddCookie(options =>
            {
                options.Cookie.Name = "ULMS.Application";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;

                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";

                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = async context =>
                    {
                        // Only check Staff accounts.
                        if (context.Principal?.IsInRole("Staff") != true)
                            return;

                        string userIdValue = context.Principal
                            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                        int userId;

                        if (!int.TryParse(userIdValue, out userId))
                        {
                            context.RejectPrincipal();

                            await context.HttpContext.SignOutAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme);

                            return;
                        }

                        var db = context.HttpContext.RequestServices
                            .GetRequiredService<ULMSDbContext>();

                        bool staffIsActive = db.UserAccounts.Any(x =>
                            x.UserId == userId &&
                            x.Role == "Staff" &&
                            x.IsActive);

                        if (!staffIsActive)
                        {
                            context.RejectPrincipal();

                            await context.HttpContext.SignOutAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    }
                };
            })
            .AddCookie("ULMS.External", options =>
            {
                options.Cookie.Name = "ULMS.External";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;

                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = false;
            });

            // Google Authentication
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

                        options.SignInScheme = "ULMS.External";
                        options.CallbackPath = "/signin-google";
                        options.SaveTokens = false;
                    });
            }

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
            });
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