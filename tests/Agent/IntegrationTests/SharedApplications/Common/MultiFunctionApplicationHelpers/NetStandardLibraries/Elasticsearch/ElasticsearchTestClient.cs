// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal abstract class ElasticsearchTestClient
    {
        protected const int Port = 9200;
        protected Uri Address = new Uri($"http://localhost:{Port}");

        public ElasticsearchTestClient() { }

        public abstract void Connect();
        public abstract void Index();

        public abstract Task<bool> IndexAsync();

        public abstract void Search();

        public abstract Task<long> SearchAsync();
    }

    public class FakeRecord
    {
        public FakeRecord(string first, string last, string location, int zip)
        {
            Id = 0;
            FirstName = first;
            LastName = last;
            Location = location;
            ZipCode = zip;
        }
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Location { get; set; }
        public int ZipCode { get; set; }
    }
}
