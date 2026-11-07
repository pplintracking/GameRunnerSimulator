using System.Text.Json;

namespace GameRunnerSimulator.Core;

/// <summary>Jogo marcado como missão: quanto tempo precisa rodar e quando cumpriu.</summary>
public sealed class Mission
{
    public string GameId { get; set; } = "";
    public int Minutes { get; set; } = Missions.DefaultMinutes;
    public DateTime? Done { get; set; }

    /// <summary>A simulação roda o tempo da missão mais 1 minuto de folga (o Discord leva alguns segundos para ver o jogo).</summary>
    public TimeSpan Target => TimeSpan.FromMinutes(Minutes + 1);
}

/// <summary>Missões marcadas, salvas em %LOCALAPPDATA%\GameRunnerSimulator\missoes.json.</summary>
public sealed class Missions
{
    public const int DefaultMinutes = 15;
    public static readonly int[] MinuteSteps = { 15, 30, 45, 60 };

    static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    readonly List<Mission> items;
    readonly string? file;

    public event Action? Changed;

    Missions(List<Mission> items, string? file)
    {
        this.items = items;
        this.file = file;
    }

    public IReadOnlyList<Mission> Items => items;
    public static string FilePath => Path.Combine(AppInfo.DataDir, "missoes.json");

    /// <summary>Lê do disco. Com <paramref name="persist"/> falso (print do CI) não grava nada.</summary>
    public static Missions Load(bool persist = true)
    {
        var list = new List<Mission>();
        if (persist)
        {
            try
            {
                if (File.Exists(FilePath))
                    list = JsonSerializer.Deserialize<List<Mission>>(File.ReadAllText(FilePath), Opt) ?? new();
            }
            catch { /* arquivo corrompido: começa vazio */ }
        }
        list.RemoveAll(m => string.IsNullOrWhiteSpace(m.GameId));
        foreach (var m in list) if (!MinuteSteps.Contains(m.Minutes)) m.Minutes = DefaultMinutes;
        return new Missions(list, persist ? FilePath : null);
    }

    public Mission? Find(string gameId) => items.FirstOrDefault(m => m.GameId == gameId);
    public bool IsMarked(string gameId) => Find(gameId) is not null;

    public Mission Mark(string gameId)
    {
        if (Find(gameId) is { } m) return m;
        m = new Mission { GameId = gameId };
        items.Add(m);
        Save();
        return m;
    }

    public void Toggle(string gameId)
    {
        if (Find(gameId) is { } m) Remove(m);
        else Mark(gameId);
    }

    public void Remove(Mission m)
    {
        if (items.Remove(m)) Save();
    }

    public void CycleMinutes(Mission m)
    {
        int i = Array.IndexOf(MinuteSteps, m.Minutes);
        m.Minutes = MinuteSteps[(i + 1) % MinuteSteps.Length];
        m.Done = null;
        Save();
    }

    public void MarkDone(Mission m)
    {
        m.Done = DateTime.Now;
        Save();
    }

    public void Reset(Mission m)
    {
        if (m.Done is null) return;
        m.Done = null;
        Save();
    }

    void Save()
    {
        if (file is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                var tmp = file + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(items, Opt));
                File.Move(tmp, file, overwrite: true);
            }
            catch { /* sem disco: segue só na memória */ }
        }
        Changed?.Invoke();
    }
}
