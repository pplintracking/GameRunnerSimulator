namespace GameRunnerSimulator.Ui;

/// <summary>
/// Lista que desenha só as linhas visíveis (aguenta 10 mil jogos sem travar), com rolagem suave,
/// barra fina própria, teclado e duplo clique.
/// </summary>
abstract class VirtualList<T> : Surface where T : class
{
    protected readonly List<T> Items = new();
    int scrollY, hover = -1, selected = -1;
    bool dragging, hoverBar;
    int dragStartY, dragStartScroll;
    protected Point Mouse { get; private set; } = new(-1, -1);

    public event EventHandler? SelectionChanged;
    public event EventHandler? ItemActivated;

    public bool Selectable { get; set; } = true;
    public string EmptyText { get; set; } = "";

    protected abstract int RowHeightLogical { get; }
    protected abstract void DrawRow(Graphics g, T item, Rectangle r, bool isSelected, bool isHover);
    protected virtual void OnRowClick(int index, Point p) { }
    protected virtual Cursor RowCursor(int index, Point p) => Cursors.Default;

    protected int RowH => S(RowHeightLogical);
    protected int PadTop => S(4);
    int BarW => S(10);
    int ContentH => Items.Count * RowH + PadTop * 2;
    int MaxScroll => Math.Max(0, ContentH - Height);

    public int Count => Items.Count;
    public T? SelectedItem => selected >= 0 && selected < Items.Count ? Items[selected] : null;
    protected T? HoverItem => hover >= 0 && hover < Items.Count ? Items[hover] : null;

    protected VirtualList()
    {
        TabStop = true;
    }

    public void SetItems(IReadOnlyList<T> items, bool resetScroll)
    {
        var keep = SelectedItem;
        Items.Clear();
        Items.AddRange(items);
        selected = keep is null ? -1 : Items.IndexOf(keep);
        if (resetScroll) scrollY = 0;
        ClampScroll();
        RefreshHover();
        Invalidate();
    }

    public void Select(T? item, bool raise = true)
    {
        int idx = item is null ? -1 : Items.IndexOf(item);
        SetSelected(idx, raise);
    }

    void SetSelected(int idx, bool raise)
    {
        if (!Selectable) return;
        if (idx == selected) return;
        selected = idx;
        if (idx >= 0) EnsureVisible(idx);
        Invalidate();
        if (raise) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EnsureVisible(int idx)
    {
        int top = PadTop + idx * RowH;
        if (top < scrollY) scrollY = top - PadTop;
        else if (top + RowH > scrollY + Height) scrollY = top + RowH - Height + PadTop;
        ClampScroll();
        Invalidate();
    }

    void ClampScroll() => scrollY = Math.Clamp(scrollY, 0, MaxScroll);

    int IndexAt(Point p)
    {
        if (p.X < 0 || p.X >= Width - BarW || p.Y < 0 || p.Y >= Height) return -1;
        int y = p.Y + scrollY - PadTop;
        if (y < 0) return -1;
        int i = y / RowH;
        return i < Items.Count ? i : -1;
    }

    protected Rectangle RowRect(int i) => new(0, PadTop + i * RowH - scrollY, Width - BarW, RowH);

    (Rectangle track, Rectangle thumb) Bar()
    {
        var track = new Rectangle(Width - BarW, 0, BarW, Height);
        if (MaxScroll <= 0) return (track, Rectangle.Empty);
        int h = Math.Max(S(36), (int)((long)Height * Height / ContentH));
        int y = (int)((long)(Height - h) * scrollY / MaxScroll);
        return (track, new Rectangle(Width - BarW + S(3), y, S(4), h));
    }

    void RefreshHover()
    {
        int h = IndexAt(Mouse);
        if (h != hover) { hover = h; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Theme.HighQuality(g);
        if (Items.Count == 0)
        {
            if (EmptyText.Length > 0)
            {
                var r = new Rectangle(S(16), S(12), Width - S(32), Height - S(24));
                Theme.DrawText(g, EmptyText, Theme.Body, Theme.Faint, r,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            return;
        }
        int first = Math.Max(0, (scrollY - PadTop) / RowH);
        int last = Math.Min(Items.Count - 1, (scrollY + Height) / RowH + 1);
        for (int i = first; i <= last; i++)
            DrawRow(g, Items[i], RowRect(i), i == selected, i == hover && !dragging);

        var (_, thumb) = Bar();
        if (!thumb.IsEmpty)
        {
            var c = dragging ? Theme.Faint : hoverBar ? Color.FromArgb(150, Theme.Faint) : Color.FromArgb(90, Theme.Faint);
            if (hoverBar || dragging) thumb = new Rectangle(thumb.X - S(1), thumb.Y, thumb.Width + S(2), thumb.Height);
            Theme.FillRound(g, c, thumb, thumb.Width / 2f);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        scrollY -= e.Delta * RowH * 3 / 120;
        ClampScroll();
        RefreshHover();
        Invalidate();
        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left)
        {
            var (track, thumb) = Bar();
            if (!thumb.IsEmpty && track.Contains(e.Location))
            {
                if (!thumb.Contains(e.Location))
                {
                    // clique na trilha: pula para aquela posição
                    scrollY = (int)((long)MaxScroll * e.Y / Math.Max(1, Height));
                    ClampScroll();
                }
                dragging = true;
                dragStartY = e.Y;
                dragStartScroll = scrollY;
                Invalidate();
            }
            else
            {
                int i = IndexAt(e.Location);
                if (i >= 0)
                {
                    SetSelected(i, true);
                    OnRowClick(i, e.Location);
                }
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Mouse = e.Location;
        if (dragging)
        {
            var (_, thumb) = Bar();
            int travel = Math.Max(1, Height - thumb.Height);
            scrollY = dragStartScroll + (int)((long)(e.Y - dragStartY) * MaxScroll / travel);
            ClampScroll();
            Invalidate();
        }
        else
        {
            bool onBar = MaxScroll > 0 && e.X >= Width - BarW;
            if (onBar != hoverBar) { hoverBar = onBar; Invalidate(); }
            RefreshHover();
            Cursor = hover >= 0 ? RowCursor(hover, e.Location) : Cursors.Default;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (dragging) { dragging = false; Invalidate(); }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        Mouse = new Point(-1, -1);
        hover = -1;
        hoverBar = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && IndexAt(e.Location) >= 0) ItemActivated?.Invoke(this, EventArgs.Empty);
        base.OnMouseDoubleClick(e);
    }

    protected override void OnResize(EventArgs e)
    {
        ClampScroll();
        base.OnResize(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Items.Count > 0 && Selectable)
        {
            int page = Math.Max(1, Height / RowH - 1);
            int target = e.KeyCode switch
            {
                Keys.Down => Math.Min(Items.Count - 1, selected + 1),
                Keys.Up => Math.Max(0, selected - 1),
                Keys.PageDown => Math.Min(Items.Count - 1, Math.Max(0, selected) + page),
                Keys.PageUp => Math.Max(0, selected - page),
                Keys.Home => 0,
                Keys.End => Items.Count - 1,
                _ => int.MinValue
            };
            if (target != int.MinValue) { SetSelected(target, true); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter && SelectedItem is not null) { ItemActivated?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}
