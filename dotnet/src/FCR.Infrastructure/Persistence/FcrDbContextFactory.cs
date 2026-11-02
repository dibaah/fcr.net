using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FCR.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef` works without the Web host.</summary>
public sealed class FcrDbContextFactory : IDesignTimeDbContextFactory<FcrDbContext>
{
    public FcrDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FcrDbContext>()
            .UseSqlite("Data Source=fcr_database.db")
            .Options;
        return new FcrDbContext(options);
    }
}
