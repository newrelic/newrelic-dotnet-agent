// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.AwsSdk
{
    public class ArnBuilder
    {
        private string _partition;
        public string Partition
        {
            private set
            {
                _partition = value;
            }
            get => _partition;
        }

        private string _region;
        public string Region
        {
            private set
            {
                _region = value;
            }
            get => _region;
        }
        private string _accountId;
        public string AccountId
        {
            private set
            {
                _accountId = value;
            }
            get => _accountId;
        }
        public ArnBuilder(string partition, string region, string accountId)
        {
            _partition = partition;
            _region = region;
            _accountId = accountId;
        }

        public string Build(string service, string resource)
        {
            if (string.IsNullOrEmpty(_partition) || string.IsNullOrEmpty(_region) || string.IsNullOrEmpty(_accountId))
            {
                return null;
            }
            return "arn:" + _partition + ":" + service + ":" + _region + ":" + _accountId + ":" + resource;
        } 
    }
}
