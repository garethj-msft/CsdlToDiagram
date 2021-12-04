using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsdlToPlant.Tests
{
    public static class StringAssertExtensions
    {
#pragma warning disable IDE0060 // Remove unused parameter - stadard StringAssert extension mechanism.
        public static void ContainsCountOf(this StringAssert assert, string value, int count, string substring)
        {
            string processedSubstring = Regex.Escape(substring);
            int found = Regex.Matches(value, processedSubstring).Count;
            if (found != count)
            {
                throw new AssertFailedException($"Unexpected number of instances of '{substring}' found in '{value}'\r\nExpected {count}, actual {found}.");
            }
        }

        public static void MatchesLines(this StringAssert assert, string value, params string[] patterns)
        {
            string pattern = string.Join(Environment.NewLine, patterns);
            StringAssert.Matches(value, new Regex(pattern, RegexOptions.Multiline));
        }
#pragma warning restore IDE0060 // Remove unused parameter
    }
}