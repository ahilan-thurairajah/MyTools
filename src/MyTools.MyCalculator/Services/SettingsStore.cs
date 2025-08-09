using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyTools.MyCalculator.Services;

public sealed class SettingsStore
{
    private readonly LocalDb _db;
    public SettingsStore(LocalDb db) => _db = db;

    public async Task SetAsync(string key, string value)
    {
        await using var conn = new SqliteConnection(_db.ConnectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Settings (Key, Value) VALUES ($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetAsync(string key)
    {
        await using var conn = new SqliteConnection(_db.ConnectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }
}
