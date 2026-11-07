namespace GameRunnerSimulator.Core;

/// <summary>
/// Ícones oficiais do Discord (cdn.discordapp.com/app-icons), baixados sob demanda, guardados em disco
/// e mantidos na memória com limite. Só é chamado da thread da interface.
/// </summary>
public sealed class IconCache : IDisposable
{
    const int Capacity = 700;
    readonly Control owner;
    readonly Dictionary<string, Image?> mem = new();
    readonly LinkedList<string> order = new();
    readonly HashSet<string> pending = new();
    readonly SemaphoreSlim gate = new(6);

    public event Action? Loaded;

    public IconCache(Control owner) => this.owner = owner;

    public static string DiskPath(string id, string hash, int size) =>
        Path.Combine(AppInfo.IconsDir, $"{id}_{hash}_{size}.png");

    public Image? Get(Game game, int size)
    {
        if (game.Icon is null) return null;
        var key = $"{game.Id}_{game.Icon}_{size}";
        if (mem.TryGetValue(key, out var img))
        {
            order.Remove(key);
            order.AddFirst(key);
            return img;
        }
        if (pending.Add(key)) _ = LoadAsync(game.Id, game.Icon, size, key);
        return null;
    }

    /// <summary>Leitura direta do disco, para a janela da simulação.</summary>
    public static Image? FromDisk(string id, string? hash)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(hash)) return null;
        foreach (var size in new[] { 128, 64 })
        {
            var path = DiskPath(id, hash, size);
            if (!File.Exists(path)) continue;
            try { return Decode(File.ReadAllBytes(path)); } catch { }
        }
        return null;
    }

    async Task LoadAsync(string id, string hash, int size, string key)
    {
        Image? img = null;
        try
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var path = DiskPath(id, hash, size);
                byte[] bytes;
                if (File.Exists(path)) bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
                else
                {
                    var url = $"https://cdn.discordapp.com/app-icons/{id}/{hash}.png?size={size}";
                    bytes = await AppInfo.Http.GetByteArrayAsync(url).ConfigureAwait(false);
                    Directory.CreateDirectory(AppInfo.IconsDir);
                    await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                }
                img = Decode(bytes);
            }
            finally { gate.Release(); }
        }
        catch { img = null; }

        if (owner.IsDisposed || !owner.IsHandleCreated) { img?.Dispose(); return; }
        try
        {
            owner.BeginInvoke((MethodInvoker)(() =>
            {
                pending.Remove(key);
                Put(key, img);
                Loaded?.Invoke();
            }));
        }
        catch { img?.Dispose(); }
    }

    static Image Decode(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var src = Image.FromStream(ms);
        return new Bitmap(src);
    }

    void Put(string key, Image? img)
    {
        mem[key] = img;
        order.AddFirst(key);
        while (order.Count > Capacity)
        {
            var last = order.Last!.Value;
            order.RemoveLast();
            if (mem.Remove(last, out var old)) old?.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var i in mem.Values) i?.Dispose();
        mem.Clear();
        order.Clear();
    }
}
