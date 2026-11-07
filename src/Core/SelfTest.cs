using System.Text;

namespace GameRunnerSimulator.Core;

/// <summary>Testes sem janela, rodados pelo CI no Windows. Escrevem o resultado num arquivo.</summary>
static class SelfTest
{
    const string OverwatchId = "356875221078245376";

    public static int Catalog(string outFile)
    {
        var sb = new StringBuilder();
        try
        {
            var r = Core.Catalog.FetchAsync(CancellationToken.None).GetAwaiter().GetResult();
            sb.AppendLine($"jogos={r.Games.Count}");
            sb.AppendLine($"exes={r.Games.Sum(g => g.Exes.Count)} com_pasta={r.Games.Sum(g => g.Exes.Count(e => e.Name.Contains('/')))}");
            var ow = r.Games.FirstOrDefault(g => g.Id == OverwatchId);
            sb.AppendLine($"overwatch={ow?.Name} exe={ow?.MainExe.Name} icone={ow?.Icon}");
            sb.AppendLine($"cache_bytes={new FileInfo(AppInfo.CatalogPath).Length}");
            var c = Core.Catalog.LoadCached();
            sb.AppendLine($"cache_jogos={c?.Games.Count}");
            var busca = Core.Catalog.Filter(c?.Games ?? r.Games, "overwatch");
            sb.AppendLine($"busca_overwatch={busca.FirstOrDefault()?.Name} total={busca.Count}");
            var acento = Core.Catalog.Filter(c?.Games ?? r.Games, "pokémon");
            sb.AppendLine($"busca_pokemon_com_acento={acento.Count}");
            sb.AppendLine($"caminho={Simulator.SafeRelative("_retail_/wow.exe")} malicioso={Simulator.SafeRelative("../../x/..\\y.exe")}");
            File.WriteAllText(outFile, sb.ToString());
            bool ok = r.Games.Count > 1000 && c?.Games.Count == r.Games.Count && ow is not null
                      && busca.FirstOrDefault()?.Id == OverwatchId;
            return ok ? 0 : 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(outFile, sb + "ERRO " + ex);
            return 1;
        }
    }

    public static int Update(string outFile)
    {
        var r = Updater.RunAsync(null, CancellationToken.None).GetAwaiter().GetResult();
        File.WriteAllText(outFile, $"antes={AppInfo.VersionText} estado={r.State} ultima={r.Latest} erro={r.Error}");
        return r.State switch
        {
            UpdateState.Ready => 0,
            UpdateState.UpToDate => 10,
            _ => 1
        };
    }
}
