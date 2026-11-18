using System.Net.Http.Headers;
using System.Text.Json;

namespace CteLogLife.Core;

public sealed record UpdateInfo(bool UpdateAvailable, string CurrentVersion, string LatestVersion,
    string? DownloadUrl, string ReleaseNotes, string ReleaseName);

/// <summary>Looks for a newer release on GitHub and downloads its installer.</summary>
public sealed class UpdateChecker : IDisposable
{
    public const string RepoOwner = "ArturSenna";
    public const string RepoName = "cte-loglife-automation";

    private readonly HttpClient _http;
    private readonly string _currentVersion;
    private readonly string _latestUrl;

    public UpdateChecker(string currentVersion, HttpMessageHandler? handler = null,
        string owner = RepoOwner, string repo = RepoName)
    {
        _currentVersion = currentVersion;
        _latestUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CTe-LogLife-Updater", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public void Dispose() => _http.Dispose();

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await _http.GetAsync(_latestUrl, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var release = document.RootElement;

        var latest = release.Str("tag_name").TrimStart('v', 'V');
        string? downloadUrl = null;
        var assets = release.Items("assets");
        foreach (var asset in assets)
        {
            var name = asset.Str("name").ToLowerInvariant();
            if (name.EndsWith(".exe") && name.Contains("setup") || name.Contains("installer"))
            {
                downloadUrl = asset.Str("browser_download_url");
                break;
            }
        }
        downloadUrl ??= assets.Where(a => a.Str("name").ToLowerInvariant().EndsWith(".exe"))
            .Select(a => a.Str("browser_download_url")).FirstOrDefault();

        return new UpdateInfo(IsNewer(latest, _currentVersion), _currentVersion, latest, downloadUrl,
            release.Str("body", "Sem notas de versão disponíveis."), release.Str("name", "Nova versão"));
    }

    /// <summary>True when <paramref name="candidate"/> is a higher version than <paramref name="current"/>.</summary>
    public static bool IsNewer(string candidate, string current) =>
        Parse(candidate) is { } latest && Parse(current) is { } mine && latest > mine;

    private static Version? Parse(string text)
    {
        var clean = new string(text.Trim().TrimStart('v', 'V').TakeWhile(c => char.IsAsciiDigit(c) || c == '.').ToArray())
            .Trim('.');
        if (clean.Length == 0) return null;
        if (!clean.Contains('.')) clean += ".0";
        return Version.TryParse(clean, out var version) ? version : null;
    }

    /// <summary>Downloads the installer to the temp folder and returns its path.</summary>
    public async Task<string> DownloadInstallerAsync(string url, IProgress<int>? progress, CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), "CTe_LogLife_Setup.exe");
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;
        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var file = File.Create(path))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report((int)(done * 100 / total));
            }
        }
        // An installer smaller than 1 MB is an error page, not an installer.
        if (new FileInfo(path).Length < 1024 * 1024)
            throw new InvalidDataException("O arquivo baixado não parece ser um instalador válido.");
        return path;
    }
}
