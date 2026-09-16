using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;
using MoneyTalk.Data;
using MoneyTalk.Data.Repositories;
using MoneyTalk.Data.Seed;

namespace MoneyTalk.Tests;

/// <summary>Spins up a fresh, isolated SQLite database held open in memory for the lifetime of a
/// single test via a dedicated connection (SQLite's <c>:memory:</c> database is destroyed the
/// moment its one connection closes, so keeping the connection alive is what makes this act like
/// a real, disposable per-test database rather than the EF InMemory provider — which skips
/// relational behaviors like unique-key or precision handling this app actually relies on).</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MoneyTalkDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new MoneyTalkDbContext(options);
        context.Database.EnsureCreated();

        _options = options;
    }

    private readonly DbContextOptions<MoneyTalkDbContext> _options;

    public IUnitOfWork NewUnitOfWork() => new EfUnitOfWork(new MoneyTalkDbContext(_options));

    public async Task<Company> CreateCompanyAsync(string name = "Test Co")
    {
        using var uow = NewUnitOfWork();
        return await DataSeeder.CreateCompanyWithDefaultsAsync(uow, name);
    }

    public void Dispose() => _connection.Dispose();
}
