using TCodeLaunchpad.Core.Models;
using TCodeLaunchpad.Core.Search;
using Xunit;

namespace TCodeLaunchpad.Core.Tests;

public class WeightedSearchEngineTests
{
    private readonly IReadOnlyList<TCodeEntry> _dataset = new[]
    {
        new TCodeEntry("FB60", "Inserimento fatture passive", "fattura passiva inserimento", "Contabilizzazione fatture", "FI", null),
        new TCodeEntry("AL08", "Gestione sessioni utente", "sessione utente login", "Monitorare sessioni aperte", "Basis", null),
        new TCodeEntry("DB02", "Diagnostica database", "diagnostica database sql", "Spazio disco e performance", "Basis", null)
    };

    [Fact]
    public void PrefixMatch_RanksBeforeContains()
    {
        var engine = new WeightedSearchEngine(new SearchOptions());

        var results = engine.Search(_dataset, "fb");

        Assert.NotEmpty(results);
        Assert.Equal("FB60", results[0].Entry.Code);
        Assert.True(results[0].PrefixHit);
    }

    [Fact]
    public void KeywordMatch_RanksWhenNoNameMatch()
    {
        var engine = new WeightedSearchEngine(new SearchOptions());

        var results = engine.Search(_dataset, "sql");

        Assert.NotEmpty(results);
        Assert.Equal("DB02", results[0].Entry.Code);
    }

    [Fact]
    public void EmptyQuery_ReturnsAlphabeticalSlice()
    {
        var engine = new WeightedSearchEngine(new SearchOptions { MaxResults = 2 });

        var results = engine.Search(_dataset, string.Empty);

        Assert.Equal(2, results.Count);
        Assert.Equal("AL08", results[0].Entry.Code);
        Assert.Equal("DB02", results[1].Entry.Code);
    }
}
