using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GameRunnerSimulator.Ui;

/// <summary>Cores, fontes e desenho comum. Paleta escura no tom do próprio Discord.</summary>
static class Theme
{
    public static readonly Color Bg0 = C(0x1E1F22);      // rodapé, campo de busca
    public static readonly Color Bg1 = C(0x2B2D31);      // coluna da lista
    public static readonly Color Bg2 = C(0x313338);      // painel principal
    public static readonly Color Bg3 = C(0x383A40);      // cartões sobre o painel
    public static readonly Color Hover = C(0x35373C);
    public static readonly Color HoverOnBg2 = C(0x3B3D44);
    public static readonly Color Selected = C(0x404249);
    public static readonly Color Line = C(0x3F4147);
    public static readonly Color Text = C(0xF2F3F5);
    public static readonly Color Muted = C(0xB5BAC1);
    public static readonly Color Faint = C(0x949BA4);
    public static readonly Color Accent = C(0x5865F2);
    public static readonly Color AccentHover = C(0x4752C4);
    public static readonly Color AccentPressed = C(0x3C45A5);
    public static readonly Color AccentText = C(0x949CF7);
    public static readonly Color Green = C(0x23A55A);
    public static readonly Color Red = C(0xDA373C);
    public static readonly Color RedHover = C(0xA12828);
    public static readonly Color Gold = C(0xF0B232);      // estrela de missão

    public static readonly Font Body = new("Segoe UI", 10f);
    public static readonly Font BodySemibold = new("Segoe UI Semibold", 10f);
    public static readonly Font Small = new("Segoe UI", 9f);
    public static readonly Font Label = new("Segoe UI Semibold", 8.25f);
    public static readonly Font Brand = new("Segoe UI Semibold", 12.5f);
    public static readonly Font Heading = new("Segoe UI Semibold", 13f);
    public static readonly Font Title = new("Segoe UI Semibold", 21f);
    public static readonly Font Timer = new("Segoe UI Semibold", 11f);
    public static readonly Font TimerBig = new("Segoe UI Semilight", 22f);
    public static readonly Font Initial = new("Segoe UI Semibold", 13f);

    static readonly Color[] PlaceholderColors =
    {
        C(0x5865F2), C(0x3BA55C), C(0xC27C0E), C(0xED4245), C(0xEB459E), C(0x8A63D2), C(0x1F8B9E)
    };

    static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    public static void HighQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Color color, RectangleF r, float radius)
    {
        using var path = Round(r, radius);
        using var b = new SolidBrush(color);
        g.FillPath(b, path);
    }

    public static void StrokeRound(Graphics g, Color color, RectangleF r, float radius, float width)
    {
        using var path = Round(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    /// <summary>Imagem com cantos arredondados e borda suavizada (pincel de textura, não recorte).</summary>
    public static void DrawRoundedImage(Graphics g, Image img, RectangleF r, float radius)
    {
        using var path = Round(r, radius);
        using var brush = new TextureBrush(img, WrapMode.Clamp);
        brush.TranslateTransform(r.X, r.Y);
        brush.ScaleTransform(r.Width / img.Width, r.Height / img.Height);
        g.FillPath(brush, path);
    }

    /// <summary>Quadrado colorido com a inicial do jogo, enquanto o ícone não chega (ou se não existe).</summary>
    public static void DrawPlaceholder(Graphics g, string seed, string name, Rectangle r, float radius)
    {
        int h = 0;
        foreach (var ch in seed) h = unchecked(h * 31 + ch);
        var color = PlaceholderColors[(int)((uint)h % PlaceholderColors.Length)];
        FillRound(g, Color.FromArgb(70, color), r, radius);
        var initial = name.FirstOrDefault(char.IsLetterOrDigit);
        if (initial == default) initial = '?';
        float size = Math.Max(6f, r.Height * 0.36f * 72f / g.DpiY);
        using var f = new Font(Initial.FontFamily, size, FontStyle.Bold, GraphicsUnit.Point);
        TextRenderer.DrawText(g, char.ToUpperInvariant(initial).ToString(), f, r, Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    public static void DrawGameIcon(Graphics g, Image? img, string id, string name, Rectangle r, float radius)
    {
        if (img is not null) DrawRoundedImage(g, img, r, radius);
        else DrawPlaceholder(g, id, name, r, radius);
    }

    /// <summary>Estrela de 5 pontas: cheia (missão marcada) ou só o contorno.</summary>
    public static void DrawStar(Graphics g, RectangleF r, bool filled, Color c, float stroke)
    {
        var pts = new PointF[10];
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2 + r.Height * 0.04f;
        float outer = Math.Min(r.Width, r.Height) / 2, inner = outer * 0.48f;
        for (int i = 0; i < 10; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 5;
            float rad = i % 2 == 0 ? outer : inner;
            pts[i] = new PointF(cx + (float)(Math.Cos(a) * rad), cy + (float)(Math.Sin(a) * rad));
        }
        if (filled) { using var b = new SolidBrush(c); g.FillPolygon(b, pts); }
        else { using var p = new Pen(c, stroke) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round }; g.DrawPolygon(p, pts); }
    }

    public const TextFormatFlags Line1 = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    public static void DrawText(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags = Line1) =>
        TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix);

    public static Size Measure(Graphics g, string s, Font f) =>
        TextRenderer.MeasureText(g, s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

    public static string Clock(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";

    // ---------- recursos embutidos ----------

    static Bitmap? logo;
    public static Bitmap? Logo => logo ??= LoadLogo();

    static Bitmap? LoadLogo()
    {
        try
        {
            using var s = typeof(Theme).Assembly.GetManifestResourceStream("logo.png");
            return s is null ? null : new Bitmap(s);
        }
        catch { return null; }
    }

    public static Icon? AppIcon()
    {
        try
        {
            using var s = typeof(Theme).Assembly.GetManifestResourceStream("app.ico");
            if (s is not null) return new Icon(s);
        }
        catch { }
        try { return Icon.ExtractAssociatedIcon(Core.AppInfo.ExePath); } catch { return null; }
    }

    // ---------- barra de título escura (Windows 10 20H1+ e 11) ----------

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void DarkTitleBar(IntPtr hwnd, Color caption)
    {
        try
        {
            int on = 1;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));                 // DWMWA_USE_IMMERSIVE_DARK_MODE
            int colorRef = caption.R | (caption.G << 8) | (caption.B << 16);
            DwmSetWindowAttribute(hwnd, 35, ref colorRef, sizeof(int));           // DWMWA_CAPTION_COLOR (Windows 11)
        }
        catch { }
    }
}
