using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace GameRunnerSimulator.Core;

public enum UpdateState { Idle, Disabled, Checking, UpToDate, Downloading, Ready, ManualOnly, Failed }

public sealed class UpdateInfo
{
    public UpdateState State { get; init; }
    public Version? Latest { get; init; }
    public string? PageUrl { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Atualização automática pela aba Releases do GitHub: baixa o .exe novo ao lado do atual, confere
/// tamanho e sha256, renomeia o atual para .old (o Windows deixa renomear um .exe em uso) e põe o novo
/// no lugar. A versão nova vale na próxima abertura, ou na hora, pelo botão «reiniciar».
/// </summary>
public static class Updater
{
    public static void CleanupOld()
    {
        try
        {
            var dir = Path.GetDirectoryName(AppInfo.ExePath)!;
            foreach (var f in Directory.GetFiles(dir, Path.GetFileName(AppInfo.ExePath) + "*.old"))
                try { File.Delete(f); } catch { /* ainda em uso por uma simulação antiga */ }
            var tmp = AppInfo.ExePath + ".new";
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        catch { }
    }

    public static async Task<UpdateInfo> RunAsync(IProgress<UpdateInfo>? progress, CancellationToken ct)
    {
        if (AppInfo.IsDevBuild) return new UpdateInfo { State = UpdateState.Disabled };
        progress?.Report(new UpdateInfo { State = UpdateState.Checking });

        Version latest;
        string? page, assetUrl = null, digest = null;
        long assetSize = -1;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{AppInfo.Repo}/releases/latest");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var resp = await AppInfo.Http.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound) return new UpdateInfo { State = UpdateState.UpToDate };
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed))
                return new UpdateInfo { State = UpdateState.Failed, Error = $"tag sem versão: {tag}" };
            latest = AppInfo.Normalize(parsed);
            page = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
            if (latest <= AppInfo.Version) return new UpdateInfo { State = UpdateState.UpToDate, Latest = latest };

            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                if (!string.Equals(a.GetProperty("name").GetString(), AppInfo.AssetName, StringComparison.OrdinalIgnoreCase)) continue;
                assetUrl = a.GetProperty("browser_download_url").GetString();
                assetSize = a.GetProperty("size").GetInt64();
                if (a.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String) digest = d.GetString();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new UpdateInfo { State = UpdateState.Failed, Error = ex.Message };
        }

        if (assetUrl is null) return new UpdateInfo { State = UpdateState.ManualOnly, Latest = latest, PageUrl = page };

        progress?.Report(new UpdateInfo { State = UpdateState.Downloading, Latest = latest });
        var target = AppInfo.ExePath;
        var tmp = target + ".new";
        try
        {
            using (var resp = await AppInfo.Http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var dst = File.Create(tmp);
                await src.CopyToAsync(dst, ct).ConfigureAwait(false);
            }

            var len = new FileInfo(tmp).Length;
            if (assetSize > 0 && len != assetSize) throw new InvalidDataException($"tamanho {len} ≠ {assetSize}");
            if (digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                string hash;
                await using (var fs = File.OpenRead(tmp))
                    hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false));
                if (!hash.Equals(digest[7..], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("sha256 não confere");
            }

            var old = $"{target}.{DateTime.Now:yyyyMMddHHmmss}.old";
            File.Move(target, old);
            try { File.Move(tmp, target); }
            catch { File.Move(old, target); throw; }
            return new UpdateInfo { State = UpdateState.Ready, Latest = latest, PageUrl = page };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            try { File.Delete(tmp); } catch { }
            // pasta sem permissão de escrita (ex.: Arquivos de Programas): só avisa e abre a página
            return new UpdateInfo { State = UpdateState.ManualOnly, Latest = latest, PageUrl = page, Error = ex.Message };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try { File.Delete(tmp); } catch { }
            return new UpdateInfo { State = UpdateState.Failed, Latest = latest, Error = ex.Message };
        }
    }

    public static void Restart()
    {
        var psi = new ProcessStartInfo(AppInfo.ExePath) { UseShellExecute = false };
        psi.ArgumentList.Add("--restarted");
        Process.Start(psi);
    }
}
