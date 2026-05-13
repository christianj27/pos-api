using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pos.Api.Data;

namespace Pos.Test.Helpers;

/// <summary>
/// Creates a fresh in-memory AppDbContext for each test instance.
/// Always pass a unique name (e.g. Guid.NewGuid().ToString()) so tests never share state.
/// </summary>
public static class DbContextFactory
{
    public static AppDbContext Create(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
