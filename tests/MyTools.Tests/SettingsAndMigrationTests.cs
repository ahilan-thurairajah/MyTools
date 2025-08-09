using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MyTools.MyCalculator.Services;
using Xunit;

namespace MyTools.Tests;

public class SettingsAndMigrationTests
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
        public override string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _path }.ToString();
    }

    [Fact]
    public async Task SettingsStore_RoundTrip_Works()
    {
        var dbPath = CreateTempDb();
        var db = new TestLocalDb(dbPath);
        await db.InitializeAsync();
        var settings = new SettingsStore(db);

        await settings.SetAsync("Theme", "Themes/Dark.xaml");
        await settings.SetAsync("Size", "Sizes/Large.xaml");
        await settings.SetAsync("Mode", "Scientific");
        await settings.SetAsync("HistoryLimit", "123");

        Assert.Equal("Themes/Dark.xaml", await settings.GetAsync("Theme"));
        Assert.Equal("Sizes/Large.xaml", await settings.GetAsync("Size"));
        Assert.Equal("Scientific", await settings.GetAsync("Mode"));
        Assert.Equal("123", await settings.GetAsync("HistoryLimit"));
    }

    [Fact]
    public async Task LocalDb_Migration_Deduplicates_History_ByExpression()
    {
        var dbPath = CreateTempDb();
        // Create DB with duplicates manually
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        await using (var conn = new SqliteConnection(cs))
        {
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE History (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Expression TEXT NOT NULL,
                Result TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL
            );";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "INSERT INTO History(Expression,Result,CreatedUtc) VALUES ('a+b','3',datetime('now')),('a+b','3',datetime('now'))";
            await cmd.ExecuteNonQueryAsync();
        }

        // Run initializer which should deduplicate and then add indexes/columns
        var db = new TestLocalDb(dbPath);
        await db.InitializeAsync();

        await using (var conn = new SqliteConnection(db.ConnectionString))
        {
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM History";
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);

            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='UX_History_Expression'";
            var idxName = (string?)await cmd.ExecuteScalarAsync();
            Assert.Equal("UX_History_Expression", idxName);
        }
    }
}
