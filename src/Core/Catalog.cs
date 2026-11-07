using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameRunnerSimulator.Core;

public sealed class Game
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public List<string> Aliases { get; set; } = new();
    public List<GameExe> Exes { get; set; } = new();

    [JsonIgnore] internal string NameKey { get; set; } = "";
    [JsonIgnore] internal string SearchKey { get; set; } = "";

    /// <summary>Executável sugerido: o primeiro que não é launcher.</summary>
    [JsonIgnore] public GameExe MainExe => Exes.FirstOrDefault(e => !e.Launcher) ?? Exes[0];
}

public sealed class GameExe
{
    /// <summary>Caminho como o Discord publica, com pasta quando houver (ex.: "_retail_/wow.exe").</summary>
    public string Name { get; set; } = "";
    public bool Launcher { get; set; }

    [JsonIgnore] public string FileName => Name.Contains('/') ? Name[(Name.LastIndexOf('/') + 1)..] : Name;
    [JsonIgnore] public string Folder => Name.Contains('/') ? Name[..(Name.LastIndexOf('/') + 1)] : "";
}

public enum CatalogSource { Cache, Discord, LocalFile }

public sealed class CatalogResult
{
    public required List<Game> Games { get; init; }
    public required DateTime Updated { get; init; }
    public required CatalogSource Source { get; init; }
}

/// <summary>Lista de jogos detectáveis do Discord: baixa, enxuga, guarda e busca.</summary>
public static class Catalog
{
    const string Url = "https://discord.com/api/v9/applications/detectable";

    static readonly JsonSerializerOptions ReadOpt = new() { PropertyNameCaseInsensitive = true };
    static readonly JsonSerializerOptions WriteOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    sealed class CacheFile
    {
        public DateTime Updated { get; set; }
        public List<Game> Games { get; set; } = new();
    }

    sealed class RawApp
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("icon_hash")] public string? IconHash { get; set; }
        [JsonPropertyName("aliases")] public List<string>? Aliases { get; set; }
        [JsonPropertyName("executables")] public List<RawExe>? Executables { get; set; }
    }

    sealed class RawExe
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("os")] public string? Os { get; set; }
        [JsonPropertyName("is_launcher")] public bool IsLauncher { get; set; }
    }

    /// <summary>Lista salva da última vez (abre na hora, mesmo sem internet).</summary>
    public static CatalogResult? LoadCached()
    {
        try
        {
            if (!File.Exists(AppInfo.CatalogPath)) return null;
            using var fs = File.OpenRead(AppInfo.CatalogPath);
            var c = JsonSerializer.Deserialize<CacheFile>(fs, WriteOpt);
            if (c is null || c.Games.Count == 0) return null;
            Index(c.Games);
            return new CatalogResult { Games = c.Games, Updated = c.Updated, Source = CatalogSource.Cache };
        }
        catch { return null; }
    }

    /// <summary>Baixa a lista atual do Discord e guarda a versão enxuta.</summary>
    public static async Task<CatalogResult> FetchAsync(CancellationToken outer)
    {
        // o Timeout do HttpClient não cobre o corpo lido em stream: prazo próprio para o pedido inteiro
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(outer);
        limit.CancelAfter(TimeSpan.FromSeconds(90));
        var ct = limit.Token;
        using var resp = await AppInfo.Http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var s = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var raw = await JsonSerializer.DeserializeAsync<List<RawApp>>(s, ReadOpt, ct).ConfigureAwait(false) ?? new();
        var games = Compact(raw);
        if (games.Count < 100) throw new InvalidDataException($"a lista do Discord veio com só {games.Count} jogos");
        var now = DateTime.Now;
        Save(games, now);
        return new CatalogResult { Games = games, Updated = now, Source = CatalogSource.Discord };
    }

    /// <summary>Compatibilidade com a versão antiga: detectable.json ao lado do .exe.</summary>
    public static CatalogResult? LoadLocalFile()
    {
        try
        {
            var path = Path.Combine(Path.GetDirectoryName(AppInfo.ExePath)!, "detectable.json");
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            var raw = JsonSerializer.Deserialize<List<RawApp>>(fs, ReadOpt) ?? new();
            var games = Compact(raw);
            if (games.Count == 0) return null;
            return new CatalogResult { Games = games, Updated = File.GetLastWriteTime(path), Source = CatalogSource.LocalFile };
        }
        catch { return null; }
    }

    static List<Game> Compact(List<RawApp> raw)
    {
        var games = new List<Game>(raw.Count / 2);
        foreach (var r in raw)
        {
            if (string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Name) || r.Executables is null) continue;
            var exes = new List<GameExe>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in r.Executables)
            {
                if (e.Os != "win32" || string.IsNullOrWhiteSpace(e.Name)) continue;
                var n = e.Name.Trim().TrimStart('>').Replace('\\', '/').Trim('/');
                if (!n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (seen.Add(n)) exes.Add(new GameExe { Name = n, Launcher = e.IsLauncher });
            }
            if (exes.Count == 0) continue;
            games.Add(new Game
            {
                Id = r.Id.Trim(),
                Name = r.Name.Trim(),
                Icon = string.IsNullOrWhiteSpace(r.IconHash) ? null : r.IconHash,
                Aliases = r.Aliases?.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList() ?? new(),
                Exes = exes.OrderBy(x => x.Launcher).ToList()
            });
        }
        games.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        Index(games);
        return games;
    }

    static void Save(List<Game> games, DateTime when)
    {
        Directory.CreateDirectory(AppInfo.DataDir);
        var tmp = AppInfo.CatalogPath + ".tmp";
        using (var fs = File.Create(tmp))
            JsonSerializer.Serialize(fs, new CacheFile { Updated = when, Games = games }, WriteOpt);
        File.Move(tmp, AppInfo.CatalogPath, overwrite: true);
    }

    static void Index(List<Game> games)
    {
        foreach (var g in games)
        {
            g.NameKey = Normalize(g.Name);
            var sb = new StringBuilder(g.NameKey);
            foreach (var a in g.Aliases) sb.Append('\n').Append(Normalize(a));
            foreach (var e in g.Exes) sb.Append('\n').Append(e.FileName.ToLowerInvariant());
            g.SearchKey = sb.ToString();
        }
    }

    /// <summary>Busca sem acento e sem caixa; quem começa com o termo vem primeiro.</summary>
    public static List<Game> Filter(List<Game> all, string query)
    {
        var q = Normalize(query.Trim());
        if (q.Length == 0) return all;
        var starts = new List<Game>();
        var contains = new List<Game>();
        foreach (var g in all)
        {
            if (g.NameKey.StartsWith(q, StringComparison.Ordinal)) starts.Add(g);
            else if (g.SearchKey.Contains(q, StringComparison.Ordinal)) contains.Add(g);
        }
        starts.AddRange(contains);
        return starts;
    }

    public static string Normalize(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
