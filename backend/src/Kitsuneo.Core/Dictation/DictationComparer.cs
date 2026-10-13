using System.Globalization;
using System.Text;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Dictation;

/// <summary>
/// Compares what the user typed in a dictation against the expected text.
/// English is compared word by word; Japanese is compared character by character
/// and <c>expected</c> must be the kana reading.
/// </summary>
public static class DictationComparer
{
    private const int KatakanaToHiraganaOffset = 0x60;

    public static DictationResult Compare(Language language, string expected, string actual)
    {
        if (!Enum.IsDefined(language))
            throw new DomainException("Invalid language.");

        var expectedTokens = Tokenize(language, expected);
        if (expectedTokens.Count == 0)
            throw new DomainException("Expected text is required.");

        var tokens = Align(expectedTokens, Tokenize(language, actual));
        var correct = tokens.Count(t => t.Status == DictationTokenStatus.Correct);
        var accuracy = Math.Round(100.0 * correct / tokens.Count, 2);

        return new DictationResult(tokens, accuracy);
    }

    private static List<string> Tokenize(Language language, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var normalized = text.Normalize(NormalizationForm.FormKC);

        return language == Language.Japanese
            ? TokenizeJapanese(normalized)
            : TokenizeEnglish(normalized);
    }

    /// <summary>Lowercase words; apostrophes are dropped and other punctuation separates words.</summary>
    private static List<string> TokenizeEnglish(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (c is '\'' or '’')
                continue;

            builder.Append(char.IsPunctuation(c) || char.IsSymbol(c) ? ' ' : char.ToLowerInvariant(c));
        }

        return [.. builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>One token per character, katakana folded to hiragana, spaces and punctuation ignored.</summary>
    private static List<string> TokenizeJapanese(string text)
    {
        var tokens = new List<string>(text.Length);
        var elements = StringInfo.GetTextElementEnumerator(text);

        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            var first = element[0];

            if (char.IsWhiteSpace(first) || char.IsPunctuation(first) || char.IsSymbol(first))
                continue;

            tokens.Add(element.Length == 1 && IsFoldableKatakana(first)
                ? ((char)(first - KatakanaToHiraganaOffset)).ToString()
                : element.ToLowerInvariant());
        }

        return tokens;
    }

    // ァ (U+30A1) to ヶ (U+30F6) map onto ぁ (U+3041) to ゖ (U+3096); ー and ・ have no hiragana form.
    private static bool IsFoldableKatakana(char c) => c is >= 'ァ' and <= 'ヶ';

    /// <summary>
    /// Levenshtein alignment walked from the start, so on ties earlier tokens are matched
    /// and gaps are pushed towards the end.
    /// </summary>
    private static List<DictationToken> Align(List<string> expected, List<string> actual)
    {
        var rows = expected.Count;
        var cols = actual.Count;

        // cost[i, j] = edit distance between expected[i..] and actual[j..]
        var cost = new int[rows + 1, cols + 1];

        for (var i = rows; i >= 0; i--)
            cost[i, cols] = rows - i;
        for (var j = cols; j >= 0; j--)
            cost[rows, j] = cols - j;

        for (var i = rows - 1; i >= 0; i--)
        {
            for (var j = cols - 1; j >= 0; j--)
            {
                var diagonal = cost[i + 1, j + 1] + (expected[i] == actual[j] ? 0 : 1);
                cost[i, j] = Math.Min(diagonal, Math.Min(cost[i + 1, j], cost[i, j + 1]) + 1);
            }
        }

        var tokens = new List<DictationToken>(Math.Max(rows, cols));
        int row = 0, col = 0;

        while (row < rows || col < cols)
        {
            if (row < rows && col < cols)
            {
                var isMatch = expected[row] == actual[col];
                if (cost[row, col] == cost[row + 1, col + 1] + (isMatch ? 0 : 1))
                {
                    tokens.Add(new DictationToken(
                        isMatch ? DictationTokenStatus.Correct : DictationTokenStatus.Wrong,
                        expected[row],
                        actual[col]));
                    row++;
                    col++;
                    continue;
                }
            }

            if (row < rows && cost[row, col] == cost[row + 1, col] + 1)
            {
                tokens.Add(new DictationToken(DictationTokenStatus.Missing, expected[row], null));
                row++;
            }
            else
            {
                tokens.Add(new DictationToken(DictationTokenStatus.Extra, null, actual[col]));
                col++;
            }
        }

        return tokens;
    }
}
