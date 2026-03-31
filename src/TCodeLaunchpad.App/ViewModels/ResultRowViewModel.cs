using TCodeLaunchpad.Core.Search;

namespace TCodeLaunchpad.App.ViewModels;

public sealed class ResultRowViewModel
{
    public required string FilterText { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }

    public required string Module { get; init; }

    public required string BusinessObjectName { get; init; }

    public required string Keywords { get; init; }

    public required string LongDescription { get; init; }

    public required string ScoreDebugText { get; init; }

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
