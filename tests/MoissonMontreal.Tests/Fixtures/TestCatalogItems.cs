using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Tests.Fixtures;

/// <summary>
/// Builds catalog items that pass the database rules: every field filled, a unique Business Central code
/// and a quality class created by the migrations.
/// </summary>
public static class TestCatalogItems
{
    public static CatalogItem Create(string name) => new()
    {
        Name = name,
        Category = "Boulangerie",
        BusinessCentralCode = "BC-" + Guid.NewGuid().ToString("N")[..12],
        Unit = "kg",
        QualityClassCode = "A",
    };
}
