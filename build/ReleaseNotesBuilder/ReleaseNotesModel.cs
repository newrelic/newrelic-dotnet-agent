// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace ReleaseNotesBuilder
{
    internal class ReleaseNotesModel
    {
        private const string FrontMatterWrapper = "---";
        private const string FrontMatterSubject = "subject";
        private const string FrontMatterReleaseDate = "releaseDate";
        private const string FrontMatterVersion = "version";
        private const string FrontMatterDownloadLink = "downloadLink";
        private const string FrontMatterFeatures = "features";
        private const string FrontMatterBugs = "bugs";
        private const string FrontMatterSecurity = "security";

        private readonly string? _subject;
        private readonly string? _releaseDate;
        private string? _releaseVersion;
        private readonly string? _downloadLink;
        private readonly List<string> _frontFeatures;
        private readonly List<string> _frontBugs;
        private readonly List<string> _frontSecurity;

        private readonly string? _preamble;
        private readonly List<string> _bodyNewFeatures;
        private readonly List<string> _bodyFixes;
        private readonly List<string> _bodySecurity;
        private readonly List<string> _bodyNotice;
        private readonly string _checksums;
        private readonly string? _epilogue;

        public ReleaseNotesModel(PersistentData persistentData, string checksums)
        {
            _frontFeatures = new List<string>();
            _frontBugs = new List<string>();
            _frontSecurity = new List<string>();
            _bodyNewFeatures = new List<string>();
            _bodyFixes = new List<string>();
            _bodySecurity = new List<string>();
            _bodyNotice = new List<string>();

            _subject = persistentData.Subject;
            _releaseDate = DateTime.Now.ToString("yyyy-MM-dd");
            _downloadLink = persistentData.DownloadLink;
            _preamble = persistentData.Preamble;
            _epilogue = persistentData.Epilogue;
            _checksums = checksums;
        }

        public string GetDocumentContents()
        {
            Validate();

            var builder = new StringBuilder();
            builder.AppendLine(FrontMatterWrapper);
            builder.AppendLine($"{FrontMatterSubject}: {_subject}");
            builder.AppendLine($"{FrontMatterReleaseDate}: '{_releaseDate}'");
            builder.AppendLine($"{FrontMatterVersion}: {_releaseVersion}");
            builder.AppendLine($"{FrontMatterDownloadLink}: '{_downloadLink}'");
            builder.AppendLine($"{FrontMatterFeatures}: [{string.Join(',', _frontFeatures)}]");
            builder.AppendLine($"{FrontMatterBugs}: [{string.Join(',', _frontBugs)}]");
            builder.AppendLine($"{FrontMatterSecurity}: [{string.Join(',', _frontSecurity)}]");
            builder.AppendLine(FrontMatterWrapper);
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(_preamble))
            {
                builder.AppendLine(_preamble);
                builder.AppendLine();
            }

            AppendSection(builder, Program.NoticeSection, _bodyNotice);
            AppendSection(builder, Program.SecuritySection, _bodySecurity);
            AppendSection(builder, Program.NewFeaturesSection, _bodyNewFeatures);
            AppendSection(builder, Program.FixesSection, _bodyFixes);

            builder.AppendLine(_checksums);

            if (!string.IsNullOrWhiteSpace(_epilogue))
            {
                builder.AppendLine();
                builder.AppendLine(_epilogue);
            }

            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string header, List<string> entries)
        {
            if (entries.Any())
            {
                builder.AppendLine(header);
                builder.AppendLine();
                foreach (var entry in entries)
                {
                    builder.AppendLine(entry);
                }

                builder.AppendLine();
            }
        }

        private void Validate()
        {
            var problems = new List<string>();

            CheckString(nameof(_subject), _subject);
            CheckString(nameof(_releaseDate), _releaseDate); // not validating that date is today since this could be run for previous releases.
            CheckString(nameof(_releaseVersion), _releaseVersion);
            CheckString(nameof(_downloadLink), _downloadLink);
            CheckString(nameof(_checksums), _checksums);

            // Not checking preamble or epilogue since they can be empty.

            CheckList($"{nameof(_frontFeatures)} and {nameof(_bodyNewFeatures)}", _frontFeatures, _bodyNewFeatures);
            CheckList($"{nameof(_frontBugs)} and {nameof(_bodyFixes)}", _frontBugs, _bodyFixes);
            CheckList($"{nameof(_frontSecurity)} and {nameof(_bodySecurity)}", _frontSecurity, _bodySecurity);

            // _bodyNotice does not need to be checked since it is singlular and optional.

            if (problems.Any())
            {
                Program.ExitWithError(ExitCode.InvalidData,
                    "The following problems occurred building the release notes:"
                    + Environment.NewLine + "  "
                    + string.Join(Environment.NewLine + "  ", problems)
                    );
            }

            void CheckString(string name, string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    problems.Add($"Data for {name} was null or whitespace.");
                }
            }

            void CheckList(string name, List<string> front, List<string> body)
            {
                if (!front.Any() && !body.Any())
                {
                    return;
                }

                if (front.Count != body.Count)
                {
                    problems.Add($"Count for {name} did not match.");
                }
            }
        }

        public void SetReleaseVersion(string releaseVersion)
        {
            _releaseVersion = releaseVersion;
        }

        public void AddFeatures(List<Entry> entries)
        {
            foreach (var entry in entries)
            {
                _frontFeatures.Add($"'{entry.Front}'");
                _bodyNewFeatures.Add(entry.Body);
            }
        }

        public void AddBugsAndFixes(List<Entry> entries)
        {
            foreach (var entry in entries)
            {
                _frontBugs.Add($"'{entry.Front}'");
                _bodyFixes.Add(entry.Body);
            }
        }

        public void AddSecurity(List<Entry> entries)
        {
            foreach (var entry in entries)
            {
                _frontSecurity.Add($"'{entry.Front}'");
                _bodySecurity.Add(entry.Body);
            }
        }

        public void AddNotice(List<Entry> entries)
        {
            foreach (var entry in entries)
            {
                // Notice does not have a front-matter item.
                _bodyNotice.Add(entry.Body);
            }
        }
    }
}
