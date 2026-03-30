using System.Globalization;
using System.IO;
using System.Net.Http;

namespace TCodeLaunchpad.App.Services;

public sealed record DataCacheStatus(
    string CacheFilePath,
    DateTimeOffset DownloadedAtUtc,
    TimeSpan Age,
    bool DownloadAttempted,
    bool DownloadSucceeded);

public sealed class DataCacheService
{
    private const string RemoteDataUrl = "https://raw.githubusercontent.com/PicciMario/TCodeHelperOverlay/refs/heads/master/data.json";
    private static readonly TimeSpan MaxCacheAge = TimeSpan.FromHours(24);
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(12) };

    private readonly string _cacheDirectory;
    private readonly string _cacheFilePath;
    private readonly string _timestampFilePath;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public DataCacheService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(localAppData, "TCodeLaunchpad", "Cache");
        _cacheFilePath = Path.Combine(_cacheDirectory, "data.json");
        _timestampFilePath = Path.Combine(_cacheDirectory, "data.downloaded-at-utc.txt");
    }

    public string CacheFilePath => _cacheFilePath;

    public async Task<DataCacheStatus> EnsureFreshAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_cacheDirectory);

            var hasCacheFile = File.Exists(_cacheFilePath);
            var downloadedAtUtc = ReadDownloadTimestampUtc();

            var needsDownload = forceRefresh ||
                !hasCacheFile ||
                !downloadedAtUtc.HasValue ||
                DateTimeOffset.UtcNow - downloadedAtUtc.Value > MaxCacheAge;

            var downloadAttempted = false;
            var downloadSucceeded = false;

            if (needsDownload)
            {
                downloadAttempted = true;
                downloadSucceeded = await TryDownloadCacheFileAsync(cancellationToken).ConfigureAwait(false);
                if (downloadSucceeded)
                {
                    downloadedAtUtc = ReadDownloadTimestampUtc();
                }
            }

            if (!File.Exists(_cacheFilePath))
            {
                throw new FileNotFoundException(
                    "Unable to retrieve transaction data and no local cache file is available.",
                    _cacheFilePath);
            }

            var effectiveDownloadedAtUtc = downloadedAtUtc ?? ReadFileWriteTimestampUtc(_cacheFilePath);

            var age = DateTimeOffset.UtcNow - effectiveDownloadedAtUtc;
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            return new DataCacheStatus(
                _cacheFilePath,
                effectiveDownloadedAtUtc,
                age,
                downloadAttempted,
                downloadSucceeded);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<bool> TryDownloadCacheFileAsync(CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(_cacheDirectory, $"data.{Guid.NewGuid():N}.tmp");
        try
        {
            using var response = await HttpClient.GetAsync(RemoteDataUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            await File.WriteAllTextAsync(tempPath, payload, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _cacheFilePath, true);

            var now = DateTimeOffset.UtcNow;
            await File.WriteAllTextAsync(_timestampFilePath, now.ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private DateTimeOffset? ReadDownloadTimestampUtc()
    {
        if (File.Exists(_timestampFilePath))
        {
            var text = File.ReadAllText(_timestampFilePath).Trim();
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        if (File.Exists(_cacheFilePath))
        {
            return ReadFileWriteTimestampUtc(_cacheFilePath);
        }

        return null;
    }

    private static DateTimeOffset ReadFileWriteTimestampUtc(string path)
    {
        var utcDateTime = File.GetLastWriteTimeUtc(path);
        return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
    }
}
