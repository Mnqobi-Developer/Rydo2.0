using Microsoft.EntityFrameworkCore;

namespace Rydo.Infrastructure.Persistence;

public sealed class RydoDbContext(DbContextOptions<RydoDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RydoDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
