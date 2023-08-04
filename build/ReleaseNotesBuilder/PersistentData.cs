// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ReleaseNotesBuilder
{
    public class PersistentData
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "subject")]
        public string? Subject { get; set; }

        [YamlDotNet.Serialization.YamlMember(Alias = "download-link")]
        public string? DownloadLink { get; set; }

        [YamlDotNet.Serialization.YamlMember(Alias = "preamble")]
        public string? Preamble { get; set; }

        [YamlDotNet.Serialization.YamlMember(Alias = "epilogue")]
        public string? Epilogue { get; set; }
    }
}
