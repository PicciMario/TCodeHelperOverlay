using TCodeLaunchpad.Core.Search;

namespace TCodeLaunchpad.App.ViewModels;

public sealed class ResultRowViewModel
{
    public required string FilterText { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }

    public required string Module { get; init; }

    public required string BusinessObjectName { get; init; }

    public required string BusinessObjectCode { get; init; }

    public required string Keywords { get; init; }

    public required string LongDescription { get; init; }

    public required string ScoreDebugText { get; init; }

    public bool IsSuggestion { get; init; }

    public string SuggestionQuery { get; init; } = string.Empty;

    public static ResultRowViewModel FromBoSuggestion(string code, string name, int transactionCount, string suggestionQuery)
    {
        return new ResultRowViewModel
        {
            FilterText = string.Empty,
            Code = code,
            Description = name,
            Module = $"{transactionCount} transaction{(transactionCount == 1 ? string.Empty : "s")}",
            BusinessObjectName = string.Empty,
            BusinessObjectCode = string.Empty,
            Keywords = string.Empty,
            LongDescription = string.Empty,
            ScoreDebugText = string.Empty,
            IsSuggestion = true,
            SuggestionQuery = suggestionQuery
        };
    }

    public static ResultRowViewModel FromSearchResult(SearchResult result, string filterText)
    {
        var breakdown = result.Breakdown ?? new SearchScoreBreakdown(0, 0, 0, 0, 0, 0, 0);

        return new ResultRowViewModel
        {
            FilterText = filterText,
            Code = result.Entry.Code,
            Description = result.Entry.Descr,
            Module = result.Entry.Module,
            BusinessObjectName = result.Entry.BusinessObject?.Name ?? string.Empty,
            BusinessObjectCode = result.Entry.BusinessObject?.Code ?? string.Empty,
            Keywords = result.Entry.Keywords,
            LongDescription = result.Entry.LongDescr,
            ScoreDebugText =
                $"score={result.Score} | " +
                $"code[prefix={breakdown.CodePrefix}, contains={breakdown.CodeContains}] | " +
                $"name[prefix={breakdown.NamePrefix}, contains={breakdown.NameContains}] | " +
                $"keywords[exact={breakdown.KeywordExact}, contains={breakdown.KeywordContains}] | " +
                $"longDescr[contains={breakdown.LongDescriptionContains}] | " +
                $"prefixHit={result.PrefixHit}"
        };
    }
}
