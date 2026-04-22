using KgmApp.Data;
using KgmApp.Filters;
using KgmApp.Models;
using KgmApp.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KgmApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddScoped<IMenuPermissionService, MenuPermissionService>();
            builder.Services.AddScoped<MemberContributionSummaryService>();
            builder.Services.Configure<MemberUpiPaymentOptions>(
                builder.Configuration.GetSection(MemberUpiPaymentOptions.SectionName));
            builder.Services.AddScoped<MenuPermissionAuthorizationFilter>();
            builder.Services.AddScoped<MenuPermissionViewDataFilter>();
            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.AddService<MenuPermissionAuthorizationFilter>();
                options.Filters.AddService<MenuPermissionViewDataFilter>();
            });
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(DatabaseConnectionHelper.ResolveConnectionString(builder.Configuration)));

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                // Required behind reverse proxies (Render) so HTTPS redirect and scheme detection work.
                var forwardedHeadersOptions = new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                };
                forwardedHeadersOptions.KnownNetworks.Clear();
                forwardedHeadersOptions.KnownProxies.Clear();
                app.UseForwardedHeaders(forwardedHeadersOptions);
            }

            // Apply pending EF Core migrations when the database is reachable.
            // In Development, start the web server even if PostgreSQL is down so localhost is not refused.
            using (var scope = app.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                logger.LogInformation("Database: checking connection and migrations…");

                if (await db.Database.CanConnectAsync())
                {
                    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
                    if (pending.Count > 0)
                    {
                        logger.LogInformation("Applying migrations: {Migrations}", string.Join(", ", pending));
                    }
                    else
                    {
                        logger.LogInformation("No pending migrations.");
                    }

                    await db.Database.MigrateAsync();
                    await RoleMenuPermissionSeeder.SeedAsync(db);

                    var applied = await db.Database.GetAppliedMigrationsAsync();
                    logger.LogInformation("Applied migrations: {Migrations}", string.Join(", ", applied));
                }
                else
                {
                    var message =
                        "Cannot connect to PostgreSQL. Check ConnectionStrings:DefaultConnection, host/firewall (Supabase: direct db host port 5432), and credentials.";

                    if (app.Environment.IsDevelopment())
                    {
                        logger.LogWarning(
                            "{Message} Starting without migrations; Member/DB pages will fail until the database is available.",
                            message);
                    }
                    else
                    {
                        logger.LogError(message);
                        throw new InvalidOperationException("Cannot connect to the database. See logs.");
                    }
                }
            }

            if (app.Environment.IsDevelopment())
            {
                app.MapGet("/debug/db", async (AppDbContext db, IConfiguration configuration) =>
                {
                    if (!await db.Database.CanConnectAsync())
                    {
                        string? connectError = null;
                        string? connectErrorType = null;
                        var probeConn = db.Database.GetDbConnection();
                        try
                        {
                            await probeConn.OpenAsync();
                        }
                        catch (Exception ex)
                        {
                            connectError = ex.Message;
                            connectErrorType = ex.GetType().FullName;
                        }
                        finally
                        {
                            if (probeConn.State != System.Data.ConnectionState.Closed)
                                await probeConn.CloseAsync();
                        }

                        return Results.Json(new
                        {
                            defaultConnectionSet = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")),
                            databaseUrlSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL")),
                            canConnect = false,
                            error = "Cannot connect to PostgreSQL.",
                            errorType = connectErrorType,
                            errorDetail = connectError
                        });
                    }

                    var applied = await db.Database.GetAppliedMigrationsAsync();
                    var pending = await db.Database.GetPendingMigrationsAsync();
                    var tables = new List<string>();
                    var conn = db.Database.GetDbConnection();
                    await conn.OpenAsync();
                    try
                    {
                        await using var cmd = conn.CreateCommand();
                        cmd.CommandText =
                            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY 1";
                        await using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                            tables.Add(reader.GetString(0));
                    }
                    finally
                    {
                        await conn.CloseAsync();
                    }

                    return Results.Json(new
                    {
                        defaultConnectionSet = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")),
                        databaseUrlSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL")),
                        canConnect = true,
                        appliedMigrations = applied.ToArray(),
                        pendingMigrations = pending.ToArray(),
                        publicSchemaTables = tables
                    });
                });
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseSession();

            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
                var isLoginPath = path == "/account/login";
                var isLogoutPath = path == "/account/logout";
                var isChangePasswordPath = path == "/account/changepassword";
                var isDebugPath = path.StartsWith("/debug/db");
                var isStaticAsset =
                    path.StartsWith("/lib/") ||
                    path.StartsWith("/css/") ||
                    path.StartsWith("/js/") ||
                    path.StartsWith("/images/") ||
                    path == "/favicon.ico" ||
                    Path.HasExtension(path);

                if (isLoginPath || isLogoutPath || isChangePasswordPath || isDebugPath || isStaticAsset)
                {
                    await next();
                    return;
                }

                var username = context.Session.GetString("Username");
                if (string.IsNullOrWhiteSpace(username))
                {
                    context.Response.Redirect("/Account/Login");
                    return;
                }

                var mustChange = context.Session.GetString("MustChangePassword");
                if (string.Equals(mustChange, "true", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("/Account/ChangePassword");
                    return;
                }

                await next();
            });

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            await app.RunAsync();
        }
    }
}
