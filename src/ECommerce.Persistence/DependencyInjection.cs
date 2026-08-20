using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Persistence.Context;
using ECommerce.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        // Pinned MySQL version — change here if you upgrade your MySQL server.
        // Using a pinned version avoids the runtime AutoDetect ping which fails
        // when the database is briefly unreachable (e.g. during EF tooling commands).
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(connectionString, serverVersion, mysql =>
            {
                mysql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                // NOTE: EnableRetryOnFailure conflicts with user-initiated transactions
                // (e.g. OrderService.CreateAsync). For local dev we skip it. For production,
                // re-enable it and wrap transactional code in
                // ctx.Database.CreateExecutionStrategy().ExecuteAsync(...).
            }));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
