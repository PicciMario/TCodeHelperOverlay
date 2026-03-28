using System.Threading;

namespace TCodeLaunchpad.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;

    public bool IsPrimaryInstance { get; }

    public SingleInstanceService(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var createdNew);
        IsPrimaryInstance = createdNew;
    }

    public void Dispose()
    {
        if (IsPrimaryInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
