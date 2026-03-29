namespace TCodeLaunchpad.Core.Search;

public sealed record SearchScoreBreakdown(
    int CodePrefix,
    int CodeContains,
    int NamePrefix,
    int NameContains,
    int KeywordExact,
    int KeywordContains,
    int LongDescriptionContains)
{
    public int Total => CodePrefix + CodeContains + NamePrefix + NameContains + KeywordExact + KeywordContains + LongDescriptionContains;
}