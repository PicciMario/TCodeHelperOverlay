namespace TCodeLaunchpad.Core.Models;

public sealed record ModuleInfo(string Code, string Name);

public sealed record BusinessObject(string Code, string Name);

public sealed record TCodeEntry(
    string Code,
    string Descr,
    string Keywords,
    string LongDescr,
    ModuleInfo? Module,
    BusinessObject? BusinessObject
);
