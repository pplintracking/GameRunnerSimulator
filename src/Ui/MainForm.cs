using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using GameRunnerSimulator.Core;

namespace GameRunnerSimulator.Ui;

sealed class MainForm : Form
{
    static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    const string OverwatchId = "356875221078245376";

    readonly IconCache icons;
    readonly Simulator simulator;
    readonly SearchBox search = new();
    readonly GameListView gameList;
    readonly DetailPanel detail;
    readonly ActiveSimsView activeList;
    readonly System.Windows.Forms.Timer searchDelay = new() { Interval = 120 };
    readonly System.Windows.Forms.Timer tick = new() { Interval = 1000 };
    readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    readonly CancellationTokenSource closing = new();
    readonly string? screenshotPath;
    readonly bool ready;

    List<Game> games = new();
    List<Game> filtered = new();
    CatalogResult? catalog;
    bool fetching, adopted, updating, restarting;
    UpdateInfo update = new() { State = AppInfo.IsDevBuild ? UpdateState.Disabled : UpdateState.Idle };
    Rectangle catalogLink, updateLink;

    public int ExitCode { get; private set; }

    int S(int v) => LogicalToDeviceUnits(v);
    int LeftW => Math.Clamp((int)(ClientSize.Width * 0.37), S(330), S(420));
    int FooterH => S(34);
    const int ListTop = 146;

    public MainForm(string? screenshotPath)
    {
        this.screenshotPath = screenshotPath;
        Text = "GameRunner Simulator";
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Bg2;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        DoubleBuffered = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(S(1080), S(700));
        MinimumSize = SizeFromClientSize(new Size(S(900), S(600)));
        Icon = Theme.AppIcon();

        icons = new IconCache(this);
        simulator = new Simulator();
        gameList = new GameListView(icons, g => simulator.Find(g.Id) is not null)
        {
            EmptyText = "Baixando a lista de jogos do Discord…"
        };
        detail = new DetailPanel(icons);
        activeList = new ActiveSimsView(icons);
        Controls.AddRange(new Control[] { search, gameList, detail, activeList });

        icons.Loaded += () => { gameList.Invalidate(); detail.Invalidate(); activeList.Invalidate(); };
        simulator.Changed += OnSimsChanged;
        search.QueryChanged += (_, _) => { searchDelay.Stop(); searchDelay.Start(); };
        search.BoxKeyDown += OnSearchKey;
        searchDelay.Tick += (_, _) => { searchDelay.Stop(); ApplyFilter(resetScroll: true); };
        gameList.SelectionChanged += (_, _) => ShowSelected();
        gameList.ItemActivated += (_, _) => ToggleSelected();
        gameList.TypeAhead += c => search.Append(c);
        detail.SimulateClicked += (_, _) => ToggleSelected();
        activeList.StopRequested += sim => simulator.Stop(sim);
        activeList.OpenRequested += Reveal;
        tick.Tick += (_, _) => { if (activeList.Count > 0) activeList.Invalidate(); };
        tick.Start();
        updateTimer.Tick += async (_, _) => await CheckUpdateAsync();

        ready = true;
        DoLayout();
    }

    // ---------------- ciclo de vida ----------------

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(Handle, Theme.Bg0);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        search.FocusBox();
        var cached = await Task.Run(Catalog.LoadCached);
        if (IsDisposed) return;
        if (cached is not null) ApplyCatalog(cached);
        await RefreshCatalogAsync();
        if (IsDisposed) return;

        if (screenshotPath is not null) { await TakeScreenshotAsync(screenshotPath); return; }

        Updater.CleanupOld();
        await CheckUpdateAsync();
        updateTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (screenshotPath is null && !restarting && e.CloseReason == CloseReason.UserClosing && simulator.Active.Count > 0)
        {
            int n = simulator.Active.Count;
            var r = MessageBox.Show(this,
                (n == 1 ? "Tem 1 simulação rodando." : $"Tem {n} simulações rodando.") +
                "\n\nSim: parar e fechar.\nNão: fechar e deixar rodando.",
                "GameRunner Simulator", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) { e.Cancel = true; return; }
            if (r == DialogResult.Yes) simulator.StopAll();
        }
        closing.Cancel();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        tick.Stop();
        updateTimer.Stop();
        icons.Dispose();
        base.OnFormClosed(e);
    }

    // ---------------- lista de jogos ----------------

    async Task RefreshCatalogAsync()
    {
        fetching = true;
        Invalidate();
        try
        {
            var fresh = await Task.Run(() => Catalog.FetchAsync(closing.Token));
            if (IsDisposed) return;
            ApplyCatalog(fresh);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested) { return; }
        catch
        {
            if (catalog is null && Catalog.LoadLocalFile() is { } local) ApplyCatalog(local);
            if (catalog is null) gameList.EmptyText = "Sem conexão e sem lista salva.\nClica em «Tentar de novo», no rodapé.";
        }
        finally
        {
            fetching = false;
            if (!IsDisposed) { gameList.Invalidate(); Invalidate(); }
        }
    }

    void ApplyCatalog(CatalogResult r)
    {
        var selId = gameList.SelectedItem?.Id ?? detail.Game?.Id;
        catalog = r;
        games = r.Games;
        ApplyFilter(resetScroll: false);
        if (selId is not null && filtered.FirstOrDefault(g => g.Id == selId) is { } again) gameList.Select(again);
        gameList.EmptyText = "Nenhum jogo com esse nome.";
        if (!adopted)
        {
            adopted = true;
            try { simulator.Adopt(games); } catch { }
        }
        Invalidate();
    }

    void ApplyFilter(bool resetScroll)
    {
        filtered = Catalog.Filter(games, search.Query);
        gameList.SetItems(filtered, resetScroll);
        Invalidate(new Rectangle(0, S(110), LeftW, S(36)));
    }

    void ShowSelected()
    {
        if (gameList.SelectedItem is not { } g) return;
        detail.SetGame(g, simulator.Find(g.Id) is not null);
        DoLayout();
    }

    void Reveal(Simulation sim)
    {
        if (filtered.FirstOrDefault(g => g.Id == sim.Game.Id) is not { } g)
        {
            search.Query = "";
            ApplyFilter(resetScroll: false);
            g = filtered.FirstOrDefault(x => x.Id == sim.Game.Id);
        }
        if (g is not null) gameList.Select(g);
        else { detail.SetGame(sim.Game, true); DoLayout(); }
    }

    void OnSearchKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Down or Keys.Enter && filtered.Count > 0)
        {
            if (gameList.SelectedItem is null || e.KeyCode == Keys.Enter) gameList.Select(filtered[0]);
            gameList.Focus();
            e.SuppressKeyPress = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.F) { search.FocusBox(); e.SuppressKeyPress = true; }
        base.OnKeyDown(e);
    }

    // ---------------- simulações ----------------

    void ToggleSelected()
    {
        var g = detail.Game ?? gameList.SelectedItem;
        if (g is null) return;
        if (simulator.Find(g.Id) is { } running) { simulator.Stop(running); return; }
        var exe = detail.Game?.Id == g.Id ? detail.SelectedExe ?? g.MainExe : g.MainExe;
        try { simulator.Start(g, exe); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não deu para iniciar a simulação.\n\n" + ex.Message,
                "GameRunner Simulator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void OnSimsChanged()
    {
        activeList.SetItems(simulator.Active.ToList(), resetScroll: false);
        if (detail.Game is { } g) detail.SetActive(simulator.Find(g.Id) is not null);
        gameList.Invalidate();
        Invalidate(ActiveHeaderRect);
    }

    // ---------------- atualização automática ----------------

    async Task CheckUpdateAsync()
    {
        if (updating || update.State is UpdateState.Ready or UpdateState.Disabled) return;
        updating = true;
        try
        {
            var progress = new Progress<UpdateInfo>(u => { update = u; Invalidate(FooterRect); });
            update = await Task.Run(() => Updater.RunAsync(progress, closing.Token));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { update = new UpdateInfo { State = UpdateState.Failed, Error = ex.Message }; }
        finally
        {
            updating = false;
            if (!IsDisposed) Invalidate(FooterRect);
        }
    }

    // ---------------- layout e desenho ----------------

    Rectangle FooterRect => new(0, ClientSize.Height - FooterH, ClientSize.Width, FooterH);
    Rectangle ActiveHeaderRect => new(LeftW + S(32), detail.Bottom + S(30), ClientSize.Width - LeftW - S(64), S(16));

    void DoLayout()
    {
        if (!ready || ClientSize.Width == 0) return;
        int w = ClientSize.Width, h = ClientSize.Height, lw = LeftW, fh = FooterH;
        search.SetBounds(S(16), S(66), lw - S(32), S(40));
        gameList.SetBounds(0, S(ListTop), lw, h - fh - S(ListTop) - S(6));
        detail.SetBounds(lw, 0, w - lw, detail.DesiredHeight);
        var ah = ActiveHeaderRect;
        int top = ah.Bottom + S(8);
        activeList.SetBounds(lw + S(32), top, w - lw - S(64) + S(10), Math.Max(S(64), h - fh - top - S(12)));
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        DoLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.HighQuality(g);
        int lw = LeftW, bodyH = ClientSize.Height - FooterH;
        using (var b = new SolidBrush(Theme.Bg1)) g.FillRectangle(b, 0, 0, lw, bodyH);
        using (var b = new SolidBrush(Theme.Bg2)) g.FillRectangle(b, lw, 0, ClientSize.Width - lw, bodyH);

        // marca
        int ls = S(30);
        if (Theme.Logo is { } logo) g.DrawImage(logo, new Rectangle(S(18), S(19), ls, ls));
        var brand = new Rectangle(S(18) + ls + S(10), S(17), lw - ls - S(40), S(34));
        int w1 = Theme.Measure(g, "GameRunner", Theme.Brand).Width;
        Theme.DrawText(g, "GameRunner", Theme.Brand, Theme.Text, brand);
        Theme.DrawText(g, "Simulator", Theme.Brand, Theme.Faint,
            new Rectangle(brand.X + w1 + S(5), brand.Y, Math.Max(0, brand.Width - w1 - S(5)), brand.Height));

        // rótulo da lista
        if (games.Count > 0)
        {
            string label = search.Query.Trim().Length > 0
                ? $"RESULTADOS · {filtered.Count.ToString("N0", Pt)}"
                : $"TODOS OS JOGOS · {games.Count.ToString("N0", Pt)}";
            Theme.DrawText(g, label, Theme.Label, Theme.Faint, new Rectangle(S(20), S(120), lw - S(40), S(18)));
        }

        // cabeçalho das simulações ativas
        var ah = ActiveHeaderRect;
        using (var pen = new Pen(Theme.Line, 1)) g.DrawLine(pen, ah.X, ah.Y - S(16), ah.Right, ah.Y - S(16));
        Theme.DrawText(g, $"SIMULAÇÕES ATIVAS · {simulator.Active.Count}", Theme.Label, Theme.Faint, ah);

        PaintFooter(g);
    }

    void PaintFooter(Graphics g)
    {
        var f = FooterRect;
        using (var b = new SolidBrush(Theme.Bg0)) g.FillRectangle(b, f);
        int pad = S(16);

        // esquerda: estado da lista
        string left;
        string? link = null;
        if (catalog is null)
        {
            left = fetching ? "Baixando a lista de jogos do Discord…" : "Sem conexão e sem lista salva.";
            if (!fetching) link = "Tentar de novo";
        }
        else
        {
            var n = catalog.Games.Count.ToString("N0", Pt);
            left = fetching
                ? $"{n} jogos · atualizando a lista…"
                : catalog.Source switch
                {
                    CatalogSource.Discord => $"{n} jogos · lista do Discord atualizada {When(catalog.Updated)}",
                    CatalogSource.Cache => $"{n} jogos · lista salva {When(catalog.Updated)}, sem conexão",
                    _ => $"{n} jogos · detectable.json local, sem conexão"
                };
            if (!fetching && catalog.Source != CatalogSource.Discord) link = "Tentar de novo";
        }
        int lw = Theme.Measure(g, left, Theme.Small).Width;
        Theme.DrawText(g, left, Theme.Small, Theme.Faint, new Rectangle(f.X + pad, f.Y, lw + S(2), f.Height));
        catalogLink = Rectangle.Empty;
        if (link is not null)
        {
            int kw = Theme.Measure(g, link, Theme.Small).Width;
            catalogLink = new Rectangle(f.X + pad + lw + S(10), f.Y, kw + S(2), f.Height);
            Theme.DrawText(g, link, Theme.Small, Theme.AccentText, catalogLink);
        }

        // direita: versão e atualização
        string ver = "v" + AppInfo.VersionText;
        string latest = update.Latest is { } l ? $"v{l.Major}.{l.Minor}.{l.Build}" : "";
        string? ulink = null;
        string right = update.State switch
        {
            UpdateState.Disabled => "build de teste · sem atualização automática",
            UpdateState.Checking => $"{ver} · procurando atualização…",
            UpdateState.UpToDate => $"{ver} · versão mais nova",
            UpdateState.Downloading => $"baixando {latest}…",
            UpdateState.Ready => $"{latest} instalada ·",
            UpdateState.ManualOnly => $"{latest} disponível ·",
            UpdateState.Failed => $"{ver} · sem conexão para procurar atualização",
            _ => ver
        };
        if (update.State == UpdateState.Ready) ulink = "Reiniciar agora";
        else if (update.State == UpdateState.ManualOnly && update.PageUrl is not null) ulink = "Baixar";

        int rw = Theme.Measure(g, right, Theme.Small).Width;
        int uw = ulink is null ? 0 : Theme.Measure(g, ulink, Theme.Small).Width + S(6);
        int x = f.Right - pad - rw - uw;
        Theme.DrawText(g, right, Theme.Small, update.State == UpdateState.Ready ? Theme.Green : Theme.Faint,
            new Rectangle(x, f.Y, rw + S(2), f.Height));
        updateLink = Rectangle.Empty;
        if (ulink is not null)
        {
            updateLink = new Rectangle(x + rw + S(6), f.Y, uw, f.Height);
            Theme.DrawText(g, ulink, Theme.Small, Theme.AccentText, updateLink);
        }
    }

    static string When(DateTime t)
    {
        var today = DateTime.Today;
        if (t.Date == today) return $"hoje, {t:HH:mm}";
        if (t.Date == today.AddDays(-1)) return $"ontem, {t:HH:mm}";
        return $"em {t:dd/MM}";
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Cursor = catalogLink.Contains(e.Location) || updateLink.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (catalogLink.Contains(e.Location) && !fetching) _ = RefreshCatalogAsync();
        else if (updateLink.Contains(e.Location))
        {
            if (update.State == UpdateState.Ready)
            {
                restarting = true;
                try { Updater.Restart(); Close(); }
                catch (Exception ex) { restarting = false; MessageBox.Show(this, ex.Message, "GameRunner Simulator"); }
            }
            else if (update.PageUrl is not null)
            {
                try { Process.Start(new ProcessStartInfo(update.PageUrl) { UseShellExecute = true }); } catch { }
            }
        }
        base.OnMouseClick(e);
    }

    // ---------------- print para o README (só no CI) ----------------

    async Task TakeScreenshotAsync(string path)
    {
        try
        {
            var first = games.FirstOrDefault(g => g.Id == OverwatchId) ?? games.FirstOrDefault();
            var second = games.FirstOrDefault(g => g.Name.Equals("Marvel Rivals", StringComparison.OrdinalIgnoreCase))
                         ?? games.FirstOrDefault(g => g.Icon is not null && g != first);
            if (second is not null) simulator.Start(second, second.MainExe);
            if (first is not null)
            {
                gameList.Select(first);
                simulator.Start(first, first.MainExe);
            }
            await Task.Delay(7000);

            using var full = new Bitmap(Width, Height);
            DrawToBitmap(full, new Rectangle(0, 0, Width, Height));
            var origin = PointToScreen(Point.Empty);
            int ox = origin.X - Left, oy = origin.Y - Top;
            using var shot = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (var g = Graphics.FromImage(shot))
                g.DrawImage(full, new Rectangle(0, 0, shot.Width, shot.Height),
                    new Rectangle(ox, oy, shot.Width, shot.Height), GraphicsUnit.Pixel);
            shot.Save(path, ImageFormat.Png);
            ExitCode = 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(path + ".erro.txt", ex.ToString());
            ExitCode = 1;
        }
        finally
        {
            simulator.StopAll();
            await Task.Delay(2000);
            Close();
        }
    }
}
