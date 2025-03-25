// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MongoDB.Bson;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MongoDB
{
    public class CustomMongoDbEntity
    {
        public ObjectId Id { get; set; }

        public string Name { get; set; }

        public CustomMongoDbEntity()
        {

        }

        public CustomMongoDbEntity(ObjectId id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
