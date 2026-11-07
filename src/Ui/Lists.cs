using GameRunnerSimulator.Core;

namespace GameRunnerSimulator.Ui;

/// <summary>Coluna da esquerda: jogo com ícone, nome, o .exe principal e a estrela de missão.</summary>
sealed class GameListView : VirtualList<Game>
{
    readonly IconCache icons;
    readonly Func<Game, bool> isActive, isMission;

    public event Action<char>? TypeAhead;
    public event Action<Game>? StarClicked;

    public GameListView(IconCache icons, Func<Game, bool> isActive, Func<Game, bool> isMission)
    {
        this.icons = icons;
        this.isActive = isActive;
        this.isMission = isMission;
        BackColor = Theme.Bg1;
    }

    protected override int RowHeightLogical => 56;

    Rectangle CardRect(Rectangle r) => new(r.X + S(8), r.Y + S(2), r.Width - S(12), r.Height - S(4));
    Rectangle StarRect(Rectangle r)
    {
        var c = CardRect(r);
        return new Rectangle(c.Right - S(36), c.Y + (c.Height - S(28)) / 2, S(28), S(28));
    }

    protected override bool IsControlHit(int index, Point p) => StarRect(RowRect(index)).Contains(p);

    protected override void OnRowClick(int index, Point p)
    {
        if (IsControlHit(index, p)) StarClicked?.Invoke(Items[index]);
    }

    protected override void DrawRow(Graphics g, Game game, Rectangle r, bool isSelected, bool isHover)
    {
        var card = CardRect(r);
        if (isSelected) Theme.FillRound(g, Theme.Selected, card, Sf(8));
        else if (isHover) Theme.FillRound(g, Theme.Hover, card, Sf(8));

        int icon = S(36);
        var ir = new Rectangle(card.X + S(10), card.Y + (card.Height - icon) / 2, icon, icon);
        Theme.DrawGameIcon(g, icons.Get(game, 64), game.Id, game.Name, ir, Sf(10));

        // estrela: cheia se é missão; contorno só quando o mouse passa
        var star = StarRect(r);
        bool mission = isMission(game);
        bool overStar = isHover && star.Contains(Mouse);
        if (overStar) Theme.FillRound(g, isSelected ? Theme.HoverOnBg2 : Theme.Selected, star, Sf(6));
        var sr = RectangleF.Inflate(star, -Sf(6), -Sf(6));
        if (mission) Theme.DrawStar(g, sr, true, Theme.Gold, 0);
        else if (isHover || isSelected) Theme.DrawStar(g, sr, false, overStar ? Theme.Gold : Theme.Faint, Sf(1.4f));

        int textX = ir.Right + S(12);
        int right = star.X - S(6);
        if (isActive(game))
        {
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

    protected override Cursor RowCursor(int index, Point p) => IsControlHit(index, p) ? Cursors.Hand : Cursors.Default;

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

/// <summary>Linha do painel de missões: missão marcada e/ou simulação rodando.</summary>
sealed class MissionItem
{
    public required Game Game { get; init; }
    public Mission? Mission { get; init; }
    public Simulation? Sim { get; init; }
    public bool Queued { get; init; }
}

/// <summary>Missões marcadas (com tempo, progresso e botão Cumprir) e simulações avulsas rodando.</summary>
sealed class MissionsView : VirtualList<MissionItem>
{
    readonly IconCache icons;

    public event Action<MissionItem>? ActionClicked, MinutesClicked, RemoveClicked, OpenRequested;

    public MissionsView(IconCache icons)
    {
        this.icons = icons;
        BackColor = Theme.Bg2;
        Selectable = false;
        EmptyText = "Nenhuma missão marcada.\nClica na estrela ao lado de um jogo para marcar a missão dele.";
    }

    protected override int RowHeightLogical => 66;

    Rectangle CardRect(Rectangle r) => new(r.X, r.Y + S(4), r.Width - S(4), r.Height - S(8));
    int Mid(Rectangle r) { var c = CardRect(r); return c.Y + c.Height / 2; }

    Rectangle RemoveRect(Rectangle r, MissionItem it) =>
        it.Mission is null ? Rectangle.Empty : new Rectangle(CardRect(r).Right - S(38), Mid(r) - S(13), S(26), S(26));

    Rectangle ButtonRect(Rectangle r, MissionItem it)
    {
        int right = it.Mission is null ? CardRect(r).Right - S(12) : RemoveRect(r, it).X - S(6);
        return new Rectangle(right - S(92), Mid(r) - S(16), S(92), S(32));
    }

    Rectangle ChipRect(Rectangle r, MissionItem it) =>
        it.Mission is null ? Rectangle.Empty : new Rectangle(ButtonRect(r, it).X - S(10) - S(62), Mid(r) - S(13), S(62), S(26));

    protected override bool IsControlHit(int index, Point p)
    {
        var r = RowRect(index);
        var it = Items[index];
        return ButtonRect(r, it).Contains(p) || ChipRect(r, it).Contains(p) || RemoveRect(r, it).Contains(p);
    }

    protected override Cursor RowCursor(int index, Point p) => Cursors.Hand;

    protected override void OnRowClick(int index, Point p)
    {
        var r = RowRect(index);
        var it = Items[index];
        if (ButtonRect(r, it).Contains(p)) ActionClicked?.Invoke(it);
        else if (ChipRect(r, it).Contains(p)) MinutesClicked?.Invoke(it);
        else if (RemoveRect(r, it).Contains(p)) RemoveClicked?.Invoke(it);
        else OpenRequested?.Invoke(it);
    }

    protected override void DrawRow(Graphics g, MissionItem it, Rectangle r, bool isSelected, bool isHover)
    {
        var card = CardRect(r);
        var m = it.Mission;
        var sim = it.Sim;
        bool done = m?.Done is not null && sim is null;
        Theme.FillRound(g, isHover ? Theme.HoverOnBg2 : Theme.Bg3, card, Sf(10));

        int icon = S(38);
        var ir = new Rectangle(card.X + S(14), card.Y + (card.Height - icon) / 2, icon, icon);
        Theme.DrawGameIcon(g, icons.Get(it.Game, 64), it.Game.Id, it.Game.Name, ir, Sf(10));
        if (done)
        {
            // selo verde no canto do ícone
            int d = S(16);
            var seal = new Rectangle(ir.Right - d + S(4), ir.Bottom - d + S(4), d, d);
            Theme.FillRound(g, Theme.Bg3, RectangleF.Inflate(seal, Sf(2), Sf(2)), d / 2f + Sf(2));
            Theme.FillRound(g, Theme.Green, seal, d / 2f);
            using var pen = new Pen(Color.White, Sf(1.8f)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLines(pen, new[]
            {
                new PointF(seal.X + d * 0.28f, seal.Y + d * 0.52f),
                new PointF(seal.X + d * 0.44f, seal.Y + d * 0.68f),
                new PointF(seal.X + d * 0.73f, seal.Y + d * 0.36f)
            });
        }

        // botão de ação
        var btn = ButtonRect(r, it);
        bool overBtn = isHover && btn.Contains(Mouse);
        string label;
        Color bg, fg;
        if (sim is not null) { label = "Parar"; bg = overBtn ? Theme.Red : Theme.Selected; fg = overBtn ? Color.White : Theme.Text; }
        else if (done) { label = "Refazer"; bg = overBtn ? Theme.Selected : Theme.Bg2; fg = Theme.Text; }
        else { label = "Cumprir"; bg = overBtn ? Theme.AccentHover : Theme.Accent; fg = Color.White; }
        Theme.FillRound(g, bg, btn, Sf(6));
        Theme.DrawText(g, label, Theme.BodySemibold, fg, btn,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        int textRight = btn.X - S(14);
        if (m is not null)
        {
            // tempo da missão: clica para trocar 15 → 30 → 45 → 60
            var chip = ChipRect(r, it);
            bool overChip = isHover && chip.Contains(Mouse);
            Theme.FillRound(g, overChip ? Theme.Selected : Theme.Bg2, chip, Sf(13));
            Theme.DrawText(g, $"{m.Minutes} min", Theme.Small, overChip ? Theme.Text : Theme.Muted, chip,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            textRight = chip.X - S(14);

            // tirar das missões
            var rm = RemoveRect(r, it);
            bool overRm = isHover && rm.Contains(Mouse);
            if (overRm) Theme.FillRound(g, Theme.Selected, rm, rm.Width / 2f);
            float k = Sf(4.5f);
            float cx = rm.X + rm.Width / 2f, cy = rm.Y + rm.Height / 2f;
            using var pen = new Pen(overRm ? Theme.Text : Theme.Faint, Sf(1.6f)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(pen, cx - k, cy - k, cx + k, cy + k);
            g.DrawLine(pen, cx - k, cy + k, cx + k, cy - k);
        }

        int x = ir.Right + S(14);
        int w = Math.Max(0, textRight - x);
        Theme.DrawText(g, it.Game.Name, Theme.BodySemibold, Theme.Text, new Rectangle(x, card.Y + S(11), w, S(20)));

        string status;
        Color color;
        if (sim is not null && m is not null)
        {
            status = $"rodando · {Theme.Clock(sim.Elapsed)} de {Theme.Clock(m.Target)}";
            color = Theme.Green;
        }
        else if (sim is not null) { status = $"rodando · {Theme.Clock(sim.Elapsed)} · sem missão marcada"; color = Theme.Green; }
        else if (done) { status = $"cumprida às {m!.Done:HH:mm}"; color = Theme.Green; }
        else if (it.Queued) { status = "na fila"; color = Theme.AccentText; }
        else { status = "falta cumprir · " + it.Game.MainExe.FileName; color = Theme.Faint; }
        Theme.DrawText(g, status, Theme.Small, color, new Rectangle(x, card.Y + S(32), w, S(18)));

        // barra de progresso no pé do cartão
        if (sim is not null && m is not null)
        {
            var track = new RectangleF(x, card.Bottom - Sf(9), Math.Max(0, textRight - x), Sf(3));
            Theme.FillRound(g, Theme.Line, track, track.Height / 2);
            float f = (float)Math.Clamp(sim.Elapsed.TotalSeconds / m.Target.TotalSeconds, 0, 1);
            if (f > 0) Theme.FillRound(g, Theme.Green, new RectangleF(track.X, track.Y, Math.Max(track.Height, track.Width * f), track.Height), track.Height / 2);
        }
    }
}
