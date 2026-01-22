using System.Text.RegularExpressions;

namespace BeefGen.Classes.Extensions;

public static class StringHelpers
{
    /// <summary>
    /// Replaces a word using regex
    /// </summary>
    /// <param name="original"></param>
    /// <param name="wordToFind"></param>
    /// <param name="replacement"></param>
    /// <param name="regexOptions"></param>
    /// <returns></returns>
    public static string ReplaceWord(this string original, string wordToFind, string replacement, RegexOptions regexOptions = RegexOptions.None)
    {
        var pattern = @$"\b{wordToFind}\b";
        return Regex.Replace(original, pattern, replacement);
    }
}
