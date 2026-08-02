using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Api.Health;

public sealed class DatabaseHealthCheck(RydoDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await database.Database.OpenConnectionAsync(cancellationToken);
            await database.Database.CloseConnectionAsync();

            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL is not reachable.",
                exception);
        }
    }
}
