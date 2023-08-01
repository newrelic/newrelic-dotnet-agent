// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace S3Validator
{
    public struct FileDetails
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "name")]
        public string Name { get; set; }

        [YamlDotNet.Serialization.YamlMember(Alias = "size")]
        public long Size { get; set; }
    }
}
