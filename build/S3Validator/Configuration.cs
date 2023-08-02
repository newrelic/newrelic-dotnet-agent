// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace S3Validator
{
    public class Configuration
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "base-url")]
        public string? BaseUrl { get; set; }

        [YamlDotNet.Serialization.YamlMember(Alias = "directory-list")]
        public List<string>? DirectoryList { get; set; }

        [YamlDotNet.Serialization.YamlMember(Alias = "file-list")]
        public List<FileDetails>? FileList { get; set; }
    }
}
