using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MyTools.MyCalculator.Services;

public class LocalDb
{
    private readonly string _dbPath;

    public LocalDb()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyTools", "MyCalculator");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "app.db");
    }

    public virtual string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();

    public async Task InitializeAsync()
    {
    await using var conn = new SqliteConnection(ConnectionString);
    await conn.OpenAsync();
    var cmd = conn.CreateCommand();
        cmd.CommandText = @"PRAGMA journal_mode=WAL;";
        await cmd.ExecuteNonQueryAsync();

        // Ensure Settings table
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Settings (
            Key TEXT PRIMARY KEY,
            Value TEXT
        );";
        await cmd.ExecuteNonQueryAsync();

    // Ensure History table exists (with latest columns if new DB)
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS History (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Expression TEXT NOT NULL,
            Result TEXT NOT NULL,
            CreatedUtc TEXT NOT NULL,
            LastUsedUtc TEXT,
            UsageCount INTEGER NOT NULL DEFAULT 0
        );";
        await cmd.ExecuteNonQueryAsync();

        // Migrate existing DBs to add new columns if missing
        // Add LastUsedUtc
        cmd.CommandText = "PRAGMA table_info(History);";
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            bool hasLastUsed = false, hasUsage = false;
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(1);
                if (string.Equals(name, "LastUsedUtc", StringComparison.OrdinalIgnoreCase)) hasLastUsed = true;
                if (string.Equals(name, "UsageCount", StringComparison.OrdinalIgnoreCase)) hasUsage = true;
            }
            reader.Close();

            if (!hasLastUsed)
            {
                cmd.CommandText = "ALTER TABLE History ADD COLUMN LastUsedUtc TEXT";
                await cmd.ExecuteNonQueryAsync();
            }
            if (!hasUsage)
            {
                cmd.CommandText = "ALTER TABLE History ADD COLUMN UsageCount INTEGER NOT NULL DEFAULT 0";
                await cmd.ExecuteNonQueryAsync();
            }

            // Initialize new columns where needed
            cmd.CommandText = @"UPDATE History
                                SET UsageCount = CASE WHEN UsageCount IS NULL OR UsageCount = 0 THEN 1 ELSE UsageCount END,
                                    LastUsedUtc = COALESCE(LastUsedUtc, CreatedUtc)";
            await cmd.ExecuteNonQueryAsync();
        }

        // Deduplicate any existing rows by Expression before creating unique index
        // 1) Update the keeper row (min rowid) with aggregated values
        cmd.CommandText = @"
            WITH keep AS (
                SELECT MIN(rowid) AS rowid, Expression
                FROM History
                GROUP BY Expression
            ), agg AS (
                SELECT Expression,
                       SUM(UsageCount) AS TotalUsage,
                       MAX(COALESCE(LastUsedUtc, CreatedUtc)) AS MaxLastUsedUtc
                FROM History
                GROUP BY Expression
            )
            UPDATE History
            SET UsageCount = (SELECT TotalUsage FROM agg WHERE agg.Expression = History.Expression),
                LastUsedUtc = (SELECT MaxLastUsedUtc FROM agg WHERE agg.Expression = History.Expression)
            WHERE rowid IN (SELECT rowid FROM keep);
        ";
        await cmd.ExecuteNonQueryAsync();

        // 2) Delete all non-keeper duplicates
        cmd.CommandText = @"
            DELETE FROM History
            WHERE rowid NOT IN (
                SELECT MIN(rowid) FROM History GROUP BY Expression
            );
        ";
        await cmd.ExecuteNonQueryAsync();

        // Ensure uniqueness by expression and helpful indexes
        cmd.CommandText = @"CREATE UNIQUE INDEX IF NOT EXISTS UX_History_Expression ON History(Expression);";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = @"CREATE INDEX IF NOT EXISTS IX_History_UsageCount ON History(UsageCount DESC);";
        await cmd.ExecuteNonQueryAsync();
    }
}
