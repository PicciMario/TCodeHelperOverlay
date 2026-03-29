using TCodeLaunchpad.Core.Models;

namespace TCodeLaunchpad.Core.Search;

public sealed record SearchResult(TCodeEntry Entry, int Score, bool PrefixHit, SearchScoreBreakdown? Breakdown = null);
