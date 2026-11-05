using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using Npgsql;

namespace MoissonMontreal.Tests.Api;

internal static class PostgresConstraintAssertions
{
    public static async Task AssertViolatesConstraintAsync(MoissonDbContext context, string constraintName)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(constraintName, postgresException.ConstraintName);
    }
}
