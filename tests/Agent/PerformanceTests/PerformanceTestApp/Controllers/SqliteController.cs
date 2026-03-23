// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace PerformanceTestApp.Controllers;

[ApiController]
[Route("[controller]")]
public class SqliteController : ControllerBase
{
    internal const string ConnectionString = "Data Source=/tmp/perftest.db";

    private readonly ILogger<SqliteController> _logger;

    public SqliteController(ILogger<SqliteController> logger)
    {
        _logger = logger;
    }

    [HttpGet("crud")]
    public async Task<IActionResult> Crud()
    {
        var value = Guid.NewGuid().ToString();

        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        long id;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO perf (value) VALUES (@value); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@value", value);
            id = (long)(await cmd.ExecuteScalarAsync())!;
        }

        _logger.LogInformation("Inserted row {Id} with value {Value}", id, value);

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT value FROM perf WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                _logger.LogError("Row {Id} not found after insert", id);
                return StatusCode(500, "Row not found after insert");
            }
        }

        _logger.LogInformation("Found row {Id}", id);

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE perf SET value = 'updated' WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Updated row {Id}", id);

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM perf WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Deleted row {Id}", id);

        return Ok(new { id });
    }
}
