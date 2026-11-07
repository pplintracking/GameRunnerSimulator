namespace GameRunnerSimulator.Ui;

/// <summary>Base dos controles desenhados à mão: duplo buffer e medidas em pixel lógico (96 dpi).</summary>
class Surface : Control
{
    public Surface()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected int S(int v) => LogicalToDeviceUnits(v);
    protected float Sf(float v) => v * DeviceDpi / 96f;
}

enum ButtonKind { Primary, Danger, Secondary }

sealed class FlatButton : Surface
{
    bool hover, pressed;
    ButtonKind kind = ButtonKind.Primary;

    public FlatButton()
    {
        Cursor = Cursors.Hand;
        Font = Theme.BodySemibold;
        TabStop = true;
    }

    public ButtonKind Kind { get => kind; set { kind = value; Invalidate(); } }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg2);
        Theme.HighQuality(g);
        var (bg, fg) = kind switch
        {
            ButtonKind.Danger => (pressed ? Theme.RedHover : hover ? Theme.RedHover : Theme.Red, Color.White),
            ButtonKind.Secondary => (pressed ? Theme.Selected : hover ? Theme.HoverOnBg2 : Theme.Bg3, Theme.Text),
            _ => (pressed ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent, Color.White)
        };
        if (!Enabled) { bg = Color.FromArgb(110, bg); fg = Color.FromArgb(150, fg); }
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, bg, r, Sf(8));
        if (Focused && ShowFocusCues) Theme.StrokeRound(g, Color.FromArgb(160, Color.White), RectangleF.Inflate(r, -Sf(2), -Sf(2)), Sf(6), Sf(1.5f));
        Theme.DrawText(g, Text, Font, fg, ClientRectangle,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>Botão quadrado com estrela: marca o jogo como missão.</summary>
sealed class StarButton : Surface
{
    bool hover, isChecked;
    readonly ToolTip tip = new() { InitialDelay = 300 };

    public StarButton()
    {
        Cursor = Cursors.Hand;
        TabStop = true;
        UpdateTip();
    }

    public bool Checked { get => isChecked; set { if (isChecked == value) return; isChecked = value; UpdateTip(); Invalidate(); } }

    void UpdateTip() => tip.SetToolTip(this, isChecked ? "Tirar das missões" : "Marcar como missão");

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tip.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg2);
        Theme.HighQuality(g);
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, hover ? Theme.HoverOnBg2 : Theme.Bg3, r, Sf(8));
        if (isChecked) Theme.StrokeRound(g, Color.FromArgb(150, Theme.Gold), RectangleF.Inflate(r, -Sf(0.5f), -Sf(0.5f)), Sf(8), Sf(1.2f));
        if (Focused && ShowFocusCues) Theme.StrokeRound(g, Color.FromArgb(160, Color.White), RectangleF.Inflate(r, -Sf(2), -Sf(2)), Sf(6), Sf(1.5f));
        float s = Math.Min(Width, Height) * 0.46f;
        var star = new RectangleF((Width - s) / 2f, (Height - s) / 2f, s, s);
        if (isChecked) Theme.DrawStar(g, star, true, Theme.Gold, 0);
        else Theme.DrawStar(g, star, false, hover ? Theme.Gold : Theme.Muted, Sf(1.6f));
    }
}

/// <summary>Campo de busca arredondado com lupa desenhada e botão de limpar.</summary>
sealed class SearchBox : Surface
{
    readonly TextBox box;
    bool hoverClear;

    public event EventHandler? QueryChanged;
    public event KeyEventHandler? BoxKeyDown;

    public SearchBox()
    {
        BackColor = Theme.Bg1;
        box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Bg0,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 10.5f),
            PlaceholderText = "Buscar jogo ou .exe"
        };
        box.TextChanged += (_, _) => { Invalidate(); QueryChanged?.Invoke(this, EventArgs.Empty); };
        box.GotFocus += (_, _) => Invalidate();
        box.LostFocus += (_, _) => Invalidate();
        box.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape && box.TextLength > 0) { box.Clear(); e.SuppressKeyPress = true; return; }
            BoxKeyDown?.Invoke(this, e);
        };
        Controls.Add(box);
        Cursor = Cursors.IBeam;
    }

    public string Query { get => box.Text; set => box.Text = value; }
    public void FocusBox() { box.Focus(); box.SelectAll(); }

    public void Append(char c)
    {
        box.Focus();
        box.AppendText(c.ToString());
        box.SelectionStart = box.TextLength;
    }

    Rectangle ClearRect => new(Width - S(34), (Height - S(24)) / 2, S(24), S(24));

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int left = S(38);
        int right = S(38);
        box.SetBounds(left, (Height - box.PreferredHeight) / 2 + S(1), Math.Max(10, Width - left - right), box.PreferredHeight);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool h = box.TextLength > 0 && ClearRect.Contains(e.Location);
        if (h != hoverClear) { hoverClear = h; Cursor = h ? Cursors.Hand : Cursors.IBeam; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { hoverClear = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (box.TextLength > 0 && ClearRect.Contains(e.Location)) box.Clear();
        box.Focus();
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Theme.HighQuality(g);
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, Theme.Bg0, r, Sf(8));
        if (box.Focused) Theme.StrokeRound(g, Color.FromArgb(150, Theme.Accent), RectangleF.Inflate(r, -Sf(0.5f), -Sf(0.5f)), Sf(8), Sf(1.5f));

        // lupa
        using (var pen = new Pen(Theme.Faint, Sf(1.8f)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
        {
            float cx = S(19), cy = Height / 2f - Sf(1), rad = Sf(5.5f);
            g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
            g.DrawLine(pen, cx + rad * 0.72f, cy + rad * 0.72f, cx + rad * 1.6f, cy + rad * 1.6f);
        }

        // limpar
        if (box.TextLength > 0)
        {
            var c = ClearRect;
            if (hoverClear) Theme.FillRound(g, Theme.Hover, c, c.Width / 2f);
            using var pen = new Pen(hoverClear ? Theme.Text : Theme.Faint, Sf(1.6f))
            { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            float k = Sf(4.5f), mx = c.X + c.Width / 2f, my = c.Y + c.Height / 2f;
            g.DrawLine(pen, mx - k, my - k, mx + k, my + k);
            g.DrawLine(pen, mx + k, my - k, mx - k, my + k);
        }
    }
}
