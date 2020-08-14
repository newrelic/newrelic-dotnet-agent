// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MongoDB.Bson;

namespace MongoDB2_6CoreApplication
{
    public class CustomMongoDB2_6Entity
    {
        public ObjectId Id { get; set; }

        public string Name { get; set; }

        public CustomMongoDB2_6Entity()
        {

        }

        public CustomMongoDB2_6Entity(ObjectId id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
