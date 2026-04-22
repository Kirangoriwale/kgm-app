using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KgmApp.Data;

/// <summary>
/// Enables EF Core tools (migrations, scaffold) by loading appsettings and environment variables the same way as at runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var basePath = ResolveContentRoot();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(DatabaseConnectionHelper.ResolveConnectionString(configuration));

        return new AppDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Finds the folder that contains appsettings.json. Design-time tools may run with a current
    /// directory that is not the project folder; the compiled output lives under bin/Debug/net8.0.
    /// </summary>
    private static string ResolveContentRoot()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(AppDbContextFactory).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            var fromBuildOutput = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
            if (File.Exists(Path.Combine(fromBuildOutput, "appsettings.json")))
                return fromBuildOutput;
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 12 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "appsettings.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
