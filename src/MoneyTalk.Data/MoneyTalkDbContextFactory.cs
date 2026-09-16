using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MoneyTalk.Data;

/// <summary>Lets `dotnet ef migrations add ...` construct a context at design time without
/// needing the WinUI app (and its Windows-only Windows App SDK dependency) to build first. Run
/// from the repo root: `dotnet ef migrations add InitialCreate -p src/MoneyTalk.Data -s src/MoneyTalk.Data`.</summary>
public class MoneyTalkDbContextFactory : IDesignTimeDbContextFactory<MoneyTalkDbContext>
{
    public MoneyTalkDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MoneyTalkDbContext>();
        optionsBuilder.UseSqlite("Data Source=moneytalk.design.db");
        return new MoneyTalkDbContext(optionsBuilder.Options);
    }
}
