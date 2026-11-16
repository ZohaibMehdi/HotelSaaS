using System.Reflection;
using FluentValidation;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Application.Security;
using HotelSaaS.Infrastructure.Notifications;
using HotelSaaS.Infrastructure.Persistence;
using HotelSaaS.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotelSaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ApplicationDbContext>(opt =>
        {
            var cs = BuildConnectionString(config);
            opt.UseSqlServer(cs, sql =>
            {
                sql.CommandTimeout(60);
                // NOTE: EnableRetryOnFailure is intentionally OFF - the execution
                // strategy rejects user-initiated transactions, and serializable
                // booking transactions must not auto-replay (PRD section 43).
            });
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<DataSeeder>();
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<ICurrentUserService>());
        return services;
    }

    /// <summary>Registers Application-layer services and validators via convention.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var appAssembly = Assembly.Load("HotelSaaS.Application");
        services.AddValidatorsFromAssembly(appAssembly);

        var serviceTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service") &&
                        t.Namespace!.StartsWith("HotelSaaS.Application.Services"))
            .ToList();

        foreach (var impl in serviceTypes)
        {
            // Register concrete type first so cross-service dependencies resolve.
            services.AddScoped(impl);
            var iface = impl.GetInterfaces().FirstOrDefault(i =>
                i.IsInterface && i.Name == $"I{impl.Name}");
            if (iface != null)
                services.AddScoped(iface, sp => sp.GetRequiredService(impl));
        }
        return services;
    }

    private static string BuildConnectionString(IConfiguration config)
    {
        var cs = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cs)) return cs;

        // Sensible local default: Windows-authenticated local SQL Server instance.
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = "localhost",
            InitialCatalog = "HotelSaaS_DB",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = false,
            MultipleActiveResultSets = false
        };
        return builder.ConnectionString;
    }
}

