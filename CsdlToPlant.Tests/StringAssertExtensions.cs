using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsdlToPlant.Tests
{
    public static class StringAssertExtensions
    {
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
            string pattern = string.Join("\r\n", patterns);
            StringAssert.Matches(value, new Regex(pattern, RegexOptions.Multiline));
        }
    }
}