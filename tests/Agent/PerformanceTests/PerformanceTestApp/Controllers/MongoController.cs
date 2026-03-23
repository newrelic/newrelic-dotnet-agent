// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace PerformanceTestApp.Controllers;

[ApiController]
[Route("[controller]")]
public class MongoController : ControllerBase
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly ILogger<MongoController> _logger;

    public MongoController(IMongoDatabase db, ILogger<MongoController> logger)
    {
        _collection = db.GetCollection<BsonDocument>("perf");
        _logger = logger;
    }

    [HttpGet("crud")]
    public async Task<IActionResult> Crud()
    {
        var value = Guid.NewGuid().ToString();
        var doc = new BsonDocument { ["value"] = value };

        _logger.LogInformation("Inserting document with {Value}", value);
        await _collection.InsertOneAsync(doc);

        var id = doc["_id"].AsObjectId;
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);

        var found = await _collection.Find(filter).FirstOrDefaultAsync();
        if (found == null)
        {
            _logger.LogError("Document {Id} not found after insert", id);
            return StatusCode(500, "Document not found after insert");
        }

        _logger.LogInformation("Updating document {Id}", id);
        var update = Builders<BsonDocument>.Update.Set("value", "updated");
        await _collection.UpdateOneAsync(filter, update);

        _logger.LogInformation("Deleting document {Id}", id);
        await _collection.DeleteOneAsync(filter);

        return Ok(new { id = id.ToString() });
    }
}
