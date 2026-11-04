using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data;

/// <summary>
/// Entity Framework entry point to the PostgreSQL database.
/// </summary>
/// <param name="options">Options that set the database provider and connection string.</param>
public class MoissonDbContext(DbContextOptions<MoissonDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Employees allowed to use the app.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Department-based roles of the app.
    /// </summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    /// Names every table, column, key and index in snake_case.
    /// </summary>
    /// <param name="optionsBuilder">Builder used to configure the context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    /// <summary>
    /// Applies every table configuration found in the Configurations folder.
    /// </summary>
    /// <param name="modelBuilder">Builder used to configure the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoissonDbContext).Assembly);
    }
}