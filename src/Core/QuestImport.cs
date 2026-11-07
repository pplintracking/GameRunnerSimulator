using System.Text.Json;

namespace GameRunnerSimulator.Core;

/// <summary>Uma Quest de "jogue por N min" lida da resposta do Discord.</summary>
public sealed class QuestEntry
{
    public required string AppId { get; init; }
    public required string Name { get; init; }
    public required int Minutes { get; init; }
}

/// <summary>O que saiu do texto colado da página de missões do Discord.</summary>
public sealed class QuestImportResult
{
    public List<QuestEntry> Play { get; } = new();   // missões de jogar no PC
    public int Total { get; set; }                   // quests vistas
    public int NonPlay { get; set; }                 // vídeo, stream, pré-compra: o app não cumpre
    public string? Error { get; set; }
}

/// <summary>
/// Lê a lista de Quests que o usuário copia da aba Network do Discord (a resposta de
/// "quests/@me"). Não fala com o Discord: só interpreta o texto colado. Em cada quest acha o
/// aplicativo (id + nome) e a tarefa PLAY_ON_DESKTOP com o tempo-alvo em segundos.
/// </summary>
public static class QuestImport
{
    public static QuestImportResult Parse(string text)
    {
        var r = new QuestImportResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            r.Error = "Cole aqui o texto que você copiou do Discord.";
            return r;
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch
        {
            r.Error = "Não entendi o texto. Copie a resposta de \"quests/@me\" " +
                      "(botão direito → Copy → Copy response) e cole inteira.";
            return r;
        }

        using (doc)
        {
            foreach (var quest in Quests(doc.RootElement))
            {
                r.Total++;
                if (!FindApplication(quest, out var id, out var name)) continue;  // sem aplicativo: ignora
                int seconds = FindPlaySeconds(quest);
                if (seconds <= 0) { r.NonPlay++; continue; }
                int minutes = Math.Clamp((seconds + 59) / 60, 1, 1440);

                int at = r.Play.FindIndex(e => e.AppId == id);
                if (at >= 0)
                {
                    if (r.Play[at].Minutes < minutes)
                        r.Play[at] = new QuestEntry { AppId = id, Name = name, Minutes = minutes };
                }
                else r.Play.Add(new QuestEntry { AppId = id, Name = name, Minutes = minutes });
            }
        }

        if (r.Total == 0)
            r.Error = "Não achei nenhuma missão no texto. Confira se copiou a resposta certa (\"quests/@me\").";
        return r;
    }

    /// <summary>A lista pode vir como {quests:[...]}, como [...] ou como uma quest só.</summary>
    static IEnumerable<JsonElement> Quests(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("quests", out var q) && q.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in q.EnumerateArray()) yield return e;
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in root.EnumerateArray()) yield return e;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root;
        }
    }

    /// <summary>Acha o objeto "application" (id + nome) em qualquer nível da quest.</summary>
    static bool FindApplication(JsonElement el, out string id, out string name)
    {
        id = "";
        name = "";
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("application", out var app) && app.ValueKind == JsonValueKind.Object &&
                app.TryGetProperty("id", out var aid) && aid.ValueKind == JsonValueKind.String)
            {
                id = aid.GetString() ?? "";
                name = app.TryGetProperty("name", out var an) && an.ValueKind == JsonValueKind.String
                    ? an.GetString() ?? "" : "";
                if (id.Length > 0) return true;
            }
            foreach (var p in el.EnumerateObject())
                if (FindApplication(p.Value, out id, out name)) return true;
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in el.EnumerateArray())
                if (FindApplication(e, out id, out name)) return true;
        }
        id = "";
        name = "";
        return false;
    }

    /// <summary>Procura a tarefa PLAY_ON_DESKTOP e devolve o alvo em segundos; 0 se não houver.</summary>
    static int FindPlaySeconds(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                if (p.Name.StartsWith("PLAY_ON_DESKTOP", StringComparison.OrdinalIgnoreCase))
                {
                    int t = Target(p.Value);
                    if (t > 0) return t;
                }
                // formato {event_name:"PLAY_ON_DESKTOP", target:900}
                if (p.Name.Equals("event_name", StringComparison.OrdinalIgnoreCase) &&
                    p.Value.ValueKind == JsonValueKind.String &&
                    (p.Value.GetString() ?? "").StartsWith("PLAY_ON_DESKTOP", StringComparison.OrdinalIgnoreCase))
                {
                    int t = Target(el);
                    if (t > 0) return t;
                }
                int inner = FindPlaySeconds(p.Value);
                if (inner > 0) return inner;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in el.EnumerateArray())
            {
                int t = FindPlaySeconds(e);
                if (t > 0) return t;
            }
        }
        return 0;

        static int Target(JsonElement obj)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty("target", out var tg))
            {
                if (tg.ValueKind == JsonValueKind.Number && tg.TryGetInt32(out var n)) return n;
                if (tg.ValueKind == JsonValueKind.String && int.TryParse(tg.GetString(), out var s)) return s;
            }
            return 0;
        }
    }
}
