// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using LibGit2Sharp;
using NewRelic.Agent.IntegrationTests.ApplicationHelpers;
using System;
using System.IO;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    public static class GitCommand
    {
        [LibraryMethod]
        public static string Clone(string repoUri, string fullPath)
        {
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            return Repository.Clone(repoUri, fullPath);
        }
    }
}
