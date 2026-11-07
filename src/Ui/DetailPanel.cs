using GameRunnerSimulator.Core;

namespace GameRunnerSimulator.Ui;

/// <summary>Jogo escolhido: ícone, nome, executável e o botão Simular/Parar.</summary>
sealed class DetailPanel : Surface
{
    readonly IconCache icons;
    readonly ExeListView exes = new();
    readonly FlatButton button = new();
    Game? game;
    bool active;

    public event EventHandler? SimulateClicked;

    public DetailPanel(IconCache icons)
    {
        this.icons = icons;
        BackColor = Theme.Bg2;
        Controls.Add(exes);
        Controls.Add(button);
        button.Click += (_, _) => SimulateClicked?.Invoke(this, EventArgs.Empty);
        exes.ItemActivated += (_, _) => SimulateClicked?.Invoke(this, EventArgs.Empty);
        SetGame(null, false);
    }

    public Game? Game => game;
    public GameExe? SelectedExe => exes.SelectedItem;

    const int Pad = 32, TopY = 28, IconSize = 80, MaxRows = 3;

    int Rows => game is null ? 0 : Math.Min(game.Exes.Count, MaxRows);

    /// <summary>Altura que o painel pede à janela (o resto vai para as simulações ativas).</summary>
    public int DesiredHeight => game is null
        ? S(250)
        : S(TopY) + S(IconSize) + S(26) + S(22) + Rows * S(46) + S(8) + S(34);

    public void SetGame(Game? g, bool isActive)
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
        exes.Visible = button.Visible = game is not null;
        SetActive(isActive);
        PerformLayout();
        Invalidate();
    }

    public void SetActive(bool isActive)
    {
        active = isActive;
        button.Text = isActive ? "Parar simulação" : "Simular";
        button.Kind = isActive ? ButtonKind.Danger : ButtonKind.Primary;
        exes.Enabled = !isActive;
        Invalidate();
    }

    Rectangle IconRect => new(S(Pad), S(TopY), S(IconSize), S(IconSize));
    int ExeLabelY => S(TopY) + S(IconSize) + S(26);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int bw = S(176), bh = S(44);
        button.SetBounds(Width - S(Pad) - bw, S(TopY) + (S(IconSize) - bh) / 2, bw, bh);
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
        int right = button.Left - S(20);
        Theme.DrawText(g, game.Name, Theme.Title, Theme.Text, new Rectangle(x, ir.Y + S(6), right - x, S(40)));

        var meta = new List<string>();
        if (active) meta.Add("rodando agora");
        meta.Add(game.Exes.Count == 1 ? "1 executável" : $"{game.Exes.Count} executáveis");
        if (game.Aliases.Count > 0) meta.Add("também " + string.Join(", ", game.Aliases.Take(3)));
        int mx = x;
        if (active)
        {
            int d = S(8);
            Theme.FillRound(g, Theme.Green, new Rectangle(mx, ir.Y + S(56), d, d), d / 2f);
            mx += d + S(7);
        }
        Theme.DrawText(g, string.Join("  ·  ", meta), Theme.Body, active ? Theme.Green : Theme.Muted,
            new Rectangle(mx, ir.Y + S(49), right - mx, S(22)));
        if (active)
        {
            // pinta o resto (contagem e apelidos) em cinza por cima da parte verde
            var head = "rodando agora  ·  ";
            int hw = Theme.Measure(g, head, Theme.Body).Width;
            using var b = new SolidBrush(BackColor);
            var rest = new Rectangle(mx + hw, ir.Y + S(49), Math.Max(0, right - mx - hw), S(22));
            g.FillRectangle(b, rest);
            Theme.DrawText(g, string.Join("  ·  ", meta.Skip(1)), Theme.Body, Theme.Muted, rest);
        }

        Theme.DrawText(g, "EXECUTÁVEL QUE O DISCORD VAI VER", Theme.Label, Theme.Faint,
            new Rectangle(S(Pad), ExeLabelY, Width - S(Pad) * 2, S(16)));

        string hint = game.Exes.Count > MaxRows
            ? $"Role para ver os {game.Exes.Count} executáveis. A janelinha da simulação pode ficar minimizada."
            : "A janelinha da simulação precisa ficar aberta enquanto a missão conta. Pode minimizar.";
        Theme.DrawText(g, hint, Theme.Small, Theme.Faint,
            new Rectangle(S(Pad), exes.Bottom + S(8), Width - S(Pad) * 2, S(18)));
    }

    void PaintEmpty(Graphics g)
    {
        int x = S(Pad), y = S(TopY) + S(8), w = Width - S(Pad) * 2;
        Theme.DrawText(g, "Escolhe um jogo na lista", Theme.Heading, Theme.Text, new Rectangle(x, y, w, S(28)));
        y += S(44);
        string[] steps =
        {
            "Busca o jogo da missão pelo nome ou pelo .exe.",
            "Clica em Simular. Abre uma janelinha com o nome do jogo.",
            "Deixa ela aberta até a missão completar no Discord."
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Theme.DrawText(g, (i + 1).ToString(), Theme.BodySemibold, Theme.AccentText, new Rectangle(x, y, S(18), S(24)));
            Theme.DrawText(g, steps[i], Theme.Body, Theme.Muted, new Rectangle(x + S(22), y, w - S(22), S(24)));
            y += S(32);
        }
        y += S(10);
        Theme.DrawText(g, "Atalhos: Ctrl+F busca · ↑ ↓ navega · Enter simula", Theme.Small, Theme.Faint, new Rectangle(x, y, w, S(20)));
    }
}
