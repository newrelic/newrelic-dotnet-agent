using System.Collections.Generic;
using System.Text;

namespace NewRelic.Parsing
{
    public static class StringsHelper
    {
        public static string FixDatabaseObjectName(string s)
        {
            int index = s.IndexOf('.');
            if (index > 0)
            {
                return new StringBuilder(s.Length)
                    .Append(RemoveBookendsAndLower(s.Substring(0, index)))
                    .Append('.')
                    .Append(FixDatabaseName(s.Substring(index + 1)))
                    .ToString();
            }
            else
            {
                return RemoveBookendsAndLower(s);
            }
        }

        /// <summary>
        /// Remove "bookend" characters (brackets, quotes, parenthesis) and convert to lower case.
        /// </summary>
        private static string RemoveBookendsAndLower(string s)
        {
            return RemoveBracketsQuotesParenthesis(s).ToLower();
        }
        private static string FixDatabaseName(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            bool first = true;
            foreach (string segment in s.Split('.'))
            {
                if (!first)
                {
                    sb.Append('.');
                }
                else
                {
                    first = false;
                }
                sb.Append(RemoveBookendsAndLower(segment));
            }
            return sb.ToString();
        }

        private static readonly KeyValuePair<char, char>[] Bookends = new KeyValuePair<char, char>[] {
            new KeyValuePair<char, char>('[',']'),
            new KeyValuePair<char, char>('"','"'),
            new KeyValuePair<char, char>('\'','\''),
            new KeyValuePair<char, char>('(',')')
        };
        public static string RemoveBracketsQuotesParenthesis(string value)
        {
            if (value.Length < 3)
                return value;

            var first = 0;
            var last = value.Length - 1;
            foreach (var kvp in Bookends)
            {
                while (value[first] == kvp.Key && value[last] == kvp.Value)
                {
                    first++;
                    last--;
                }
            }
            if (first != 0)
            {
                var length = value.Length - first * 2;
                value = value.Substring(first, length);
            }

            return value;
        }
    }
}
