using GameRunnerSimulator.Core;
using GameRunnerSimulator.Ui;

namespace GameRunnerSimulator;

static class Program
{
    [STAThread]
    static int Main(string[] argv)
    {
        var args = CliArgs.Parse(argv);

        // modos sem janela (usados pelo CI)
        if (args.Value("--version-file") is { } vf) { File.WriteAllText(vf, AppInfo.VersionText); return 0; }
        if (args.Value("--selftest-catalog") is { } cf) return SelfTest.Catalog(cf);
        if (args.Value("--selftest-update") is { } uf) return SelfTest.Update(uf);

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // processo de simulação: este .exe copiado com o nome do jogo
        if (args.Has("--child"))
        {
            var name = args.Value("--name") ?? Path.GetFileNameWithoutExtension(AppInfo.ExePath);
            Application.Run(new ChildForm(args.Value("--id") ?? "", name, args.Value("--icon"), args.Value("--shot")));
            return 0;
        }

        var shot = args.Value("--screenshot");
        using var mutex = new Mutex(true, "GameRunnerSimulator.Main.v2", out bool owner);
        if (!owner && shot is null)
        {
            // depois de atualizar, a versão nova espera a antiga fechar
            if (args.Has("--restarted"))
            {
                try { owner = mutex.WaitOne(TimeSpan.FromSeconds(15)); }
                catch (AbandonedMutexException) { owner = true; }
            }
            if (!owner)
            {
                MessageBox.Show("O GameRunner já está aberto.", "GameRunner Simulator",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
        }

        var form = new MainForm(shot);
        Application.Run(form);
        return form.ExitCode;
    }
}

sealed class CliArgs
{
    readonly Dictionary<string, string?> map = new(StringComparer.OrdinalIgnoreCase);

    public static CliArgs Parse(string[] argv)
    {
        var a = new CliArgs();
        for (int i = 0; i < argv.Length; i++)
        {
            if (!argv[i].StartsWith("--")) continue;
            string? value = i + 1 < argv.Length && !argv[i + 1].StartsWith("--") ? argv[++i] : null;
            a.map[argv[i]] = value;
        }
        return a;
    }

    public bool Has(string key) => map.ContainsKey(key);
    public string? Value(string key) => map.TryGetValue(key, out var v) ? v : null;
}
