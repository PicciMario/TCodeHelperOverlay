using TCodeLaunchpad.Core.Models;

namespace TCodeLaunchpad.Core.Data;

public interface ITransactionRepository
{
    IReadOnlyList<TCodeEntry> Load(string filePath);
}
