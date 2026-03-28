using TCodeLaunchpad.Core.Models;

namespace TCodeLaunchpad.Core.Search;

public interface IWeightedSearchEngine
{
    IReadOnlyList<SearchResult> Search(IReadOnlyList<TCodeEntry> dataset, string query);
}
