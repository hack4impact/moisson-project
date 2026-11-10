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
    /// Donors stored in the database.
    /// </summary>
    public DbSet<Donor> Donors => Set<Donor>();

    /// <summary>
    /// Food items stored in the database.
    /// </summary>
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();

    /// <summary>
    /// Tax receipts given to donors.
    /// </summary>
    public DbSet<Receipt> Receipts => Set<Receipt>();

    /// <summary>
    /// Food items on each receipt, with the price used to value them.
    /// </summary>
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();

    /// <summary>
    /// Types of food recognized by the app, which the price grid gives a price to.
    /// </summary>
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    /// <summary>
    /// Price history of each catalog item, one row per effective date.
    /// </summary>
    public DbSet<Price> Prices => Set<Price>();

    /// <summary>
    /// Names every table, column, key and index in snake_case.
    /// </summary>
    /// <param name="optionsBuilder">Builder used to configure the context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    /// <summary>
    /// Converts every date and time to UTC before it is written, so a value such as DateTimeOffset.Now can be saved.
    /// </summary>
    /// <param name="configurationBuilder">Builder used to configure the conventions of the model.</param>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
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
