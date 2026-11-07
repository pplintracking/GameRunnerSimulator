using GameRunnerSimulator.Core;

namespace GameRunnerSimulator.Ui;

/// <summary>Coluna da esquerda: jogo com ícone, nome e o .exe principal.</summary>
sealed class GameListView : VirtualList<Game>
{
    readonly IconCache icons;
    readonly Func<Game, bool> isActive;

    public event Action<char>? TypeAhead;

    public GameListView(IconCache icons, Func<Game, bool> isActive)
    {
        this.icons = icons;
        this.isActive = isActive;
        BackColor = Theme.Bg1;
    }

    protected override int RowHeightLogical => 56;

    protected override void DrawRow(Graphics g, Game game, Rectangle r, bool isSelected, bool isHover)
    {
        var card = new Rectangle(r.X + S(8), r.Y + S(2), r.Width - S(12), r.Height - S(4));
        if (isSelected) Theme.FillRound(g, Theme.Selected, card, Sf(8));
        else if (isHover) Theme.FillRound(g, Theme.Hover, card, Sf(8));

        int icon = S(36);
        var ir = new Rectangle(card.X + S(10), card.Y + (card.Height - icon) / 2, icon, icon);
        Theme.DrawGameIcon(g, icons.Get(game, 64), game.Id, game.Name, ir, Sf(10));

        bool active = isActive(game);
        int textX = ir.Right + S(12);
        int right = card.Right - S(12);
        if (active)
        {
            // ponto verde + "rodando"
            var tag = "rodando";
            var size = Theme.Measure(g, tag, Theme.Small);
            int tx = right - size.Width;
            Theme.DrawText(g, tag, Theme.Small, Theme.Green, new Rectangle(tx, card.Y, size.Width + S(2), card.Height));
            int d = S(8);
            Theme.FillRound(g, Theme.Green, new Rectangle(tx - d - S(6), card.Y + (card.Height - d) / 2, d, d), d / 2f);
            right = tx - d - S(14);
        }

        var nameColor = isSelected || isHover ? Theme.Text : Color.FromArgb(232, Theme.Text);
        Theme.DrawText(g, game.Name, Theme.BodySemibold, nameColor, new Rectangle(textX, card.Y + S(7), right - textX, S(20)));
        var sub = game.MainExe.FileName + (game.Exes.Count > 1 ? $"  +{game.Exes.Count - 1}" : "");
        Theme.DrawText(g, sub, Theme.Small, Theme.Faint, new Rectangle(textX, card.Y + S(27), right - textX, S(17)));
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar)) { TypeAhead?.Invoke(e.KeyChar); e.Handled = true; }
        base.OnKeyPress(e);
    }
}

/// <summary>Executáveis do jogo escolhido, com a pasta em cinza e o nome em branco.</summary>
sealed class ExeListView : VirtualList<GameExe>
{
    public ExeListView()
    {
        BackColor = Theme.Bg2;
    }

    protected override int RowHeightLogical => 46;

    protected override Cursor RowCursor(int index, Point p) => Cursors.Hand;

    protected override void DrawRow(Graphics g, GameExe exe, Rectangle r, bool isSelected, bool isHover)
    {
        var card = new RectangleF(r.X + Sf(1), r.Y + Sf(3), r.Width - Sf(4), r.Height - Sf(6));
        Theme.FillRound(g, isSelected ? Theme.Bg3 : isHover ? Theme.HoverOnBg2 : Theme.Bg2, card, Sf(8));
        Theme.StrokeRound(g, isSelected ? Theme.Accent : Theme.Line, card, Sf(8), isSelected ? Sf(1.6f) : Sf(1f));

        // rádio
        float d = Sf(16);
        var radio = new RectangleF(card.X + Sf(14), card.Y + (card.Height - d) / 2, d, d);
        using (var pen = new Pen(isSelected ? Theme.Accent : Theme.Faint, Sf(1.8f)))
            g.DrawEllipse(pen, radio);
        if (isSelected)
        {
            var dot = RectangleF.Inflate(radio, -Sf(4), -Sf(4));
            using var b = new SolidBrush(Theme.Accent);
            g.FillEllipse(b, dot);
        }

        int x = (int)(radio.Right + Sf(12));
        int right = (int)(card.Right - Sf(14));
        if (exe.Launcher)
        {
            var tag = "launcher";
            var sz = Theme.Measure(g, tag, Theme.Small);
            Theme.DrawText(g, tag, Theme.Small, Theme.Faint, new Rectangle(right - sz.Width, (int)card.Y, sz.Width + S(2), (int)card.Height));
            right -= sz.Width + S(12);
        }
        var row = new Rectangle(x, (int)card.Y, Math.Max(0, right - x), (int)card.Height);
        if (exe.Folder.Length > 0)
        {
            var fw = Math.Min(Theme.Measure(g, exe.Folder, Theme.Body).Width, row.Width / 2);
            Theme.DrawText(g, exe.Folder, Theme.Body, Theme.Faint, new Rectangle(row.X, row.Y, fw, row.Height));
            row = new Rectangle(row.X + fw, row.Y, Math.Max(0, row.Width - fw), row.Height);
        }
        Theme.DrawText(g, exe.FileName, Theme.Body, Theme.Text, row);
    }
}

/// <summary>Simulações rodando agora, com cronômetro e botão parar.</summary>
sealed class ActiveSimsView : VirtualList<Simulation>
{
    readonly IconCache icons;

    public event Action<Simulation>? StopRequested;
    public event Action<Simulation>? OpenRequested;

    public ActiveSimsView(IconCache icons)
    {
        this.icons = icons;
        BackColor = Theme.Bg2;
        Selectable = false;
        EmptyText = "Nenhuma simulação rodando. Escolhe um jogo e clica em Simular.";
    }

    protected override int RowHeightLogical => 64;

    Rectangle CardRect(Rectangle r) => new(r.X, r.Y + S(4), r.Width - S(4), r.Height - S(8));
    Rectangle StopRect(Rectangle r)
    {
        var c = CardRect(r);
        return new Rectangle(c.Right - S(84), c.Y + (c.Height - S(32)) / 2, S(72), S(32));
    }

    protected override Cursor RowCursor(int index, Point p) => Cursors.Hand;

    protected override void OnRowClick(int index, Point p)
    {
        var sim = Items[index];
        if (StopRect(RowRect(index)).Contains(p)) StopRequested?.Invoke(sim);
        else OpenRequested?.Invoke(sim);
    }

    protected override void DrawRow(Graphics g, Simulation sim, Rectangle r, bool isSelected, bool isHover)
    {
        var card = CardRect(r);
        Theme.FillRound(g, isHover ? Theme.HoverOnBg2 : Theme.Bg3, card, Sf(10));

        int icon = S(36);
        var ir = new Rectangle(card.X + S(14), card.Y + (card.Height - icon) / 2, icon, icon);
        Theme.DrawGameIcon(g, icons.Get(sim.Game, 64), sim.Game.Id, sim.Game.Name, ir, Sf(10));

        var stop = StopRect(r);
        bool hoverStop = isHover && stop.Contains(Mouse);
        Theme.FillRound(g, hoverStop ? Theme.Red : Theme.Selected, stop, Sf(6));
        Theme.DrawText(g, "Parar", Theme.BodySemibold, hoverStop ? Color.White : Theme.Text, stop,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        var clock = Theme.Clock(sim.Elapsed);
        var cw = Theme.Measure(g, "00:00:00", Theme.Timer).Width;
        var clockRect = new Rectangle(stop.X - S(16) - cw, card.Y, cw, card.Height);
        Theme.DrawText(g, clock, Theme.Timer, Theme.Text, clockRect,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        int x = ir.Right + S(12);
        int w = clockRect.X - S(12) - x;
        Theme.DrawText(g, sim.Game.Name, Theme.BodySemibold, Theme.Text, new Rectangle(x, card.Y + S(10), w, S(20)));
        var dot = S(7);
        Theme.FillRound(g, Theme.Green, new Rectangle(x, card.Y + S(36), dot, dot), dot / 2f);
        Theme.DrawText(g, sim.Exe.FileName, Theme.Small, Theme.Faint, new Rectangle(x + dot + S(6), card.Y + S(31), w - dot - S(6), S(17)));
    }
}
