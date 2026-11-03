using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data;

public class MoissonDbContext(DbContextOptions<MoissonDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoissonDbContext).Assembly);
    }
}