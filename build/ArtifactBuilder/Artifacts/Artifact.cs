/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace ArtifactBuilder.Artifacts
{
    public abstract class Artifact
    {
        public Artifact(string name)
        {
            Name = name;

            RepoRootDirectory = FileHelpers.GetRepoRootDirectory();
            StagingDirectory = $@"{RepoRootDirectory}\Build\_staging\{Name}";
            PackageDirectory = $@"{RepoRootDirectory}\Build\Packaging\{Name}";
            OutputDirectory = $@"{RepoRootDirectory}\Build\BuildArtifacts\{Name}";

            HomeRootDirectory = FileHelpers.GetHomeRootDirectory();
        }

        public string RepoRootDirectory { get; }
        public string HomeRootDirectory { get; }
        public string Name { get; }

        protected string StagingDirectory;
        protected string PackageDirectory;
        protected string OutputDirectory;

        public void Build()
        {
            FileHelpers.DeleteDirectories(StagingDirectory, OutputDirectory);
            InternalBuild();
        }

        protected abstract void InternalBuild();
    }
}
