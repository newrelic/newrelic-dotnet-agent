// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MongoDB.Bson;

namespace MongoDBApplication
{
    public class CustomMongoEntity
    {
        public ObjectId Id { get; set; }

        public string Name { get; set; }

        public CustomMongoEntity()
        {

        }

        public CustomMongoEntity(ObjectId id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
