using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Pos.Api.Data;
using Pos.Api.Services.Implementations;
using Pos.Api.Services.Interfaces;

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

    /// <summary>
    /// Builds the real settlement guard. Enforcement is off unless <paramref name="enforcementStartDate"/> is
    /// supplied, so the existing service tests keep exercising the services exactly as before.
    /// </summary>
    public static ISettlementGuard CreateGuard(AppDbContext db, DateOnly? enforcementStartDate = null)
    {
        var settings = new Dictionary<string, string?>();

        if (enforcementStartDate.HasValue)
            settings["Settlement:EnforcementStartDate"] = enforcementStartDate.Value.ToString("yyyy-MM-dd");

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new SettlementGuard(db, config);
    }
}
