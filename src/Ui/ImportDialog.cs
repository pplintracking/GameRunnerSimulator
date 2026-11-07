namespace GameRunnerSimulator.Ui;

/// <summary>
/// Cola aqui a lista de missões copiada do Discord e o app marca sozinho as de "jogue por X min".
/// Não fala com o Discord: só lê o texto colado.
/// </summary>
sealed class ImportDialog : Form
{
    readonly TextBox box;
    readonly Label title, steps, status;
    readonly FlatButton importBtn, closeBtn;
    readonly Func<string, (bool ok, string msg)> import;

    int S(int v) => LogicalToDeviceUnits(v);

    public ImportDialog(Func<string, (bool ok, string msg)> import)
    {
        this.import = import;
        Text = "Importar missões do Discord";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Bg1;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        Icon = Theme.AppIcon();

        title = new Label
        {
            Text = "Trazer as missões do Discord",
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            AutoSize = false
        };
        steps = new Label
        {
            Font = Theme.Body,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Text =
                "1. No navegador, abra discord.com e entre em Loja → Missões.\n" +
                "2. Aperte F12, abra a aba Network e recarregue a página. Na busca, digite: quests\n" +
                "3. Na linha quests@me, botão direito → Copy → Copy response.\n" +
                "4. Cole abaixo e clique em Importar.\n\n" +
                "O app não envia nada pro Discord: ele só lê o que você colou e marca as missões de " +
                "\"jogue por X minutos\". As de vídeo, stream ou pré-compra ele não cumpre."
        };
        box = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Bg0,
            ForeColor = Theme.Text,
            Font = new Font("Consolas", 9f),
            PlaceholderText = "Cole aqui o conteúdo de quests/@me"
        };
        status = new Label { Font = Theme.Small, ForeColor = Theme.Faint, AutoSize = false, Text = "" };
        importBtn = new FlatButton { Text = "Importar", Kind = ButtonKind.Primary };
        closeBtn = new FlatButton { Text = "Fechar", Kind = ButtonKind.Secondary };
        importBtn.Click += (_, _) => RunImport();
        closeBtn.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { title, steps, box, status, importBtn, closeBtn });
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(Handle, Theme.Bg1);
    }

    protected override void OnLoad(EventArgs e)
    {
        // depois do handle criado o DPI é confiável (o resto do app monta o layout aqui também);
        // o tamanho vai antes do base para o centrar-no-pai usar a medida final
        ClientSize = new Size(S(560), S(492));
        int x = S(24), w = S(512);
        title.SetBounds(x, S(20), w, S(28));
        steps.SetBounds(x, S(52), w, S(146));
        box.SetBounds(x, S(204), w, S(168));
        status.SetBounds(x, S(380), w, S(42));
        importBtn.SetBounds(x + w - S(150), S(434), S(150), S(38));
        closeBtn.SetBounds(x + w - S(150) - S(10) - S(120), S(434), S(120), S(38));
        base.OnLoad(e);
    }

    void RunImport()
    {
        var (ok, msg) = import(box.Text);
        status.ForeColor = ok ? Theme.Green : Theme.Red;
        status.Text = msg;
    }
}
