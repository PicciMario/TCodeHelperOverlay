namespace TCodeLaunchpad.Core.Search;

public sealed class SearchOptions
{
    // 0 or less means no hard cap.
    public int MaxResults { get; set; } = 0;

    public bool PrefixFirst { get; set; } = true;

    public int CodePrefixWeight { get; set; } = 1000;

    public int NamePrefixWeight { get; set; } = 800;

    public int CodeContainsWeight { get; set; } = 500;

    public int NameContainsWeight { get; set; } = 300;

    public int KeywordExactWeight { get; set; } = 120;

    public int KeywordContainsWeight { get; set; } = 60;

    public int LongDescriptionContainsWeight { get; set; } = 15;
}
