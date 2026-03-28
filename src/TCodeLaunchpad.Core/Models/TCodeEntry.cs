namespace TCodeLaunchpad.Core.Models;

public sealed record BusinessObject(string Code, string Name);

public sealed record TCodeEntry(
    string Code,
    string Descr,
    string Keywords,
    string LongDescr,
    string Module,
    BusinessObject? BusinessObject
);
