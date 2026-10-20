using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomeUtilities.Data;

/// <summary>Used by <c>dotnet ef</c> only; the database file is never committed.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HomeUtilitiesDbContext>
{
    public HomeUtilitiesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HomeUtilitiesDbContext>()
            .UseSqlite("Data Source=design.db")
            .Options;
        return new HomeUtilitiesDbContext(options);
    }
}
