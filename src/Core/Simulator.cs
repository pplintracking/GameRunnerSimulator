using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameRunnerSimulator.Core;

public sealed class Simulation
{
    public required Game Game { get; init; }
    public required GameExe Exe { get; init; }
    public required Process Process { get; init; }
    public required string Folder { get; init; }
    public required DateTime Started { get; init; }
    public TimeSpan Elapsed => DateTime.Now - Started;
}

/// <summary>
/// Liga e desliga simulações. Cada uma é este mesmo .exe, ligado (hard link) ou copiado para
/// %LOCALAPPDATA%\GameRunnerSimulator\sims\&lt;id do jogo&gt;\&lt;caminho do Discord&gt;, e iniciado com --child.
/// </summary>
public sealed class Simulator
{
    readonly List<Simulation> active = new();
    readonly SynchronizationContext ui;

    /// <summary>Disparado na thread da interface sempre que a lista de simulações muda.</summary>
    public event Action? Changed;

    public Simulator()
    {
        ui = SynchronizationContext.Current ?? new SynchronizationContext();
    }

    public IReadOnlyList<Simulation> Active => active;
    public Simulation? Find(string gameId) => active.FirstOrDefault(s => s.Game.Id == gameId);

    public Simulation Start(Game game, GameExe exe)
    {
        if (Find(game.Id) is { } running) return running;

        var folder = Path.Combine(AppInfo.SimsDir, game.Id);
        var dest = Path.Combine(folder, SafeRelative(exe.Name));
        var destDir = Path.GetDirectoryName(dest)!;
        Directory.CreateDirectory(destDir);
        try { if (File.Exists(dest)) File.Delete(dest); } catch { /* em uso: reaproveita */ }
        if (!File.Exists(dest)) LinkOrCopy(AppInfo.ExePath, dest);

        var psi = new ProcessStartInfo(dest) { UseShellExecute = false, WorkingDirectory = destDir };
        psi.ArgumentList.Add("--child");
        psi.ArgumentList.Add("--id");
        psi.ArgumentList.Add(game.Id);
        psi.ArgumentList.Add("--name");
        psi.ArgumentList.Add(game.Name);
        if (game.Icon is not null)
        {
            psi.ArgumentList.Add("--icon");
            psi.ArgumentList.Add(game.Icon);
        }

        var p = Process.Start(psi) ?? throw new InvalidOperationException("o Windows não iniciou o processo");
        var sim = new Simulation { Game = game, Exe = exe, Process = p, Folder = folder, Started = DateTime.Now };
        Track(sim);
        return sim;
    }

    public void Stop(Simulation sim)
    {
        try
        {
            if (!sim.Process.HasExited) sim.Process.Kill();
        }
        catch { /* já saiu */ }
    }

    public void StopAll()
    {
        foreach (var s in active.ToList()) Stop(s);
    }

    /// <summary>Recupera simulações que ficaram abertas de uma sessão anterior e limpa pastas órfãs.</summary>
    public void Adopt(IReadOnlyList<Game> games)
    {
        if (!Directory.Exists(AppInfo.SimsDir)) return;
        var byId = new Dictionary<string, Game>();
        foreach (var g in games) byId.TryAdd(g.Id, g);
        var root = Path.GetFullPath(AppInfo.SimsDir).TrimEnd('\\') + "\\";

        foreach (var dir in Directory.GetDirectories(AppInfo.SimsDir))
        {
            var id = Path.GetFileName(dir);
            if (Find(id) is not null) continue;
            bool alive = false;
            foreach (var file in SafeFiles(dir))
            {
                foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(file)))
                {
                    string? path = null;
                    try { path = p.MainModule?.FileName; } catch { }
                    if (path is null || !path.Equals(file, StringComparison.OrdinalIgnoreCase)) { p.Dispose(); continue; }
                    alive = true;
                    if (!byId.TryGetValue(id, out var game)) continue;
                    var rel = path[root.Length..][(id.Length + 1)..].Replace('\\', '/');
                    var exe = game.Exes.FirstOrDefault(e => SafeRelative(e.Name).Replace('\\', '/')
                        .Equals(rel, StringComparison.OrdinalIgnoreCase)) ?? new GameExe { Name = rel };
                    DateTime started;
                    try { started = p.StartTime; } catch { started = DateTime.Now; }
                    Track(new Simulation { Game = game, Exe = exe, Process = p, Folder = dir, Started = started });
                    break;
                }
                if (alive) break;
            }
            if (!alive) TryDeleteFolder(dir);
        }
    }

    void Track(Simulation sim)
    {
        active.Add(sim);
        sim.Process.EnableRaisingEvents = true;
        sim.Process.Exited += (_, _) => ui.Post(state =>
        {
            if (!active.Remove(sim)) return;
            _ = Task.Run(() => CleanupLater(sim.Folder));
            Changed?.Invoke();
        }, null);
        Changed?.Invoke();
        if (sim.Process.HasExited && active.Contains(sim))
        {
            active.Remove(sim);
            Changed?.Invoke();
        }
    }

    static async Task CleanupLater(string folder)
    {
        for (int i = 0; i < 10; i++)
        {
            if (TryDeleteFolder(folder)) return;
            await Task.Delay(300).ConfigureAwait(false);
        }
    }

    static bool TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            return true;
        }
        catch { return false; }
    }

    static IEnumerable<string> SafeFiles(string dir)
    {
        try { return Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories); }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>"_retail_/wow.exe" → "_retail_\wow.exe", sem "..", sem raiz e sem caractere inválido.</summary>
    public static string SafeRelative(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var parts = name.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => new string(s.Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.'))
            .Where(s => s.Length > 0 && s != "." && s != "..")
            .ToArray();
        if (parts.Length == 0) throw new InvalidOperationException($"nome de executável inválido: {name}");
        return Path.Combine(parts);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    /// <summary>Hard link não ocupa espaço; em outro disco, cai para cópia.</summary>
    static void LinkOrCopy(string source, string dest)
    {
        if (!CreateHardLink(dest, source, IntPtr.Zero)) File.Copy(source, dest, overwrite: true);
    }
}
