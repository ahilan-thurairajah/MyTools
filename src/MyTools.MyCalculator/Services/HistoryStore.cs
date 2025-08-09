using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyTools.MyCalculator.Services;

public sealed record HistoryItem(long Id, string Expression, string Result, DateTime CreatedUtc, DateTime LastUsedUtc, long UsageCount);

public sealed class HistoryStore
{
    private readonly LocalDb _db;
    public HistoryStore(LocalDb db) => _db = db;

    public async Task AddAsync(string expression, string result)
    {
        await using var conn = new SqliteConnection(_db.ConnectionString);
        await conn.OpenAsync();
        var tx = await conn.BeginTransactionAsync();
        try
        {
            // Try update existing (unique by Expression); if not exists, insert new
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE History
                                 SET Result = $r,
                                     UsageCount = UsageCount + 1,
                                     LastUsedUtc = $now
                                 WHERE Expression = $e";
            cmd.Parameters.AddWithValue("$e", expression);
            cmd.Parameters.AddWithValue("$r", result);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            var rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0)
            {
                cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO History (Expression, Result, CreatedUtc, LastUsedUtc, UsageCount)
                                    VALUES ($e,$r,$t,$now,1)";
                cmd.Parameters.AddWithValue("$e", expression);
                cmd.Parameters.AddWithValue("$r", result);
                cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
                cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<HistoryItem>> GetLatestAsync(int limit)
    {
        var list = new List<HistoryItem>(limit);
        await using var conn = new SqliteConnection(_db.ConnectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        // Top 5 by frequency (desc), then remaining by Expression asc
        cmd.CommandText = @"
            WITH Ranked AS (
                SELECT Id, Expression, Result, CreatedUtc, LastUsedUtc, UsageCount,
                       ROW_NUMBER() OVER (ORDER BY UsageCount DESC, LastUsedUtc DESC, Id DESC) AS rn
                FROM History
            ),
            TopFive AS (
                SELECT 0 AS grp, rn, Id, Expression, Result, CreatedUtc, LastUsedUtc, UsageCount
                FROM Ranked
                WHERE rn <= 5
                ORDER BY rn
            ),
            TheRest AS (
                SELECT 1 AS grp, rn, Id, Expression, Result, CreatedUtc, LastUsedUtc, UsageCount
                FROM Ranked
                WHERE rn > 5
                ORDER BY Expression COLLATE NOCASE ASC
            )
            SELECT Id, Expression, Result, CreatedUtc, LastUsedUtc, UsageCount
            FROM (
                SELECT * FROM TopFive
                UNION ALL
                SELECT * FROM TheRest
            )
            ORDER BY grp, rn
            LIMIT $n;";
        cmd.Parameters.AddWithValue("$n", limit);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new HistoryItem(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                DateTime.Parse(reader.GetString(4)),
                reader.GetInt64(5)
            ));
        }
        return list;
    }

    public async Task TrimAsync(int keep)
    {
        await using var conn = new SqliteConnection(_db.ConnectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            WITH Ranked AS (
                SELECT Id, Expression, UsageCount, LastUsedUtc,
                       ROW_NUMBER() OVER (ORDER BY UsageCount DESC, LastUsedUtc DESC, Id DESC) AS rn
                FROM History
            ),
            Ordered AS (
                SELECT Id, Expression, rn, 0 AS grp FROM Ranked WHERE rn <= 5
                UNION ALL
                SELECT Id, Expression, rn, 1 AS grp FROM Ranked WHERE rn > 5
            ),
            KeepIds AS (
                SELECT Id
                FROM Ordered
                ORDER BY grp,
                         CASE WHEN grp = 0 THEN rn END,
                         CASE WHEN grp = 1 THEN Expression END COLLATE NOCASE
                LIMIT $k
            )
            DELETE FROM History WHERE Id NOT IN (SELECT Id FROM KeepIds);";
        cmd.Parameters.AddWithValue("$k", keep);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<HistoryItem>> SearchAsync(string query, int limit)
    {
        var list = new List<HistoryItem>(limit);
        await using var conn = new SqliteConnection(_db.ConnectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Id, Expression, Result, CreatedUtc, LastUsedUtc, UsageCount
                             FROM History
                             WHERE Expression LIKE $q
                             ORDER BY UsageCount DESC, LastUsedUtc DESC, Expression COLLATE NOCASE ASC
                             LIMIT $n";
        cmd.Parameters.AddWithValue("$q", "%" + query + "%");
        cmd.Parameters.AddWithValue("$n", limit);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new HistoryItem(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                DateTime.Parse(reader.GetString(4)),
                reader.GetInt64(5)
            ));
        }
        return list;
    }
}
