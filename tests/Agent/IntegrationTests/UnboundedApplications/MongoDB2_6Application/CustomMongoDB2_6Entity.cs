/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MongoDB.Bson;

namespace MongoDB2_6Application
{
    internal class CustomMongoDB2_6Entity
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
