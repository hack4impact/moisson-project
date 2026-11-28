using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Data.Configurations;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class DonorProfileMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string MigrationBeforeDonorProfile = "20261127053347_AddReceiptNumbers";

    [Fact]
    public async Task Migrate_KeepsExistingDonors_AndMakesThemOrganizations()
    {
        await using var context = CreateContext("donor_profile_upgrade");
        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeDonorProfile);
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO donors (id, name, address, postal_code, contact_person) VALUES
              ('00000000-0000-0000-0000-0000000000d1', 'Boulangerie Saint-Laurent', '123 rue Saint-Laurent', 'H2X 1Y4', 'Marie Tremblay'),
              ('00000000-0000-0000-0000-0000000000d2', 'Ferme des Îles', NULL, NULL, NULL);
            """);

        await context.Database.MigrateAsync();

        var donors = await context.Donors.OrderBy(d => d.Name).ToListAsync();
        Assert.Equal(["Boulangerie Saint-Laurent", "Ferme des Îles"], donors.Select(d => d.Name));
        Assert.All(donors, donor =>
        {
            Assert.Equal(DonorTypes.Organization, donor.Type);
            Assert.Null(donor.City);
            Assert.Null(donor.Province);
            Assert.Null(donor.Email);
            Assert.Null(donor.Phone);
            Assert.Null(donor.Notes);
            Assert.Null(donor.BusinessCentralCode);
            Assert.Null(donor.UpdatedAt);
        });
        Assert.Equal("123 rue Saint-Laurent", donors[0].Address);
        Assert.Equal("H2X 1Y4", donors[0].PostalCode);
        Assert.Equal("Marie Tremblay", donors[0].ContactPerson);
    }

    [Fact]
    public async Task Migrate_RemovesTheTypeDefault_SoANewDonorMustStateItsType()
    {
        await using var context = CreateContext("donor_profile_no_default");
        await context.Database.MigrateAsync();

        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync("INSERT INTO donors (name) VALUES ('Sans type');"));

        Assert.Equal(Npgsql.PostgresErrorCodes.NotNullViolation, exception.SqlState);
        Assert.Equal("type", exception.ColumnName);
    }

    [Fact]
    public async Task Donor_StoresTheFullProfile()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var code = UniqueCode();
        var donor = Donor(code: code);
        donor.Type = DonorTypes.Individual;
        donor.Email = "marie@exemple.ca";
        donor.Phone = "514-555-0100";
        donor.Notes = "Donne chaque automne";
        donor.UpdatedAt = new DateTimeOffset(2026, 11, 18, 9, 30, 0, TimeSpan.FromHours(-5));
        context.Donors.Add(donor);
        await context.SaveChangesAsync();

        await using var reader = CreateContext();
        var saved = await reader.Donors.SingleAsync(d => d.Id == donor.Id);

        Assert.Equal("Marie Tremblay", saved.Name);
        Assert.Equal(DonorTypes.Individual, saved.Type);
        Assert.Equal("123 rue Saint-Laurent", saved.Address);
        Assert.Equal("Montréal", saved.City);
        Assert.Equal("QC", saved.Province);
        Assert.Equal("H2X 1Y4", saved.PostalCode);
        Assert.Equal("marie@exemple.ca", saved.Email);
        Assert.Equal("514-555-0100", saved.Phone);
        Assert.Equal("Donne chaque automne", saved.Notes);
        Assert.Equal(code, saved.BusinessCentralCode);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 14, 30, 0, TimeSpan.Zero), saved.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, saved.UpdatedAt!.Value.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("company")]
    [InlineData("Organization")]
    [InlineData("INDIVIDUAL")]
    [InlineData(" individual")]
    public async Task SaveChanges_Throws_WhenTypeIsNotIndividualOrOrganization(string type)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var donor = Donor();
        donor.Type = type;
        context.Donors.Add(donor);

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_donors_type");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenBusinessCentralCodeIsEmpty()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.Donors.Add(Donor(code: ""));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_donors_business_central_code_not_empty");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenBusinessCentralCodeIsDuplicated()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var code = UniqueCode();
        context.Donors.Add(Donor(code: code));
        await context.SaveChangesAsync();

        await using var second = CreateContext();
        second.Donors.Add(Donor(code: code));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(second, DonorConfiguration.BusinessCentralCodeIndex);
    }

    [Fact]
    public async Task SaveChanges_AcceptsManyDonorsWithoutBusinessCentralCode()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.Donors.AddRange(Donor(code: null), Donor(code: null));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task RollingBack_RemovesTheProfileColumns_AndKeepsTheDonors()
    {
        await using var context = CreateContext("donor_profile_rollback");
        await context.Database.MigrateAsync();
        context.Donors.Add(Donor(code: "C-ROLLBACK"));
        await context.SaveChangesAsync();

        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeDonorProfile);

        var columns = await ColumnsAsync(context, "donors");
        Assert.Equal(["address", "contact_person", "created_at", "id", "name", "postal_code"], columns);
        var names = await context.Database.SqlQueryRaw<string>("SELECT name AS \"Value\" FROM donors").ToListAsync();
        Assert.Equal(["Marie Tremblay"], names);

        await context.Database.MigrateAsync();
        await using var reader = CreateContext("donor_profile_rollback");
        var donor = await reader.Donors.SingleAsync();
        Assert.Equal(DonorTypes.Organization, donor.Type);
        Assert.Null(donor.BusinessCentralCode);
    }

    private static async Task<List<string>> ColumnsAsync(MoissonDbContext context, string table) =>
        await context.Database
            .SqlQueryRaw<string>(
                "SELECT column_name::text AS \"Value\" FROM information_schema.columns WHERE table_name = {0} ORDER BY column_name",
                table)
            .ToListAsync();

    private static string UniqueCode() => "C-" + Guid.NewGuid().ToString("N")[..12];

    private static Donor Donor(string? code = null) => new()
    {
        Name = "Marie Tremblay",
        Address = "123 rue Saint-Laurent",
        City = "Montréal",
        Province = "QC",
        PostalCode = "H2X 1Y4",
        BusinessCentralCode = code,
    };

    private MoissonDbContext CreateContext(string? database = null)
    {
        var connectionString = postgres.ConnectionString;
        if (database is not null)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            builder["Database"] = database;
            connectionString = builder.ConnectionString;
        }

        return new MoissonDbContext(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(connectionString).Options);
    }
}
