using TCodeLaunchpad.Core.Search;

namespace TCodeLaunchpad.App.ViewModels;

public sealed class ResultRowViewModel
{
    public required string Code { get; init; }

    public required string Description { get; init; }

    public required string Module { get; init; }

    public required string Keywords { get; init; }

    public required string LongDescription { get; init; }

    public static ResultRowViewModel FromSearchResult(SearchResult result)
    {
        return new ResultRowViewModel
        {
            Code = result.Entry.Code,
            Description = result.Entry.Descr,
            Module = result.Entry.Module,
            Keywords = result.Entry.Keywords,
            LongDescription = result.Entry.LongDescr
        };
    }
}
