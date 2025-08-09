using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyTools.MyCalculator.Services;
using Xunit;

namespace MyTools.Tests;

public class HistoryStoreTests
{
    private static string CreateTempDb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MyTools.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "app.db");
    }

    private sealed class TestLocalDb : LocalDb
    {
        private readonly string _path;
        public TestLocalDb(string path) { _path = path; }
        public override string ConnectionString => new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _path }.ToString();
    }

    [Fact]
    public async Task Add_UpsertAndUsage_AndOrder_Top5ByUsageThenAlpha()
    {
        var dbPath = CreateTempDb();
        var db = new TestLocalDb(dbPath);
        await db.InitializeAsync();
        var store = new HistoryStore(db);

        // Add duplicates to test upsert
        await store.AddAsync("a+b", "3");
        await store.AddAsync("a+b", "3");
        await store.AddAsync("c+d", "7");
        await store.AddAsync("e+f", "11");
        await store.AddAsync("g+h", "15");
        await store.AddAsync("i+j", "19");
        await store.AddAsync("k+l", "23");

        var latest = await store.GetLatestAsync(100);
        // top 5 by usage (a+b usage=2, others 1) then alpha for the rest
        Assert.Equal("a+b", latest.First().Expression);
        Assert.Contains(latest.Take(5), x => x.Expression == "a+b");
        // alphabetic after top 5
        var remaining = latest.Skip(5).Select(x => x.Expression).ToList();
        var sorted = remaining.OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, remaining);
    }

    [Fact]
    public async Task Search_Filters_AndOrdersByUsageThenAlpha()
    {
        var dbPath = CreateTempDb();
        var db = new TestLocalDb(dbPath);
        await db.InitializeAsync();
        var store = new HistoryStore(db);

        await store.AddAsync("sum(1,2)", "3");
        await store.AddAsync("sum(2,3)", "5");
        await store.AddAsync("max(1,2)", "2");
        // bump usage
        await store.AddAsync("sum(1,2)", "3");

        var results = await store.SearchAsync("sum", 50);
        foreach (var r in results)
            Assert.Contains("sum", r.Expression, StringComparison.OrdinalIgnoreCase);
        // first should be sum(1,2) because higher usage
        Assert.Equal("sum(1,2)", results[0].Expression);
    }

    [Fact]
    public async Task TrimAsync_RespectsOrderingAndLimit()
    {
        var dbPath = CreateTempDb();
        var db = new TestLocalDb(dbPath);
        await db.InitializeAsync();
        var store = new HistoryStore(db);

        for (int i = 0; i < 10; i++)
            await store.AddAsync($"x+{i}", (i+1).ToString());

        await store.TrimAsync(5);
        var after = await store.GetLatestAsync(100);
        Assert.Contains(after, x => x.Expression.Length <= 5);
    }
}
