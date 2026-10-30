using Microsoft.EntityFrameworkCore;

namespace MoissonMontreal.Api.Data;

public class MoissonDbContext(DbContextOptions<MoissonDbContext> options) : DbContext(options)
{
    // Les tables (DbSet<Don>, DbSet<Recu>, ...) seront ajoutées avec l'issue du schéma.
}
