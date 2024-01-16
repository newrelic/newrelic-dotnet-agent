// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    public class MockFileSystemManager : IFileSystemManager
    {
        public bool PathExists { get; set; }
        public string FileContents { get; set; }

        public bool Exists(string path)
        {
            return PathExists;
        }

        public void WriteAllText(string path, string contents)
        {
            FileContents = contents;
        }
    }
}

