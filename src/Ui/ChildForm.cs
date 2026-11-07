using System.Drawing.Imaging;
using GameRunnerSimulator.Core;

namespace GameRunnerSimulator.Ui;

/// <summary>
/// Janela do processo de simulação. O processo já tem o nome do .exe do jogo; ela só mostra
/// o que está rodando e há quanto tempo. Fechou a janela, a simulação acaba.
/// </summary>
sealed class ChildForm : Form
{
    readonly string id, name, exeName;
    readonly string? iconHash, shotPath;
    readonly DateTime started = DateTime.Now;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    Image? icon;

    int S(int v) => LogicalToDeviceUnits(v);

    public ChildForm(string id, string name, string? iconHash, string? shotPath)
    {
        this.id = id;
        this.name = name;
        this.iconHash = iconHash;
        this.shotPath = shotPath;
        exeName = Path.GetFileName(AppInfo.ExePath);

        Text = name;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg1;
        DoubleBuffered = true;
        Icon = Theme.AppIcon();
        ClientSize = new Size(S(440), S(196));

        icon = IconCache.FromDisk(id, iconHash);
        timer.Tick += (_, _) => Invalidate(ClockRect);
        timer.Start();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(Handle, Theme.Bg1);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (icon is null && !string.IsNullOrEmpty(iconHash) && !string.IsNullOrEmpty(id))
        {
            try
            {
                var bytes = await AppInfo.Http.GetByteArrayAsync($"https://cdn.discordapp.com/app-icons/{id}/{iconHash}.png?size=128");
                using var ms = new MemoryStream(bytes);
                using var img = Image.FromStream(ms);
                icon = new Bitmap(img);
                Invalidate();
            }
            catch { }
        }
        if (shotPath is not null)
        {
            await Task.Delay(2500);
            try
            {
                using var full = new Bitmap(Width, Height);
                DrawToBitmap(full, new Rectangle(0, 0, Width, Height));
                var origin = PointToScreen(Point.Empty);
                using var shot = new Bitmap(ClientSize.Width, ClientSize.Height);
                using (var g = Graphics.FromImage(shot))
                    g.DrawImage(full, new Rectangle(0, 0, shot.Width, shot.Height),
                        new Rectangle(origin.X - Left, origin.Y - Top, shot.Width, shot.Height), GraphicsUnit.Pixel);
                shot.Save(shotPath, ImageFormat.Png);
            }
            catch (Exception ex) { File.WriteAllText(shotPath + ".erro.txt", ex.ToString()); }
            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        icon?.Dispose();
        base.OnFormClosed(e);
    }

    int Pad => S(24);
    int DividerY => Pad + S(64) + S(22);
    Rectangle ClockRect => new(Pad, DividerY + S(14), S(200), S(44));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.HighQuality(g);
        int w = ClientSize.Width;

        var ir = new Rectangle(Pad, Pad, S(64), S(64));
        Theme.DrawGameIcon(g, icon, id.Length > 0 ? id : name, name, ir, LogicalToDeviceUnits(16));

        int x = ir.Right + S(18);
        int d = S(8);
        Theme.FillRound(g, Theme.Green, new Rectangle(x, Pad + S(6), d, d), d / 2f);
        Theme.DrawText(g, "SIMULANDO", Theme.Label, Theme.Green, new Rectangle(x + d + S(7), Pad, w - x - Pad, S(20)));
        Theme.DrawText(g, name, Theme.Heading, Theme.Text, new Rectangle(x, Pad + S(20), w - x - Pad, S(28)));
        Theme.DrawText(g, exeName, Theme.Small, Theme.Faint, new Rectangle(x, Pad + S(48), w - x - Pad, S(18)));

        using (var pen = new Pen(Theme.Line, 1)) g.DrawLine(pen, Pad, DividerY, w - Pad, DividerY);

        Theme.DrawText(g, Theme.Clock(DateTime.Now - started), Theme.TimerBig, Theme.Text, ClockRect);
        Theme.DrawText(g, "Deixa esta janela aberta\nenquanto a missão conta.", Theme.Small, Theme.Faint,
            new Rectangle(w / 2, DividerY + S(14), w / 2 - Pad, S(44)),
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }
}
