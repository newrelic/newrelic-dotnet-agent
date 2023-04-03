using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace release_notes_generator
{
    internal class FrontmatterFormatter
    {
        public FrontmatterFormatter() { }

        // TODO: Unit tests
        public static string FormatStringForFrontmatter(string input)
        {
            var result = input;

            // Filter out anything up to a `(` or `[`
            var match = Regex.Match(result, @"(.*?)[\(\[]", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                result = match.Groups[1].Value.Trim();
            }

            // End capture on any ". "
            match = Regex.Match(result, @"(.*?)\. ", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                result = match.Groups[1].Value.Trim();
            }

            return result;
        }
    }
}
