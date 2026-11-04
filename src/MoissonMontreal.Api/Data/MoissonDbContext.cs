using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data;

/// <summary>
/// Database session for the Moisson Montréal API.
/// </summary>
public class MoissonDbContext(DbContextOptions<MoissonDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Donors stored in the database.
    /// </summary>
    public DbSet<Donor> Donors => Set<Donor>();

    /// <summary>
    /// Food items stored in the database.
    /// </summary>
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoissonDbContext).Assembly);
    }
}
