// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET462

using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using System.Collections;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MongoDB
{
    [Library]
    public class MongoDBLegacyExerciser
    {
        private readonly string DatabaseName = Guid.NewGuid().ToString();
        private const string CollectionName = "myCollection";

        private MongoDatabase _db;

        [LibraryMethod]
        [Transaction]
        public void SetupClient()
        {
            var client = new MongoClient(new MongoUrl(MongoDbConfiguration.MongoDb3_2ConnectionString));
            var server = client.GetServer();
            _db = server.GetDatabase(DatabaseName);
            if (!_db.CollectionExists(CollectionName))
            {
                _db.CreateCollection(CollectionName);
            }
        }

        [LibraryMethod]
        public void CleanupClient()
        {
            _db.Drop();
        }

        #region Insert Update Remove API

        [LibraryMethod]
        [Transaction]
        public void Insert()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Genghis Khan" });
        }

        [LibraryMethod]
        [Transaction]
        public string OrderedBulkInsert()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            var orderedBulkOperation = collection.InitializeOrderedBulkOperation();
            orderedBulkOperation.Insert(new CustomMongoDbEntity(ObjectId.GenerateNewId(DateTime.Now), "Winston Churchill"));
            orderedBulkOperation.Insert(new CustomMongoDbEntity(ObjectId.GenerateNewId(), "FDR"));
            var result = orderedBulkOperation.Execute();
            return string.Format("OrderedBulkInsert: number documents inserted = {0}", result.InsertedCount);
        }

        [LibraryMethod]
        [Transaction]
        public string UnorderedBulkInsert()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            var unorderedBulkOperation = collection.InitializeUnorderedBulkOperation();
            unorderedBulkOperation.Insert(new CustomMongoDbEntity(ObjectId.GenerateNewId(DateTime.Now), "Stephen Jay Gould"));
            unorderedBulkOperation.Insert(new CustomMongoDbEntity(ObjectId.GenerateNewId(), "Robert Ardrey"));
            unorderedBulkOperation.Insert(new CustomMongoDbEntity(ObjectId.GenerateNewId(DateTime.Now.AddDays(-1.0)), "Robert Leakey"));
            var result = unorderedBulkOperation.Execute();
            return string.Format("UnorderedBulkInsert: number documents inserted = {0}", result.InsertedCount);
        }

        [LibraryMethod]
        [Transaction]
        public string Update()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Jules Verne" });
            var result = collection.Update(Query<CustomMongoDbEntity>.EQ(e => e.Name, "Jules Verne"),
                Update<CustomMongoDbEntity>.Set(e => e.Name, "H.G. Wells"));
            return result.ToString();
        }

        [LibraryMethod]
        [Transaction]
        public string Remove()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Alpha" });
            var result = collection.Remove(Query<CustomMongoDbEntity>.EQ(e => e.Name, "Alpha"));
            return result.ToString();
        }

        [LibraryMethod]
        [Transaction]
        public string RemoveAll()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Beta" });
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Gamma" });
            var result = collection.RemoveAll();
            return result.ToString();
        }

        [LibraryMethod]
        [Transaction]
        public string Drop()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Hawking" });
            var result = collection.Drop();
            return result.ToString();
        }

        #endregion

        #region Find API

        [LibraryMethod]
        [Transaction]
        public IEnumerable FindAll()
        {
            var cursor = _db.GetCollection(CollectionName).FindAll();
            return cursor;
        }

        [LibraryMethod]
        [Transaction]
        public MongoCursor<CustomMongoDbEntity> Find()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.Find(Query<CustomMongoDbEntity>.EQ(e => e.Name, "Genghis Khan"));
            return cursor;
        }

        [LibraryMethod]
        [Transaction]
        public CustomMongoDbEntity FindOne()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Julius Caesar" });
            var entity = collection.FindOne(Query<CustomMongoDbEntity>.EQ(e => e.Name, "Julius Caesar"));
            return entity;
        }

        [LibraryMethod]
        [Transaction]
        public CustomMongoDbEntity FindOneById()
        {
            var id = ObjectId.GenerateNewId(DateTime.Now);

            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = id, Name = "Julius Caesar" });
            var entity = collection.FindOneById(id);
            return entity;
        }

        [LibraryMethod]
        [Transaction]
        public BsonDocument FindOneAs()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Julius Caesar" });
            var bsonDocument = collection.FindOneAs<BsonDocument>();
            return bsonDocument;
        }

        #endregion

        #region FindAnd API

        [LibraryMethod]
        [Transaction]
        public string FindAndModify()
        {
            if (!_db.CollectionExists(CollectionName))
                _db.CreateCollection(CollectionName);

            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Jules Verne" });

            var query = Query<CustomMongoDbEntity>.EQ(e => e.Name, "Jules Verne");

            var result = collection.FindAndModify(
               new FindAndModifyArgs()
               {
                   Query = query,
                   Update = new UpdateBuilder().Set("Name", "Olaf Stapledon")
               });

            return result.ToString();
        }

        [LibraryMethod]
        [Transaction]
        public string FindAndRemove()
        {
            var collection = _db.GetCollection<CustomMongoDbEntity>(CollectionName);
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Jules Verne" });

            var query = Query<CustomMongoDbEntity>.EQ(e => e.Name, "Jules Verne");

            var result = collection.FindAndRemove(
               new FindAndRemoveArgs()
               {
                   Query = query
               });

            return result.ToString();
        }

        #endregion

        #region Generic Find API

        [LibraryMethod]
        [Transaction]
        public MongoCursor<CustomMongoDbEntity> GenericFind()
        {
            MongoCollection<CustomMongoDbEntity> collection = new MongoCollection<CustomMongoDbEntity>(_db, CollectionName, new MongoCollectionSettings());
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.Find(Query<CustomMongoDbEntity>.EQ(e => e.Name, "Genghis Khan"));
            return cursor;
        }

        [LibraryMethod]
        [Transaction]
        public MongoCursor<CustomMongoDbEntity> GenericFindAs()
        {
            MongoCollection<CustomMongoDbEntity> collection = new MongoCollection<CustomMongoDbEntity>(_db, CollectionName, new MongoCollectionSettings());
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.FindAs<CustomMongoDbEntity>(Query<CustomMongoDbEntity>.EQ(e => e.Name, "Genghis Khan"));
            return cursor;
        }

        [LibraryMethod]
        [Transaction]
        public IEnumerator<CustomMongoDbEntity> CursorGetEnumerator()
        {
            MongoCollection<CustomMongoDbEntity> collection = new MongoCollection<CustomMongoDbEntity>(_db, CollectionName, new MongoCollectionSettings());
            collection.Insert(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.Find(Query<CustomMongoDbEntity>.EQ(e => e.Name, "Genghis Khan"));
            var enumerator = cursor.GetEnumerator();
            return enumerator;
        }

        #endregion

        #region Index API

        [LibraryMethod]
        [Transaction]
        public string CreateIndex()
        {
            var result = _db.GetCollection(CollectionName).CreateIndex("keyname");
            return result.ToString();
        }

        [LibraryMethod]
        [Transaction]
        public string GetIndexes()
        {
            var result = _db.GetCollection(CollectionName).GetIndexes();
            return result.ToString();
        }

        [LibraryMethod]
        [Transaction]
        public string IndexExistsByName()
        {
            var result = _db.GetCollection(CollectionName).IndexExistsByName("keyname");
            return result.ToString();
        }

        #endregion

        #region Aggregate API

        [LibraryMethod]
        [Transaction]
        public string Aggregate()
        {
            var result = _db.GetCollection(CollectionName).Aggregate(new AggregateArgs { Pipeline = new[] { new BsonDocument(new BsonElement("name", new BsonInt32(1))) } });
            return result.ToString();
        }

        #endregion

        #region Other API


        [LibraryMethod]
        [Transaction]
        public string Validate()
        {
            var result = _db.GetCollection(CollectionName).Validate();
            return result.ToString();
        }

        [LibraryMethod]
        [Transaction]
        public string ParallelScanAs()
        {
            var readOnlyCollection = _db.GetCollection(CollectionName).ParallelScanAs<CustomMongoDbEntity>(new ParallelScanArgs());
            return readOnlyCollection.ToString();
        }

        #endregion
    }
}

#endif
