using System.Text;

namespace Utf8Converter;

public class MainForm : Form
{
    // ── Controls ──────────────────────────────────────────────────────────
    private Button btnFile = null!;
    private Button btnFolder = null!;
    private Button btnClear = null!;
    private ProgressBar progressBar = null!;
    private Label lblStatus = null!;
    private ListBox lstLog = null!;
    private Panel pnlTop = null!;
    private Panel pnlBottom = null!;

    // ── Colour scheme ─────────────────────────────────────────────────────
    private static readonly Color BgDark    = Color.FromArgb(30, 30, 30);
    private static readonly Color BgPanel   = Color.FromArgb(40, 40, 40);
    private static readonly Color Accent    = Color.FromArgb(0, 120, 215);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);
    private static readonly Color OkGreen   = Color.FromArgb(100, 200, 100);
    private static readonly Color WarnYellow= Color.FromArgb(220, 180, 60);
    private static readonly Color ErrRed    = Color.FromArgb(220, 80, 80);
    private static readonly Color SkipGray  = Color.FromArgb(140, 140, 140);

    // ── Log items with colour ─────────────────────────────────────────────
    private readonly List<LogEntry> _logEntries = [];

    public MainForm()
    {
        // Register extra encodings (Windows code pages)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        InitializeControls();
        Text = "UTF-8 Konverter Titlova";
        MinimumSize = new Size(720, 520);
        Size = new Size(860, 620);
        BackColor = BgDark;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10f);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;
    }

    // ── UI construction ───────────────────────────────────────────────────

    private void InitializeControls()
    {
        // Top panel: buttons + status
        pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = BgPanel,
            Padding = new Padding(8, 8, 8, 8)
        };

        btnFile = MakeButton("  Odaberi datoteku", Accent);
        btnFile.Click += BtnFile_Click;

        btnFolder = MakeButton("  Odaberi folder", Color.FromArgb(0, 140, 100));
        btnFolder.Click += BtnFolder_Click;

        btnClear = MakeButton("  Očisti log", Color.FromArgb(80, 80, 80));
        btnClear.Click += (_, _) => ClearLog();

        lblStatus = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Right,
            Width = 320,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = SkipGray,
            Font = new Font("Segoe UI", 9f),
            Text = "Odaberite datoteku ili folder za obradu."
        };

        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        btnFlow.Controls.AddRange([btnFile, btnFolder, btnClear]);

        pnlTop.Controls.Add(lblStatus);
        pnlTop.Controls.Add(btnFlow);

        // Progress bar
        progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 6,
            Style = ProgressBarStyle.Continuous,
            BackColor = BgPanel,
            ForeColor = Accent
        };

        // Log list
        lstLog = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = TextColor,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9.5f),
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 20,
            SelectionMode = SelectionMode.MultiSimple
        };
        lstLog.DrawItem += LstLog_DrawItem;

        // Bottom panel: legend
        pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = BgPanel
        };
        var legend = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SkipGray,
            Font = new Font("Segoe UI", 8.5f),
            Text = "[OK] već UTF-8   [KONVERTIRAN] promijenjen encoding   [PRESKOČEN] binarno/nepoznato   [GREŠKA] problem s datotekom"
        };
        pnlBottom.Controls.Add(legend);

        Controls.Add(lstLog);
        Controls.Add(progressBar);
        Controls.Add(pnlTop);
        Controls.Add(pnlBottom);
    }

    private static Button MakeButton(string text, Color backColor)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Width = 180,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(4, 4, 4, 4),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.3f);
        return btn;
    }

    // ── Owner-draw log list ───────────────────────────────────────────────

    private void LstLog_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _logEntries.Count) return;

        e.DrawBackground();
        var entry = _logEntries[e.Index];
        var textColor = (e.State & DrawItemState.Selected) != 0
            ? Color.White
            : entry.Color;

        using var brush = new SolidBrush(textColor);
        e.Graphics.DrawString(entry.Text, e.Font ?? lstLog.Font, brush,
            new RectangleF(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height),
            new StringFormat { LineAlignment = StringAlignment.Center });
        e.DrawFocusRectangle();
    }

    // ── Button handlers ───────────────────────────────────────────────────

    private async void BtnFile_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Odaberi datoteku za provjeru",
            Filter = "Titlovi i tekstualne datoteke|*.srt;*.sub;*.ass;*.ssa;*.vtt;*.txt;*.sbv;*.smi;*.mpl|Sve datoteke|*.*"
        };
        if (ofd.ShowDialog() == DialogResult.OK)
            await ProcessAsync([ofd.FileName]);
    }

    private async void BtnFolder_Click(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Odaberi folder za rekurzivno pretraživanje",
            UseDescriptionForTitle = true
        };
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            SetStatus("Tražim datoteke titlova…", SkipGray);
            var files = await Task.Run(() => FileScanner.FindSubtitleFiles(fbd.SelectedPath));
            if (files.Length == 0)
            {
                SetStatus("Nisu pronađene datoteke titlova.", WarnYellow);
                return;
            }
            await ProcessAsync(files);
        }
    }

    // ── Core processing ───────────────────────────────────────────────────

    private async Task ProcessAsync(string[] files)
    {
        SetBusy(true);
        progressBar.Maximum = files.Length;
        progressBar.Value = 0;

        int converted = 0, already = 0, skipped = 0, errors = 0;

        await Task.Run(() =>
        {
            foreach (var file in files)
            {
                LogEntry entry;
                try
                {
                    entry = FileProcessor.Process(file);
                }
                catch (Exception ex)
                {
                    entry = new LogEntry($"[GREŠKA]     {ShortPath(file)}: {ex.Message}", ErrRed);
                    errors++;
                }

                switch (entry.Tag)
                {
                    case LogTag.Converted: converted++; break;
                    case LogTag.AlreadyUtf8: already++; break;
                    case LogTag.Skipped: skipped++; break;
                    case LogTag.Error: errors++; break;
                }

                Invoke(() =>
                {
                    _logEntries.Add(entry);
                    lstLog.Items.Add(entry.Text);
                    lstLog.TopIndex = lstLog.Items.Count - 1;
                    progressBar.Value++;
                    SetStatus(
                        $"Obrađujem {progressBar.Value}/{files.Length} — {Path.GetFileName(file)}",
                        SkipGray);
                });
            }
        });

        var summary =
            $"Gotovo: {files.Length} dat. | " +
            $"Konvertirano: {converted} | " +
            $"Već UTF-8: {already} | " +
            $"Preskočeno: {skipped} | " +
            $"Greške: {errors}";
        SetStatus(summary, converted > 0 ? OkGreen : TextColor);
        SetBusy(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ClearLog()
    {
        _logEntries.Clear();
        lstLog.Items.Clear();
        progressBar.Value = 0;
        SetStatus("Log je očišćen.", SkipGray);
    }

    private void SetStatus(string text, Color color)
    {
        lblStatus.ForeColor = color;
        lblStatus.Text = text;
    }

    private void SetBusy(bool busy)
    {
        btnFile.Enabled = !busy;
        btnFolder.Enabled = !busy;
        btnClear.Enabled = !busy;
    }

    private static string ShortPath(string path) =>
        path.Length > 70 ? "…" + path[^67..] : path;
}
