using GameRunnerSimulator.Core;

namespace GameRunnerSimulator.Ui;

/// <summary>Jogo escolhido: ícone, nome, executável, a estrela de missão e o botão Simular/Parar.</summary>
sealed class DetailPanel : Surface
{
    readonly IconCache icons;
    readonly ExeListView exes = new();
    readonly FlatButton button = new();
    readonly StarButton star = new();
    Game? game;
    Mission? mission;
    bool active;

    public event EventHandler? SimulateClicked;
    public event EventHandler? MarkClicked;

    public DetailPanel(IconCache icons)
    {
        this.icons = icons;
        BackColor = Theme.Bg2;
        Controls.Add(exes);
        Controls.Add(star);
        Controls.Add(button);
        button.Click += (_, _) => SimulateClicked?.Invoke(this, EventArgs.Empty);
        star.Click += (_, _) => MarkClicked?.Invoke(this, EventArgs.Empty);
        exes.ItemActivated += (_, _) => SimulateClicked?.Invoke(this, EventArgs.Empty);
        SetGame(null, false, null);
    }

    public Game? Game => game;
    public GameExe? SelectedExe => exes.SelectedItem;

    const int Pad = 32, TopY = 24, IconSize = 80, MaxRows = 2;

    int Rows => game is null ? 0 : Math.Min(game.Exes.Count, MaxRows);

    /// <summary>Altura que o painel pede à janela (o resto vai para as missões).</summary>
    public int DesiredHeight => game is null
        ? S(236)
        : S(TopY) + S(IconSize) + S(24) + S(20) + Rows * S(46) + S(8) + S(30);

    public void SetGame(Game? g, bool isActive, Mission? m)
    {
        if (g?.Id != game?.Id)
        {
            game = g;
            exes.SetItems(g?.Exes ?? new List<GameExe>(), resetScroll: true);
            exes.Select(g?.MainExe, raise: false);
        }
        else if (g is not null)
        {
            var keep = exes.SelectedItem?.Name;
            game = g;
            exes.SetItems(g.Exes, resetScroll: false);
            exes.Select(g.Exes.FirstOrDefault(e => e.Name == keep) ?? g.MainExe, raise: false);
        }
        exes.Visible = button.Visible = star.Visible = game is not null;
        SetState(isActive, m);
        PerformLayout();
        Invalidate();
    }

    public void SetState(bool isActive, Mission? m)
    {
        active = isActive;
        mission = m;
        star.Checked = m is not null;
        button.Text = isActive ? "Parar simulação" : "Simular";
        button.Kind = isActive ? ButtonKind.Danger : ButtonKind.Primary;
        exes.Enabled = !isActive;
        Invalidate();
    }

    Rectangle IconRect => new(S(Pad), S(TopY), S(IconSize), S(IconSize));
    int ExeLabelY => S(TopY) + S(IconSize) + S(24);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int bw = S(168), bh = S(44);
        int by = S(TopY) + (S(IconSize) - bh) / 2;
        button.SetBounds(Width - S(Pad) - bw, by, bw, bh);
        star.SetBounds(button.Left - S(10) - bh, by, bh, bh);
        int listH = Rows * S(46) + S(8);
        exes.SetBounds(S(Pad) - S(1), ExeLabelY + S(20), Width - S(Pad) * 2 + S(10), listH);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Theme.HighQuality(g);
        if (game is null) { PaintEmpty(g); return; }

        var ir = IconRect;
        Theme.DrawGameIcon(g, icons.Get(game, 128), game.Id, game.Name, ir, Sf(18));

        int x = ir.Right + S(20);
        int right = star.Left - S(20);
        Theme.DrawText(g, game.Name, Theme.Title, Theme.Text, new Rectangle(x, ir.Y + S(6), right - x, S(40)));

        // linha de estado: cada pedaço com a sua cor
        var parts = new List<(string text, Color color, Color? dot)>();
        if (active) parts.Add(("rodando agora", Theme.Green, Theme.Green));
        if (mission?.Done is not null && !active) parts.Add(($"missão cumprida às {mission.Done:HH:mm}", Theme.Gold, Theme.Gold));
        else if (mission is not null) parts.Add(($"missão de {mission.Minutes} min", Theme.Gold, null));
        parts.Add((game.Exes.Count == 1 ? "1 executável" : $"{game.Exes.Count} executáveis", Theme.Muted, null));
        if (game.Aliases.Count > 0) parts.Add(("também " + string.Join(", ", game.Aliases.Take(3)), Theme.Muted, null));

        int mx = x, my = ir.Y + S(49), mh = S(22);
        var sep = "  ·  ";
        int sepW = Theme.Measure(g, sep, Theme.Body).Width;
        for (int i = 0; i < parts.Count && mx < right; i++)
        {
            if (i > 0)
            {
                Theme.DrawText(g, sep, Theme.Body, Theme.Faint, new Rectangle(mx, my, Math.Max(0, right - mx), mh));
                mx += sepW;
            }
            var (text, color, dot) = parts[i];
            if (dot is { } dc)
            {
                int d = S(8);
                Theme.FillRound(g, dc, new Rectangle(mx, my + (mh - d) / 2, d, d), d / 2f);
                mx += d + S(7);
            }
            int w = Theme.Measure(g, text, Theme.Body).Width;
            Theme.DrawText(g, text, Theme.Body, color, new Rectangle(mx, my, Math.Max(0, right - mx), mh));
            mx += w;
        }

        Theme.DrawText(g, "EXECUTÁVEL QUE O DISCORD VAI VER", Theme.Label, Theme.Faint,
            new Rectangle(S(Pad), ExeLabelY, Width - S(Pad) * 2, S(16)));

        string hint = game.Exes.Count > MaxRows
            ? $"Role para ver os {game.Exes.Count} executáveis. A estrela marca o jogo como missão."
            : "A estrela marca o jogo como missão: ele vai para o painel de baixo, com tempo e progresso.";
        Theme.DrawText(g, hint, Theme.Small, Theme.Faint,
            new Rectangle(S(Pad), exes.Bottom + S(6), Width - S(Pad) * 2, S(18)));
    }

    void PaintEmpty(Graphics g)
    {
        int x = S(Pad), y = S(TopY) + S(8), w = Width - S(Pad) * 2;
        Theme.DrawText(g, "Escolhe um jogo na lista", Theme.Heading, Theme.Text, new Rectangle(x, y, w, S(28)));
        y += S(42);
        string[] steps =
        {
            "Aceita a missão no Discord e busca o jogo dela aqui.",
            "Clica na estrela: o jogo vai para Missões, com o tempo da missão.",
            "Clica em Cumprir (ou Cumprir todas) e deixa o app aberto."
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Theme.DrawText(g, (i + 1).ToString(), Theme.BodySemibold, Theme.AccentText, new Rectangle(x, y, S(18), S(24)));
            Theme.DrawText(g, steps[i], Theme.Body, Theme.Muted, new Rectangle(x + S(22), y, w - S(22), S(24)));
            y += S(30);
        }
        y += S(8);
        Theme.DrawText(g, "Atalhos: Ctrl+F busca · ↑ ↓ navega · Enter simula · Ctrl+M marca missão", Theme.Small, Theme.Faint, new Rectangle(x, y, w, S(20)));
    }
}
