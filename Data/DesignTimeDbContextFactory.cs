using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Data;

// Script generation must not depend on application startup, secrets or a live database.
// A database update requires an explicit --connection supplied by an authorized operator.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer().Options);
}
