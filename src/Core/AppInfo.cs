using System.Net;

namespace GameRunnerSimulator.Core;

static class AppInfo
{
    /// <summary>Repositório de onde saem as atualizações (aba Releases).</summary>
    public const string Repo = "pplintracking/GameRunnerSimulator";
    public const string AssetName = "GameRunnerSimulator.exe";

    public static Version Version { get; } = Normalize(typeof(AppInfo).Assembly.GetName().Version);
    public static string VersionText => $"{Version.Major}.{Version.Minor}.{Version.Build}";
    public static bool IsDevBuild => Version == new Version(0, 0, 0);

    public static string ExePath { get; } = Environment.ProcessPath ?? Application.ExecutablePath;

    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameRunnerSimulator");
    public static string SimsDir => Path.Combine(DataDir, "sims");
    public static string IconsDir => Path.Combine(DataDir, "icons");
    public static string CatalogPath => Path.Combine(DataDir, "games.json");

    public static Version Normalize(Version? v) =>
        v is null ? new Version(0, 0, 0) : new Version(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    static readonly Lazy<HttpClient> http = new(() =>
    {
        var h = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
        h.DefaultRequestHeaders.UserAgent.ParseAdd($"GameRunnerSimulator/{VersionText}");
        return h;
    });

    public static HttpClient Http => http.Value;
}
