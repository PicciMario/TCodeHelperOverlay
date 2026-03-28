using System.Globalization;
using System.Text;
using TCodeLaunchpad.Core.Models;

namespace TCodeLaunchpad.Core.Search;

public sealed class WeightedSearchEngine : IWeightedSearchEngine
{
    private readonly SearchOptions _options;

    public WeightedSearchEngine(SearchOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<SearchResult> Search(IReadOnlyList<TCodeEntry> dataset, string query)
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var emptyQueryResults = dataset
                .OrderBy(static x => x.Code, StringComparer.OrdinalIgnoreCase)
                .Select(static x => new SearchResult(x, 0, false))
                .ToList();

            if (_options.MaxResults > 0)
            {
                return emptyQueryResults.Take(_options.MaxResults).ToList();
            }

            return emptyQueryResults;
        }

        var queryTokens = Tokenize(normalizedQuery);
        var results = new List<SearchResult>(dataset.Count);

        foreach (var entry in dataset)
        {
            var code = Normalize(entry.Code);
            var name = Normalize(entry.Descr);
            var keywords = Normalize(entry.Keywords);
            var longDescr = Normalize(entry.LongDescr);

            var prefixHit = code.StartsWith(normalizedQuery, StringComparison.Ordinal)
                || name.StartsWith(normalizedQuery, StringComparison.Ordinal);

            var score = 0;

            if (code.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                score += _options.CodePrefixWeight;
            }
            else if (code.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += _options.CodeContainsWeight;
            }

            if (name.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                score += _options.NamePrefixWeight;
            }
            else if (name.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += _options.NameContainsWeight;
            }

            foreach (var token in queryTokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (TokenExists(keywords, token))
                {
                    score += _options.KeywordExactWeight;
                }
                else if (keywords.Contains(token, StringComparison.Ordinal))
                {
                    score += _options.KeywordContainsWeight;
                }

                if (longDescr.Contains(token, StringComparison.Ordinal))
                {
                    score += _options.LongDescriptionContainsWeight;
                }
            }

            if (score > 0)
            {
                results.Add(new SearchResult(entry, score, prefixHit));
            }
        }

        var ordered = results
            .OrderByDescending(x => _options.PrefixFirst && x.PrefixHit)
            .ThenByDescending(static x => x.Score)
            .ThenBy(static x => x.Entry.Code.Length)
            .ThenBy(static x => x.Entry.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_options.MaxResults > 0)
        {
            return ordered.Take(_options.MaxResults).ToList();
        }

        return ordered;
    }

    private static bool TokenExists(string text, string token)
    {
        var tokens = Tokenize(text);
        return tokens.Contains(token, StringComparer.Ordinal);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var buffer = new char[formD.Length];
        var index = 0;

        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer[index++] = c;
        }

        return new string(buffer, 0, index);
    }

    private static string[] Tokenize(string value)
    {
        return value
            .Split(new[] { ' ', '\t', ',', '.', ';', ':', '-', '/', '\\', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
