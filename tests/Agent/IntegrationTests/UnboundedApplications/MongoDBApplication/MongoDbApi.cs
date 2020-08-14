// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using System.Collections;
using NewRelic.Agent.IntegrationTests.Shared;

namespace MongoDBApplication
{
    public class MongoDbApi
    {
        const string DatabaseName = "myDb";
        const string CollectionName = "myCollection";

        private readonly MongoDatabase _db;
        private readonly string _collectionName;

        public MongoDbApi()
        {
            var client = new MongoClient(new MongoUrl(MongoDbConfiguration.MongoDbConnectionString));
            var server = client.GetServer();
            _db = server.GetDatabase(DatabaseName);
            _collectionName = CollectionName;
        }

        #region Insert Update Remove API
        public void Insert()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Genghis Khan" });
        }

        public string OrderedBulkInsert()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            var orderedBulkOperation = collection.InitializeOrderedBulkOperation();
            orderedBulkOperation.Insert(new CustomMongoEntity(ObjectId.GenerateNewId(DateTime.Now), "Winston Churchill"));
            orderedBulkOperation.Insert(new CustomMongoEntity(ObjectId.GenerateNewId(), "FDR"));
            var result = orderedBulkOperation.Execute();
            return string.Format("OrderedBulkInsert: number documents inserted = {0}", result.InsertedCount);
        }

        public string UnorderedBulkInsert()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            var unorderedBulkOperation = collection.InitializeUnorderedBulkOperation();
            unorderedBulkOperation.Insert(new CustomMongoEntity(ObjectId.GenerateNewId(DateTime.Now), "Stephen Jay Gould"));
            unorderedBulkOperation.Insert(new CustomMongoEntity(ObjectId.GenerateNewId(), "Robert Ardrey"));
            unorderedBulkOperation.Insert(new CustomMongoEntity(ObjectId.GenerateNewId(DateTime.Now.AddDays(-1.0)), "Robert Leakey"));
            var result = unorderedBulkOperation.Execute();
            return string.Format("UnorderedBulkInsert: number documents inserted = {0}", result.InsertedCount);
        }


        public string Update()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Jules Verne" });
            var result = collection.Update(Query<CustomMongoEntity>.EQ(e => e.Name, "Jules Verne"),
                Update<CustomMongoEntity>.Set(e => e.Name, "H.G. Wells"));
            return result.ToString();
        }

        public string Remove()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Alpha" });
            var result = collection.Remove(Query<CustomMongoEntity>.EQ(e => e.Name, "Alpha"));
            return result.ToString();
        }

        public string RemoveAll()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Beta" });
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Gamma" });
            var result = collection.RemoveAll();
            return result.ToString();
        }

        public string Drop()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Hawking" });
            var result = collection.Drop();
            return result.ToString();
        }
        #endregion

        #region Find API
        public IEnumerable FindAll()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var cursor = _db.GetCollection(_collectionName).FindAll();
            return cursor;
        }

        public MongoCursor<CustomMongoEntity> Find()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.Find(Query<CustomMongoEntity>.EQ(e => e.Name, "Genghis Khan"));
            return cursor;
        }

        public CustomMongoEntity FindOne()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Julius Caesar" });
            var entity = collection.FindOne(Query<CustomMongoEntity>.EQ(e => e.Name, "Julius Caesar"));
            return entity;
        }

        public CustomMongoEntity FindOneById()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var id = ObjectId.GenerateNewId(DateTime.Now);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = id, Name = "Julius Caesar" });
            var entity = collection.FindOneById(id);
            return entity;
        }

        public BsonDocument FindOneAs()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Julius Caesar" });
            var bsonDocument = collection.FindOneAs<BsonDocument>();
            return bsonDocument;
        }
        #endregion

        #region FindAnd API
        public string FindAndModify()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Jules Verne" });

            var query = Query<CustomMongoEntity>.EQ(e => e.Name, "Jules Verne");

            var result = collection.FindAndModify(
               new FindAndModifyArgs()
               {
                   Query = query,
                   Update = MongoDB.Driver.Builders.Update.Set("Name", "Olaf Stapledon")
               });

            return result.ToString();
        }


        public string FindAndRemove()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var collection = _db.GetCollection<CustomMongoEntity>(_collectionName);
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Jules Verne" });

            var query = Query<CustomMongoEntity>.EQ(e => e.Name, "Jules Verne");

            var result = collection.FindAndRemove(
               new FindAndRemoveArgs()
               {
                   Query = query
               });

            return result.ToString();
        }

        #endregion

        #region Generic Find API
        public MongoCursor<CustomMongoEntity> GenericFind()
        {
            MongoCollection<CustomMongoEntity> collection = new MongoCollection<CustomMongoEntity>(_db, _collectionName, new MongoCollectionSettings());
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.Find(Query<CustomMongoEntity>.EQ(e => e.Name, "Genghis Khan"));
            return cursor;
        }

        public MongoCursor<CustomMongoEntity> GenericFindAs()
        {
            MongoCollection<CustomMongoEntity> collection = new MongoCollection<CustomMongoEntity>(_db, _collectionName, new MongoCollectionSettings());
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.FindAs<CustomMongoEntity>(Query<CustomMongoEntity>.EQ(e => e.Name, "Genghis Khan"));
            return cursor;
        }

        public IEnumerator<CustomMongoEntity> CursorGetEnumerator()
        {
            MongoCollection<CustomMongoEntity> collection = new MongoCollection<CustomMongoEntity>(_db, _collectionName, new MongoCollectionSettings());
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.Find(Query<CustomMongoEntity>.EQ(e => e.Name, "Genghis Khan"));
            var enumerator = cursor.GetEnumerator();
            return enumerator;
        }

        public bool CursorMoveNext()
        {
            MongoCollection<CustomMongoEntity> collection = new MongoCollection<CustomMongoEntity>(_db, _collectionName, new MongoCollectionSettings());
            collection.Insert(new CustomMongoEntity { Id = new ObjectId(), Name = "Genghis Khan" });
            var cursor = collection.Find(Query<CustomMongoEntity>.EQ(e => e.Name, "Genghis Khan"));
            var enumerator = new MongoCursorEnumerator<CustomMongoEntity>(cursor);
            return enumerator.MoveNext();
        }


        #endregion

        #region Index API
        public string CreateIndex()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var result = _db.GetCollection(_collectionName).CreateIndex("keyname");
            return result.ToString();
        }

        public string GetIndexes()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var result = _db.GetCollection(_collectionName).GetIndexes();
            return result.ToString();
        }

        public string IndexExistsByName()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var result = _db.GetCollection(_collectionName).IndexExistsByName("keyname");
            return result.ToString();
        }

        #endregion

        #region Aggregate API
        public string Aggregate()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var result = _db.GetCollection(_collectionName).Aggregate(new AggregateArgs { Pipeline = new[] { new BsonDocument(new BsonElement("name", new BsonInt32(1))) } });
            return result.ToString();
        }

        public string AggregateExplain()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var result = _db.GetCollection(_collectionName).AggregateExplain(new AggregateArgs { Pipeline = new[] { new BsonDocument(new BsonElement("name", new BsonInt32(1))) } });
            return result.ToString();
        }
        #endregion

        #region Other API
        public string Validate()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var result = _db.GetCollection(_collectionName).Validate();
            return result.ToString();
        }

        public string ParallelScanAs()
        {
            if (!_db.CollectionExists(_collectionName))
                _db.CreateCollection(_collectionName);

            var readOnlyCollection = _db.GetCollection(_collectionName).ParallelScanAs<CustomMongoEntity>(new ParallelScanArgs());
            return readOnlyCollection.ToString();
        }

        public string CreateCollection()
        {
            var collectionName = string.Format("collection-{0}", Guid.NewGuid().ToString());
            var result = _db.CreateCollection(collectionName);
            return result.ToString();
        }
        #endregion
    }
}
