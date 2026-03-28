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
}
