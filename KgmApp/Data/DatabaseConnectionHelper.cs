using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KgmApp.Data;

public static class DatabaseConnectionHelper
{
    /// <summary>
    /// Uses ConnectionStrings:DefaultConnection when set (Npgsql key/value or postgresql:// URI); otherwise DATABASE_URL.
    /// When <see cref="PreferIpv4Host"/> is enabled, resolves the host to a concrete IP address to avoid Windows DNS
    /// lookup mismatches (e.g. "The requested name is valid, but no data of the requested type was found").
    /// </summary>
    public static string ResolveConnectionString(IConfiguration configuration)
    {
        var preferIpv4 = configuration.GetValue("Database:PreferIpv4", true);

        var fromConfig = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            var cs = ConvertDatabaseUrlToNpgsqlConnectionString(fromConfig.Trim());
            return PreferIpv4Host(cs, preferIpv4);
        }

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            var cs = ConvertDatabaseUrlToNpgsqlConnectionString(databaseUrl.Trim());
            return PreferIpv4Host(cs, preferIpv4);
        }

        throw new InvalidOperationException(
            "Database is not configured. Add ConnectionStrings:DefaultConnection to appsettings (or user secrets), or set the DATABASE_URL environment variable.");
    }

    /// <summary>
    /// If <paramref name="enabled"/> is true, replaces <see cref="NpgsqlConnectionStringBuilder.Host"/> with a concrete
    /// address from DNS. It prefers IPv4, but falls back to IPv6 when only AAAA records exist.
    /// This helps with Windows <c>SocketException</c> when hostname resolution for a specific address family fails.
    /// </summary>
    public static string PreferIpv4Host(string connectionString, bool enabled = true)
    {
        if (!enabled || string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        try
        {
            var csb = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(csb.Host))
                return connectionString;

            // Supabase uses host-based routing for multi-tenant Postgres endpoints.
            // Replacing host with a raw IP breaks tenant resolution (e.g. "tenant/user not found").
            if (IsSupabaseHost(csb.Host))
                return connectionString;

            if (IPAddress.TryParse(csb.Host, out _))
                return connectionString;

            IPAddress[] addresses;
            try
            {
                addresses = Dns.GetHostAddresses(csb.Host);
            }
            catch
            {
                return connectionString;
            }

            var preferred =
                addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);

            if (preferred == null)
                return connectionString;

            csb.Host = preferred.ToString();
            return csb.ConnectionString;
        }
        catch
        {
            return connectionString;
        }
    }

    private static bool IsSupabaseHost(string host)
    {
        return host.EndsWith(".supabase.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Accepts postgres:// or postgresql:// URLs, or a plain Npgsql connection string.
    /// </summary>
    public static string ConvertDatabaseUrlToNpgsqlConnectionString(string databaseUrl)
    {
        if (!databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return databaseUrl;
        }

        var uri = new Uri(databaseUrl);
        var userInfoParts = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfoParts[0]);
        var password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty;

        var database = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(database))
            throw new InvalidOperationException("The PostgreSQL URI must include a database name in the path (e.g. /postgres).");

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = username,
            Password = password,
            Database = database
        };

        if (!string.IsNullOrEmpty(uri.Query))
        {
            var query = uri.Query.TrimStart('?');
            foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = segment.IndexOf('=');
                if (eq <= 0) continue;
                var key = Uri.UnescapeDataString(segment[..eq]);
                var value = Uri.UnescapeDataString(segment[(eq + 1)..]);
                try
                {
                    csb[key] = value;
                }
                catch
                {
                    // Ignore keys Npgsql does not recognize
                }
            }
        }

        return csb.ConnectionString;
    }
}
