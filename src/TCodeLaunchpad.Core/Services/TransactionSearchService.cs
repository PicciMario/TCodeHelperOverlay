using TCodeLaunchpad.Core.Data;
using TCodeLaunchpad.Core.Models;
using TCodeLaunchpad.Core.Search;

namespace TCodeLaunchpad.Core.Services;

public sealed class TransactionSearchService
{
    private readonly ITransactionRepository _repository;
    private readonly IWeightedSearchEngine _searchEngine;
    private readonly string _dataPath;
    private IReadOnlyList<TCodeEntry> _dataset = Array.Empty<TCodeEntry>();

    public TransactionSearchService(ITransactionRepository repository, IWeightedSearchEngine searchEngine, string dataPath)
    {
        _repository = repository;
        _searchEngine = searchEngine;
        _dataPath = dataPath;
    }

    public int Count => _dataset.Count;

    public void Reload()
    {
        _dataset = _repository.Load(_dataPath);
    }

    public IReadOnlyList<SearchResult> Search(string query)
    {
        return _searchEngine.Search(_dataset, query);
    }

    public IReadOnlyList<SearchResult> SearchByModule(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return Array.Empty<SearchResult>();
        }

        return _dataset
            .Where(x => x.Module.StartsWith(module, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SearchResult(x, 0, false))
            .ToList();
    }

    public IReadOnlyList<SearchResult> SearchByBusinessObjectCode(string businessObjectCode)
    {
        if (string.IsNullOrWhiteSpace(businessObjectCode))
        {
            return Array.Empty<SearchResult>();
        }

        return _dataset
            .Where(x => x.BusinessObject?.Code.StartsWith(businessObjectCode, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SearchResult(x, 0, false))
            .ToList();
    }

    public IReadOnlyList<(string Code, string Name, int TransactionCount)> GetBusinessObjectSuggestions(string prefix)
    {
        return _dataset
            .Where(x => x.BusinessObject != null &&
                        (string.IsNullOrEmpty(prefix) ||
                         x.BusinessObject.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(x => x.BusinessObject!.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.First().BusinessObject!.Name, g.Count()))
            .OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
