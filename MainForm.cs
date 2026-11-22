using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace DiagnosticoPC;

internal sealed class MainForm : Form
{
    private readonly DiagnosticEngine engine = new();
    private readonly Dictionary<string, Panel> pages = new();
    private readonly Dictionary<string, RichTextBox> pageOutputs = new();
    private readonly List<DiagnosticEntry> liveEntries = new();

    private readonly Button runButton = new();
    private readonly Button cancelButton = new();
    private readonly Button exportButton = new();
    private readonly Button adminButton = new();
    private readonly Button technicalButton = new();
    private readonly Label statusLabel = new();
    private readonly Label pageTitle = new();
    private readonly Label pageSubtitle = new();
    private readonly Label dashboardSummary = new();
    private readonly ProgressBar progressBar = new();
    private readonly FlowLayoutPanel metricPanel = new();
    private readonly RichTextBox overviewOutput = new();
    private readonly CheckBox anonymizeExport = new();

    private CancellationTokenSource? cancellation;
    private DiagnosticReport? report;
    private bool isRunning;
    private bool closeAfterCancel;
    private bool showTechnicalDetails;
    private readonly bool isAdmin = DiagnosticEngine.IsAdministrator();

    private static readonly Color Background = Color.FromArgb(14, 18, 25);
    private static readonly Color Sidebar = Color.FromArgb(20, 25, 34);
    private static readonly Color Surface = Color.FromArgb(27, 34, 45);
    private static readonly Color SurfaceRaised = Color.FromArgb(35, 44, 58);
    private static readonly Color Border = Color.FromArgb(51, 62, 78);
    private static readonly Color Primary = Color.FromArgb(75, 130, 205);
    private static readonly Color Muted = Color.FromArgb(163, 175, 192);
    private static readonly Color Good = Color.FromArgb(76, 190, 137);
    private static readonly Color Warning = Color.FromArgb(233, 179, 78);
    private static readonly Color Danger = Color.FromArgb(224, 99, 99);
    private static readonly Color Accent = Color.FromArgb(117, 163, 224);
    private static readonly Color TextMain = Color.FromArgb(225, 231, 239);

    public MainForm()
    {
        Text = "Diagnóstico PC";
        MinimumSize = new Size(1120, 720);
        Size = new Size(1380, 900);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 9.5F);
        DoubleBuffered = true;

        BuildShell();
        ShowPage("Inicio");
        UpdateAdminButton();
        UpdateTechnicalButton();
    }

    private void BuildShell()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Background,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 226));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Background,
            Margin = Padding.Empty
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            Padding = new Padding(22, 12, 22, 12)
        };

        BuildPages(content);
        main.Controls.Add(BuildHeader(), 0, 0);
        main.Controls.Add(content, 0, 1);
        main.Controls.Add(BuildFooter(), 0, 2);
        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(main, 1, 0);
        Controls.Add(root);
    }

    private Control BuildSidebar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            Padding = new Padding(16, 24, 16, 14)
        };

        var brand = new Label
        {
            Text = "Diagnóstico PC",
            Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 38
        };
        var tagline = new Label
        {
            Text = "CENTRO DE ANÁLISIS DEL SISTEMA",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Muted,
            Dock = DockStyle.Top,
            Height = 27
        };

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Sidebar,
            Padding = new Padding(0, 18, 0, 0),
            Margin = Padding.Empty
        };

        AddNav(nav, "Inicio", "Estado general");
        AddNav(nav, "Hardware", "Componentes y unidades");
        AddNav(nav, "Seguridad", "Protecciones de Windows");
        AddNav(nav, "Red", "Internet y conectividad");
        AddNav(nav, "Windows", "Integridad y estabilidad");
        AddNav(nav, "Rendimiento", "Procesos y arranque");
        AddNav(nav, "Dispositivos", "Controladores");

        var bottom = new Label
        {
            Text = "ANÁLISIS LOCAL  ·  SOLO LECTURA\r\nLos informes se guardan únicamente cuando los exportás.",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Muted,
            Dock = DockStyle.Bottom,
            Height = 54,
            TextAlign = ContentAlignment.BottomLeft
        };

        panel.Controls.Add(nav);
        panel.Controls.Add(tagline);
        panel.Controls.Add(brand);
        panel.Controls.Add(bottom);
        return panel;
    }

    private void AddNav(FlowLayoutPanel nav, string name, string description)
    {
        var button = new Button
        {
            Text = name + "\r\n" + description,
            Width = 194,
            Height = 53,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Sidebar,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(12, 0, 4, 0),
            Margin = new Padding(0, 2, 0, 4),
            Cursor = Cursors.Hand,
            Tag = name
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = SurfaceRaised;
        button.Click += (_, _) => ShowPage(name);
        nav.Controls.Add(button);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Background,
            Padding = new Padding(22, 12, 22, 8)
        };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titlePanel = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        pageTitle.Text = "Resumen general";
        pageTitle.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
        pageTitle.ForeColor = Color.White;
        pageTitle.AutoSize = true;
        pageTitle.Location = new Point(0, 0);

        pageSubtitle.Text = "Diagnóstico técnico local · Windows 11";
        pageSubtitle.Font = new Font("Segoe UI", 9F);
        pageSubtitle.ForeColor = Muted;
        pageSubtitle.AutoSize = true;
        pageSubtitle.Location = new Point(2, 34);

        titlePanel.Controls.Add(pageTitle);
        titlePanel.Controls.Add(pageSubtitle);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Background,
            Padding = new Padding(0, 3, 0, 0),
            Margin = Padding.Empty
        };

        ConfigureButton(runButton, "Iniciar análisis", Primary, Color.White, 145);
        runButton.Click += async (_, _) => await StartDiagnosticAsync();

        ConfigureButton(cancelButton, "Cancelar", SurfaceRaised, TextMain, 100);
        cancelButton.Enabled = false;
        cancelButton.Click += (_, _) => cancellation?.Cancel();

        ConfigureButton(adminButton, "Ejecutar como administrador", SurfaceRaised, TextMain, 190);
        adminButton.Click += (_, _) => RelaunchAsAdministrator();

        ConfigureButton(exportButton, "Exportar informe", SurfaceRaised, TextMain, 135);
        exportButton.Enabled = false;
        exportButton.Click += (_, _) => ExportReport();

        ConfigureButton(technicalButton, "Ver datos técnicos", SurfaceRaised, TextMain, 145);
        technicalButton.Click += (_, _) =>
        {
            showTechnicalDetails = !showTechnicalDetails;
            UpdateTechnicalButton();
            RenderAllPages();
        };

        actions.Controls.Add(runButton);
        actions.Controls.Add(cancelButton);
        actions.Controls.Add(adminButton);
        actions.Controls.Add(exportButton);
        actions.Controls.Add(technicalButton);

        header.Controls.Add(titlePanel, 0, 0);
        header.Controls.Add(actions, 0, 1);
        return header;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            Padding = new Padding(16, 7, 16, 6)
        };
        statusLabel.Text = "Listo. Iniciá el análisis cuando quieras.";
        statusLabel.ForeColor = Muted;
        statusLabel.AutoEllipsis = true;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        progressBar.Dock = DockStyle.Right;
        progressBar.Width = 230;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        progressBar.Margin = new Padding(8, 5, 8, 5);

        anonymizeExport.Text = "Ocultar datos personales al exportar";
        anonymizeExport.Checked = true;
        anonymizeExport.ForeColor = Muted;
        anonymizeExport.BackColor = Sidebar;
        anonymizeExport.AutoSize = true;
        anonymizeExport.Dock = DockStyle.Right;
        anonymizeExport.Padding = new Padding(8, 0, 8, 0);

        footer.Controls.Add(statusLabel);
        footer.Controls.Add(progressBar);
        footer.Controls.Add(anonymizeExport);
        return footer;
    }

    private void BuildPages(Panel host)
    {
        pages["Inicio"] = BuildDashboard();
        pages["Hardware"] = BuildReportPage("Hardware y almacenamiento", "Inventario, componentes, capacidad y estado de las unidades.");
        pages["Seguridad"] = BuildReportPage("Seguridad", "Defender, firewall, TPM, Secure Boot, BitLocker y protecciones.");
        pages["Red"] = BuildReportPage("Red y conectividad", "Adaptadores, Wi-Fi, DNS, puerta de enlace y conectividad HTTPS.");
        pages["Windows"] = BuildReportPage("Windows y estabilidad", "Integridad del sistema, servicios, actualizaciones y eventos.");
        pages["Rendimiento"] = BuildReportPage("Rendimiento", "Uso de recursos, procesos y aplicaciones configuradas al inicio.");
        pages["Dispositivos"] = BuildReportPage("Dispositivos y controladores", "Estados de dispositivos y códigos informados por Windows.");

        foreach (var page in pages.Values)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            host.Controls.Add(page);
        }
    }

    private Panel BuildDashboard()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(2) };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Background
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var intro = new Label
        {
            Text = "Estado del equipo y puntos que requieren atención. El resumen se construye con las pruebas completadas.",
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        metricPanel.Dock = DockStyle.Fill;
        metricPanel.FlowDirection = FlowDirection.LeftToRight;
        metricPanel.WrapContents = false;
        metricPanel.BackColor = Background;
        metricPanel.Padding = new Padding(0, 3, 0, 3);
        metricPanel.Margin = Padding.Empty;

        var summary = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(14, 9, 14, 8) };
        var summaryHeading = new Label
        {
            Text = "ESTADO DEL ANÁLISIS",
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = Accent,
            Dock = DockStyle.Top,
            Height = 21
        };
        dashboardSummary.Text = "Todavía no se ejecutó el diagnóstico.";
        dashboardSummary.Font = new Font("Segoe UI", 9F);
        dashboardSummary.ForeColor = TextMain;
        dashboardSummary.Dock = DockStyle.Fill;
        dashboardSummary.TextAlign = ContentAlignment.MiddleLeft;
        dashboardSummary.Name = "dashboardSummary";
        summary.Controls.Add(dashboardSummary);
        summary.Controls.Add(summaryHeading);

        var detailsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(14) };
        var detailsTitle = new Label
        {
            Text = "HALLAZGOS Y RECOMENDACIONES",
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = Accent,
            Dock = DockStyle.Top,
            Height = 25
        };
        overviewOutput.Dock = DockStyle.Fill;
        overviewOutput.ReadOnly = true;
        overviewOutput.BackColor = Surface;
        overviewOutput.ForeColor = TextMain;
        overviewOutput.BorderStyle = BorderStyle.None;
        overviewOutput.Font = new Font("Segoe UI", 9.5F);
        overviewOutput.WordWrap = true;
        overviewOutput.Text = "Cuando finalice el análisis, esta pantalla mostrará los problemas encontrados y las acciones recomendadas.";
        detailsPanel.Controls.Add(overviewOutput);
        detailsPanel.Controls.Add(detailsTitle);

        layout.Controls.Add(intro, 0, 0);
        layout.Controls.Add(metricPanel, 0, 1);
        layout.Controls.Add(summary, 0, 2);
        layout.Controls.Add(detailsPanel, 0, 3);
        panel.Controls.Add(layout);
        AddMetricCards(0, 0, 0, 0);
        return panel;
    }

    private Panel BuildReportPage(string title, string description)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(2) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Background };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var intro = new Label
        {
            Text = description,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var output = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Surface,
            ForeColor = TextMain,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5F),
            WordWrap = true,
            DetectUrls = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            HideSelection = false,
            Padding = new Padding(8)
        };
        pageOutputs[title] = output;

        var container = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(16) };
        container.Controls.Add(output);
        layout.Controls.Add(intro, 0, 0);
        layout.Controls.Add(container, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private void ShowPage(string name)
    {
        if (!pages.TryGetValue(name, out var selected)) return;
        foreach (var pair in pages) pair.Value.Visible = pair.Key == name;
        selected.BringToFront();

        pageTitle.Text = name switch
        {
            "Inicio" => "Resumen general",
            "Hardware" => "Hardware y almacenamiento",
            "Seguridad" => "Seguridad del equipo",
            "Red" => "Red y conectividad",
            "Windows" => "Windows y estabilidad",
            "Rendimiento" => "Rendimiento del sistema",
            "Dispositivos" => "Dispositivos y controladores",
            _ => name
        };
        pageSubtitle.Text = name == "Inicio"
            ? "Diagnóstico técnico local · Windows 11"
            : "Resultados explicados · Solo lectura";

        foreach (Control control in FindNavButtons())
        {
            if (control is Button button)
            {
                bool active = Equals(button.Tag, name);
                button.BackColor = active ? SurfaceRaised : Sidebar;
                button.ForeColor = active ? Color.White : TextMain;
                button.FlatAppearance.BorderColor = active ? Primary : Sidebar;
                button.FlatAppearance.BorderSize = active ? 1 : 0;
            }
        }
    }

    private IEnumerable<Control> FindNavButtons()
    {
        var root = Controls.OfType<TableLayoutPanel>().FirstOrDefault();
        if (root?.Controls.Count == 0) yield break;
        if (root.Controls[0] is not Panel sidebar) yield break;
        foreach (Control child in sidebar.Controls)
            if (child is FlowLayoutPanel flow)
                foreach (Control item in flow.Controls)
                    yield return item;
    }

    private async Task StartDiagnosticAsync()
    {
        if (isRunning) return;

        isRunning = true;
        cancellation = new CancellationTokenSource();
        report = null;
        liveEntries.Clear();

        runButton.Enabled = false;
        cancelButton.Enabled = true;
        exportButton.Enabled = false;
        progressBar.Value = 0;
        foreach (var output in pageOutputs.Values) output.Clear();
        AddMetricCards(0, 0, 0, 0);
        overviewOutput.Text = "Ejecutando comprobaciones. Los hallazgos aparecerán acá a medida que termine cada prueba.";
        dashboardSummary.Text = "Análisis en curso...";
        ShowPage("Inicio");
        statusLabel.Text = "Preparando diagnóstico...";

        try
        {
            var progress = new Progress<DiagnosticProgress>(p =>
            {
                int value = p.Total <= 0 ? 0 : (int)Math.Round((double)p.Completed / p.Total * 100);
                progressBar.Value = Math.Clamp(value, 0, 100);
                statusLabel.Text = p.Completed >= p.Total
                    ? "Preparando el informe final..."
                    : $"Comprobación {Math.Min(p.Completed + 1, p.Total)} de {p.Total}: {p.CurrentTitle}";
            });
            var entryProgress = new Progress<DiagnosticEntry>(entry =>
            {
                liveEntries.Add(entry);
                RenderPage(PageKey(entry.Area));
                RefreshDashboardFromEntries();
            });

            report = await engine.RunAsync(progress, entryProgress, cancellation.Token);
            RefreshDashboard(report);
            exportButton.Enabled = report.Entries.Count > 0;
            progressBar.Value = report.Cancelled ? Math.Min(progressBar.Value, 99) : 100;
            statusLabel.Text = report.Cancelled
                ? "Análisis cancelado. Se conservaron los resultados parciales."
                : $"Análisis terminado · {report.Duration:hh\\:mm\\:ss}.";
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "Análisis cancelado.";
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Se produjo un error durante el diagnóstico.";
            MessageBox.Show(this, ex.Message, "Error de diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            isRunning = false;
            runButton.Enabled = true;
            cancelButton.Enabled = false;
            UpdateAdminButton();
            if (closeAfterCancel && !IsDisposed && IsHandleCreated)
            {
                closeAfterCancel = false;
                BeginInvoke(new Action(Close));
            }
        }
    }

    private void AppendEntry(DiagnosticEntry entry)
    {
        liveEntries.Add(entry);
        RenderPage(PageKey(entry.Area));
        RefreshDashboardFromEntries();
    }

    private static string PageKey(string area) => area switch
    {
        "Hardware" or "Almacenamiento" => "Hardware y almacenamiento",
        "Seguridad" => "Seguridad",
        "Red" => "Red y conectividad",
        "Rendimiento" => "Rendimiento",
        "Dispositivos" => "Dispositivos y controladores",
        _ => "Windows y estabilidad"
    };

    private void RenderAllPages()
    {
        foreach (string key in pageOutputs.Keys) RenderPage(key);
    }

    private void RenderPage(string key)
    {
        if (!pageOutputs.TryGetValue(key, out var output)) return;
        output.Clear();
        var entries = liveEntries.Where(x => PageKey(x.Area) == key).ToList();
        if (entries.Count == 0)
        {
            output.SelectionColor = Muted;
            output.SelectionFont = new Font("Segoe UI", 10F);
            output.AppendText("Todavía no hay resultados para esta sección.\r\n\r\nIniciá el análisis desde «Resumen general». Las comprobaciones aparecerán acá al completarse.");
            return;
        }

        foreach (var entry in entries)
        {
            var narrative = DiagnosticNarrator.Create(entry);
            output.SelectionStart = output.TextLength;
            output.SelectionColor = LevelColor(entry.Level);
            output.SelectionFont = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
            output.AppendText($"{LevelText(entry.Level)}  ·  {entry.Title}\r\n");

            output.SelectionColor = Color.White;
            output.SelectionFont = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            output.AppendText(narrative.Headline + "\r\n");

            output.SelectionColor = Muted;
            output.SelectionFont = new Font("Segoe UI", 8.5F);
            output.AppendText($"Duración de la prueba: {entry.Duration:mm\\:ss}\r\n");

            output.SelectionColor = TextMain;
            output.SelectionFont = new Font("Segoe UI", 9.5F);
            output.AppendText(narrative.Explanation + "\r\n");

            foreach (string fact in narrative.Facts)
            {
                output.SelectionColor = TextMain;
                output.SelectionFont = new Font("Segoe UI", 9.5F);
                output.AppendText("  •  " + fact + "\r\n");
            }

            output.SelectionColor = Accent;
            output.SelectionFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            output.AppendText("Qué conviene hacer\r\n");
            output.SelectionColor = TextMain;
            output.SelectionFont = new Font("Segoe UI", 9F);
            output.AppendText(narrative.Recommendation + "\r\n");

            if (showTechnicalDetails)
            {
                output.SelectionColor = Muted;
                output.SelectionFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
                output.AppendText("\r\nEVIDENCIA TÉCNICA ORIGINAL\r\n");
                output.SelectionColor = Color.FromArgb(205, 215, 228);
                output.SelectionFont = new Font("Consolas", 8.5F);
                output.AppendText(entry.Details + "\r\n");
            }

            output.SelectionColor = Border;
            output.SelectionFont = new Font("Segoe UI", 9F);
            output.AppendText("\r\n────────────────────────────────────────────────────────────\r\n\r\n");
        }

        output.SelectionStart = output.TextLength;
        output.ScrollToCaret();
    }

    private void RefreshDashboardFromEntries()
    {
        int ok = liveEntries.Count(x => x.Level == FindingLevel.Ok);
        int warnings = liveEntries.Count(x => x.Level == FindingLevel.Warning);
        int critical = liveEntries.Count(x => x.Level == FindingLevel.Critical);
        int notVerified = liveEntries.Count(x => x.Level == FindingLevel.NotVerified);
        AddMetricCards(ok, warnings, critical, notVerified);

        var important = liveEntries
            .Where(x => x.Level is FindingLevel.Critical or FindingLevel.Warning or FindingLevel.NotVerified)
            .OrderBy(x => SeverityOrder(x.Level))
            .ToList();

        RenderOverview(important, isRunning);
        dashboardSummary.Text = isRunning
            ? $"Comprobaciones completadas: {liveEntries.Count} · El análisis continúa. Los resultados aún pueden cambiar."
            : $"Comprobaciones completadas: {liveEntries.Count} · Seleccioná una sección para consultar cada resultado.";
    }

    private void RefreshDashboard(DiagnosticReport value)
    {
        int ok = value.Entries.Count(x => x.Level == FindingLevel.Ok);
        int warnings = value.Entries.Count(x => x.Level == FindingLevel.Warning);
        int critical = value.Entries.Count(x => x.Level == FindingLevel.Critical);
        int notVerified = value.Entries.Count(x => x.Level == FindingLevel.NotVerified);
        AddMetricCards(ok, warnings, critical, notVerified);

        string general = critical > 0
            ? $"Se detectaron {critical} resultado(s) crítico(s). Revisá esos puntos primero."
            : warnings > 0
                ? $"Se detectaron {warnings} observación(es) que conviene revisar."
                : notVerified > 0
                    ? "No se detectaron alertas confirmadas, pero algunas pruebas no pudieron verificarse."
                    : "Las comprobaciones realizadas no detectaron observaciones.";

        dashboardSummary.Text =
            general + $"  Duración: {value.Duration:hh\\:mm\\:ss}. " +
            $"Permisos: {(value.RanElevated ? "administrador" : "usuario estándar")}. " +
            (value.Cancelled ? "El análisis fue cancelado; los resultados son parciales." : "El análisis finalizó.");

        var important = value.Entries
            .Where(x => x.Level is FindingLevel.Critical or FindingLevel.Warning or FindingLevel.NotVerified)
            .OrderBy(x => SeverityOrder(x.Level))
            .ToList();
        RenderOverview(important, false);
    }

    private void RenderOverview(IReadOnlyList<DiagnosticEntry> important, bool running)
    {
        overviewOutput.Clear();
        if (important.Count == 0)
        {
            overviewOutput.SelectionColor = Good;
            overviewOutput.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            overviewOutput.AppendText(running ? "Todavía no se registraron observaciones." : "No se detectaron observaciones en las pruebas completadas.");
            overviewOutput.SelectionColor = Muted;
            overviewOutput.SelectionFont = new Font("Segoe UI", 9F);
            overviewOutput.AppendText("\r\nEsto no garantiza ausencia total de malware ni sustituye pruebas específicas del fabricante.");
            return;
        }

        foreach (var item in important)
        {
            var story = DiagnosticNarrator.Create(item);
            overviewOutput.SelectionColor = LevelColor(item.Level);
            overviewOutput.SelectionFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            overviewOutput.AppendText($"{LevelText(item.Level)}  ·  {item.Title}\r\n");
            overviewOutput.SelectionColor = TextMain;
            overviewOutput.SelectionFont = new Font("Segoe UI", 9F);
            overviewOutput.AppendText(story.Headline + "\r\n");
            if (story.Facts.Count > 0)
                overviewOutput.AppendText("  " + story.Facts[0] + "\r\n");
            overviewOutput.SelectionColor = Muted;
            overviewOutput.AppendText(story.Recommendation + "\r\n\r\n");
        }

        if (running)
        {
            overviewOutput.SelectionColor = Muted;
            overviewOutput.AppendText("El análisis sigue en curso. Este resumen se actualiza a medida que terminan las pruebas.");
        }
    }

    private static int SeverityOrder(FindingLevel level) => level switch
    {
        FindingLevel.Critical => 0,
        FindingLevel.Warning => 1,
        FindingLevel.NotVerified => 2,
        FindingLevel.Info => 3,
        _ => 4
    };

    private void AddMetricCards(int ok, int warnings, int critical, int notVerified)
    {
        metricPanel.SuspendLayout();
        metricPanel.Controls.Clear();
        metricPanel.Controls.Add(CreateMetricCard("CORRECTAS", ok.ToString(), "Comprobaciones aprobadas", Good));
        metricPanel.Controls.Add(CreateMetricCard("OBSERVACIONES", warnings.ToString(), "Conviene revisarlas", Warning));
        metricPanel.Controls.Add(CreateMetricCard("CRÍTICAS", critical.ToString(), "Prioridad alta", Danger));
        metricPanel.Controls.Add(CreateMetricCard("NO VERIFICADAS", notVerified.ToString(), "Sin conclusión disponible", Muted));
        metricPanel.ResumeLayout();
    }

    private Control CreateMetricCard(string title, string number, string subtitle, Color color)
    {
        var card = new Panel
        {
            Width = 202,
            Height = 94,
            BackColor = Surface,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(13, 8, 10, 7)
        };
        var heading = new Label
        {
            Text = title,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 20
        };
        var value = new Label
        {
            Text = number,
            ForeColor = color,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            Dock = DockStyle.Left,
            Width = 55,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var caption = new Label
        {
            Text = subtitle,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(caption);
        card.Controls.Add(value);
        card.Controls.Add(heading);
        return card;
    }

    private void ExportReport()
    {
        if (report is null || report.Entries.Count == 0) return;

        using var dialog = new SaveFileDialog
        {
            Title = "Exportar informe de Diagnóstico PC",
            Filter = "Informe HTML (recomendado)|*.html|Informe de texto|*.txt",
            FilterIndex = 1,
            FileName = "DiagnosticoPC_" + report.StartedAt.ToString("yyyyMMdd_HHmm") +
                       (anonymizeExport.Checked ? "_anonimo" : "")
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string content = Path.GetExtension(dialog.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase)
                ? BuildTextReport(report)
                : BuildHtmlReport(report);
            if (anonymizeExport.Checked) content = Anonymize(content);
            File.WriteAllText(dialog.FileName, content, new UTF8Encoding(true));
            statusLabel.Text = "Informe exportado correctamente.";
            MessageBox.Show(this,
                "El informe se guardó correctamente.\r\n\r\nPara obtener un PDF, abrí el HTML en tu navegador y elegí Imprimir > Guardar como PDF.",
                "Exportación completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo guardar el informe: " + ex.Message, "Error de exportación", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string BuildTextReport(DiagnosticReport value)
    {
        var sb = new StringBuilder();
        int ok = value.Entries.Count(x => x.Level == FindingLevel.Ok);
        int warnings = value.Entries.Count(x => x.Level == FindingLevel.Warning);
        int critical = value.Entries.Count(x => x.Level == FindingLevel.Critical);
        int notVerified = value.Entries.Count(x => x.Level == FindingLevel.NotVerified);

        sb.AppendLine("DIAGNÓSTICO PC — INFORME");
        sb.AppendLine(new string('=', 72));
        sb.AppendLine("Inicio: " + value.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"));
        sb.AppendLine("Fin: " + value.FinishedAt.ToString("dd/MM/yyyy HH:mm:ss"));
        sb.AppendLine("Duración: " + value.Duration.ToString(@"hh\:mm\:ss"));
        sb.AppendLine("Permisos: " + (value.RanElevated ? "Administrador" : "Usuario estándar"));
        sb.AppendLine("Resultado: " + (value.Cancelled ? "Parcial — análisis cancelado" : "Análisis finalizado"));
        sb.AppendLine();
        sb.AppendLine("RESUMEN EJECUTIVO");
        sb.AppendLine($"Correctas: {ok}  |  Observaciones: {warnings}  |  Críticas: {critical}  |  No verificadas: {notVerified}");
        sb.AppendLine(OverallText(critical, warnings, notVerified, value.Cancelled));
        sb.AppendLine();

        var important = value.Entries.Where(x => x.Level is FindingLevel.Critical or FindingLevel.Warning or FindingLevel.NotVerified)
            .OrderBy(x => SeverityOrder(x.Level)).ToList();
        sb.AppendLine("HALLAZGOS QUE REQUIEREN ATENCIÓN");
        if (important.Count == 0) sb.AppendLine("No se registraron observaciones en las comprobaciones completadas.");
        foreach (var entry in important)
        {
            var story = DiagnosticNarrator.Create(entry);
            sb.AppendLine();
            sb.AppendLine($"[{LevelText(entry.Level)}] {entry.Title}");
            sb.AppendLine(story.Headline);
            sb.AppendLine(story.Explanation);
            foreach (var fact in story.Facts) sb.AppendLine("  - " + fact);
            sb.AppendLine("Qué conviene hacer: " + story.Recommendation);
        }

        sb.AppendLine();
        sb.AppendLine("RESULTADOS POR SECCIÓN");
        foreach (var entry in value.Entries)
        {
            var story = DiagnosticNarrator.Create(entry);
            sb.AppendLine();
            sb.AppendLine(new string('-', 72));
            sb.AppendLine($"{LevelText(entry.Level)} — {entry.Area} — {entry.Title}");
            sb.AppendLine(story.Headline);
            sb.AppendLine(story.Explanation);
            foreach (var fact in story.Facts) sb.AppendLine("  - " + fact);
            sb.AppendLine("Recomendación: " + story.Recommendation);
            sb.AppendLine($"Duración de la prueba: {entry.Duration:mm\\:ss}");
        }

        sb.AppendLine();
        sb.AppendLine("APÉNDICE TÉCNICO");
        sb.AppendLine("La salida original se conserva para poder auditar cada resultado. No es una interpretación automática.");
        foreach (var entry in value.Entries)
        {
            sb.AppendLine();
            sb.AppendLine($"--- {entry.Id} | {entry.Title} ---");
            sb.AppendLine(entry.Details);
        }

        sb.AppendLine();
        sb.AppendLine("ALCANCE Y LIMITACIONES");
        sb.AppendLine("Las consultas dependen de la información que expongan Windows, firmware, dispositivos y controladores.");
        sb.AppendLine("No verificado significa que no se obtuvo evidencia suficiente; no equivale a una falla confirmada.");
        sb.AppendLine("El estado de Defender no garantiza ausencia de malware y no reemplaza un análisis antivirus completo.");
        sb.AppendLine("Las pruebas no realizan reparaciones ni cambian configuraciones.");
        return sb.ToString();
    }

    private static string OverallText(int critical, int warnings, int notVerified, bool cancelled)
    {
        if (cancelled) return "El análisis se canceló. Los resultados son parciales y no deben tomarse como una revisión completa.";
        if (critical > 0) return $"Se detectaron {critical} resultado(s) críticos que requieren revisión prioritaria.";
        if (warnings > 0) return $"Se detectaron {warnings} observación(es). Revisá los hallazgos antes de concluir que el equipo está en buen estado.";
        if (notVerified > 0) return "No se detectaron alertas confirmadas, pero hay pruebas no verificadas. El resultado general es incompleto.";
        return "No se detectaron observaciones en las comprobaciones realizadas. Esto no garantiza ausencia total de malware o fallas intermitentes.";
    }

    private static string BuildHtmlReport(DiagnosticReport value)
    {
        var sb = new StringBuilder();
        int ok = value.Entries.Count(x => x.Level == FindingLevel.Ok);
        int warnings = value.Entries.Count(x => x.Level == FindingLevel.Warning);
        int critical = value.Entries.Count(x => x.Level == FindingLevel.Critical);
        int notVerified = value.Entries.Count(x => x.Level == FindingLevel.NotVerified);
        string verdictCss = critical > 0 ? "critical" : warnings > 0 ? "warning" : notVerified > 0 ? "pending" : "ok";

        sb.Append("""
<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Diagnóstico PC — Informe</title>
<style>
:root{color-scheme:light;--ink:#182333;--muted:#68778b;--line:#dfe5ec;--paper:#fff;--bg:#eef2f6;--navy:#15253a;--blue:#346aa8;--green:#19734e;--amber:#93600b;--red:#a52e37;--gray:#5e6b7c}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.6 "Segoe UI",Arial,sans-serif}
main{max-width:1080px;margin:30px auto;padding:0 22px}.hero{background:var(--navy);color:#fff;padding:30px 34px;border-radius:14px}
.eyebrow{font-size:11px;letter-spacing:1.2px;color:#b9c9dc;font-weight:700}h1{font-size:28px;line-height:1.2;margin:8px 0}.meta{display:flex;gap:20px;flex-wrap:wrap;color:#c4d0df;font-size:12px;margin-top:16px}
.verdict{margin:18px 0;padding:17px 20px;border-radius:10px;background:#fff;border:1px solid var(--line);border-left:5px solid var(--gray)}
.verdict strong{display:block;font-size:16px;margin-bottom:3px}.verdict.ok{border-left-color:var(--green)}.verdict.warning{border-left-color:var(--amber)}.verdict.critical{border-left-color:var(--red)}.verdict.pending{border-left-color:var(--gray)}
.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:16px 0}.metric,.section{background:var(--paper);border:1px solid var(--line);border-radius:11px;padding:16px}
.metric span{display:block;color:var(--muted);font-size:11px;font-weight:700;letter-spacing:.5px}.metric b{display:block;font-size:27px;margin-top:3px}
.ok{color:var(--green)}.warning{color:var(--amber)}.critical{color:var(--red)}.pending{color:var(--gray)}.info{color:var(--blue)}
h2{font-size:18px;margin:0 0 12px}h3{font-size:14px;margin:0 0 5px}
.section{margin:15px 0}.finding{padding:15px 0;border-top:1px solid var(--line);break-inside:avoid}.finding:first-of-type{border-top:0}
.badge{font-size:10px;font-weight:700;letter-spacing:.5px;text-transform:uppercase}.explanation{color:#39485b;margin:5px 0 8px}.facts{margin:7px 0 9px;padding-left:20px}.facts li{margin:3px 0}.action{background:#f5f8fb;border-radius:7px;padding:10px 12px;color:#314158}
details{margin-top:9px;border:1px solid var(--line);border-radius:7px;padding:9px 11px;background:#fbfcfd}summary{cursor:pointer;color:var(--blue);font-weight:600;font-size:12px}
pre{white-space:pre-wrap;overflow-wrap:anywhere;margin:9px 0 0;padding:12px;border-radius:6px;background:#f1f4f7;color:#354254;font:11px/1.55 Consolas,monospace}
.small{color:var(--muted);font-size:11px}.section-note{color:var(--muted);font-size:12px}.footer{color:var(--muted);font-size:11px;margin:22px 2px}
@media(max-width:720px){.grid{grid-template-columns:repeat(2,1fr)}main{padding:0 12px}.hero{padding:22px}}
@media print{body{background:#fff}main{max-width:none;margin:0;padding:0}.hero{border-radius:0;print-color-adjust:exact;-webkit-print-color-adjust:exact}.section,.metric,.verdict{break-inside:avoid}.section{margin:10px 0}details:not([open]) pre{display:block}details{break-inside:avoid}}
</style></head><body><main>
""");
        sb.Append("<header class=\"hero\"><div class=\"eyebrow\">INFORME DE DIAGNÓSTICO LOCAL</div><h1>Diagnóstico PC</h1><div>Resumen técnico del estado del equipo</div><div class=\"meta\">");
        sb.Append("<span>Inicio: ").Append(WebUtility.HtmlEncode(value.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"))).Append("</span>");
        sb.Append("<span>Duración: ").Append(WebUtility.HtmlEncode(value.Duration.ToString(@"hh\:mm\:ss"))).Append("</span>");
        sb.Append("<span>Permisos: ").Append(value.RanElevated ? "Administrador" : "Usuario estándar").Append("</span>");
        sb.Append("<span>Estado: ").Append(value.Cancelled ? "Parcial / cancelado" : "Finalizado").Append("</span></div></header>");

        sb.Append("<div class=\"verdict ").Append(verdictCss).Append("\"><strong>Conclusión general</strong>")
          .Append(WebUtility.HtmlEncode(OverallText(critical, warnings, notVerified, value.Cancelled))).Append("</div>");

        sb.Append("<div class=\"grid\">");
        AddHtmlMetric(sb, "CORRECTAS", ok, "ok");
        AddHtmlMetric(sb, "OBSERVACIONES", warnings, "warning");
        AddHtmlMetric(sb, "CRÍTICAS", critical, "critical");
        AddHtmlMetric(sb, "NO VERIFICADAS", notVerified, "pending");
        sb.Append("</div>");

        var priority = value.Entries.Where(x => x.Level is FindingLevel.Critical or FindingLevel.Warning or FindingLevel.NotVerified)
            .OrderBy(x => SeverityOrder(x.Level)).ToList();
        sb.Append("<section class=\"section\"><h2>Hallazgos que requieren atención</h2>");
        if (priority.Count == 0)
            sb.Append("<p>No se registraron observaciones en las comprobaciones completadas.</p>");
        foreach (var entry in priority) AppendHtmlFinding(sb, entry, false);
        sb.Append("</section>");

        foreach (var group in value.Entries.GroupBy(x => x.Area))
        {
            sb.Append("<section class=\"section\"><h2>").Append(WebUtility.HtmlEncode(group.Key)).Append("</h2>");
            sb.Append("<div class=\"section-note\">Resultados de las comprobaciones de esta sección.</div>");
            foreach (var entry in group) AppendHtmlFinding(sb, entry, true);
            sb.Append("</section>");
        }

        sb.Append("<div class=\"footer\">ALCANCE: Las consultas dependen de la información que expongan Windows, firmware, dispositivos y controladores. Un resultado no verificado no equivale a una falla confirmada. El estado de Defender no garantiza ausencia total de malware. No se ejecutaron reparaciones ni cambios de configuración.</div>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendHtmlFinding(StringBuilder sb, DiagnosticEntry entry, bool includeTechnical)
    {
        var story = DiagnosticNarrator.Create(entry);
        string css = entry.Level switch
        {
            FindingLevel.Ok => "ok",
            FindingLevel.Warning => "warning",
            FindingLevel.Critical => "critical",
            FindingLevel.NotVerified => "pending",
            _ => "info"
        };
        sb.Append("<article class=\"finding\"><div class=\"badge ").Append(css).Append("\">")
          .Append(WebUtility.HtmlEncode(LevelText(entry.Level))).Append("</div><h3>")
          .Append(WebUtility.HtmlEncode(entry.Title)).Append("</h3><div><strong>")
          .Append(WebUtility.HtmlEncode(story.Headline)).Append("</strong></div><p class=\"explanation\">")
          .Append(WebUtility.HtmlEncode(story.Explanation)).Append("</p><ul class=\"facts\">");
        foreach (var fact in story.Facts)
            sb.Append("<li>").Append(WebUtility.HtmlEncode(fact)).Append("</li>");
        sb.Append("</ul><div class=\"action\"><strong>Qué conviene hacer:</strong> ")
          .Append(WebUtility.HtmlEncode(story.Recommendation)).Append("</div>");
        sb.Append("<div class=\"small\">Duración: ").Append(WebUtility.HtmlEncode(entry.Duration.ToString(@"mm\:ss")))
          .Append(" · Código de salida: ").Append(WebUtility.HtmlEncode(entry.ExitCode?.ToString() ?? "No disponible")).Append("</div>");
        if (includeTechnical)
            sb.Append("<details><summary>Ver evidencia técnica original</summary><pre>")
              .Append(WebUtility.HtmlEncode(entry.Details)).Append("</pre></details>");
        sb.Append("</article>");
    }

    private static void AddHtmlMetric(StringBuilder sb, string title, int count, string css)
    {
        sb.Append("<div class=\"metric\"><span>").Append(WebUtility.HtmlEncode(title))
          .Append("</span><b class=\"").Append(css).Append("\">").Append(count).Append("</b></div>");
    }

    private static string Anonymize(string content)
    {
        content = Regex.Replace(content, @"\b(?:[0-9a-fA-F]{2}[:-]){5}[0-9a-fA-F]{2}\b", "[MAC OCULTA]");
        content = Regex.Replace(content, @"\b(?:\d{1,3}\.){3}\d{1,3}\b", "[IP OCULTA]");
        content = Regex.Replace(content, @"(?i)\b[0-9a-f]{1,4}(?::[0-9a-f]{1,4}){2,7}\b", "[IPv6 OCULTA]");
        content = Regex.Replace(content, @"(?i)\bS-1-5-21-(?:\d+-){3}\d+\b", "[SID OCULTO]");
        content = Regex.Replace(content, @"(?i)C:\\Users\\[^\\\r\n]+", @"C:\Users\[USUARIO OCULTO]");
        content = Regex.Replace(content, @"(?im)^(Usuario|Usuario actual|User|Equipo|ComputerName|SerialNumber|UUID)\s*:\s*.*$", "$1: [OCULTO]");
        content = Regex.Replace(content, @"(?im)^\s*Usuario\s*:\s*.*$", "Usuario: [OCULTO]");
        return content;
    }

    private void RelaunchAsAdministrator()
    {
        if (isAdmin || isRunning) return;
        var answer = MessageBox.Show(
            this,
            "Se abrirá otra instancia con permisos de administrador. Esto permite ejecutar DISM, SFC, CHKDSK y consultas de seguridad adicionales.\r\n\r\nLa aplicación seguirá siendo de solo lectura: no reparará ni modificará el sistema.",
            "Permisos de administrador",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);
        if (answer != DialogResult.OK) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas"
            });
            Close();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            statusLabel.Text = "Se canceló la solicitud de permisos de administrador.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo reiniciar con permisos elevados: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateAdminButton()
    {
        adminButton.Text = isAdmin ? "Administrador activo" : "Ejecutar como administrador";
        adminButton.Enabled = !isAdmin && !isRunning;
        adminButton.BackColor = isAdmin ? Color.FromArgb(35, 83, 67) : SurfaceRaised;
    }

    private void UpdateTechnicalButton()
    {
        technicalButton.Text = showTechnicalDetails ? "Ocultar datos técnicos" : "Ver datos técnicos";
        technicalButton.BackColor = showTechnicalDetails ? Color.FromArgb(44, 61, 83) : SurfaceRaised;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (isRunning)
        {
            e.Cancel = true;
            closeAfterCancel = true;
            statusLabel.Text = "Cancelando la prueba activa y cerrando de forma segura...";
            cancellation?.Cancel();
            return;
        }
        base.OnFormClosing(e);
    }

    private static string LevelText(FindingLevel level) => level switch
    {
        FindingLevel.Ok => "CORRECTO",
        FindingLevel.Info => "INFORMATIVO",
        FindingLevel.Warning => "OBSERVACIÓN",
        FindingLevel.Critical => "CRÍTICO",
        FindingLevel.NotVerified => "NO VERIFICADO",
        _ => "INFORMATIVO"
    };

    private static Color LevelColor(FindingLevel level) => level switch
    {
        FindingLevel.Ok => Good,
        FindingLevel.Warning => Warning,
        FindingLevel.Critical => Danger,
        FindingLevel.NotVerified => Muted,
        _ => Accent
    };

    private static string OverallText(int critical, int warnings, int notVerified, bool cancelled)
    {
        if (cancelled) return "El análisis se canceló. Los resultados son parciales.";
        if (critical > 0) return $"Se detectaron {critical} resultado(s) críticos que requieren revisión prioritaria.";
        if (warnings > 0) return $"Se detectaron {warnings} observación(es) que conviene revisar.";
        if (notVerified > 0) return "No se detectaron alertas confirmadas, pero algunas pruebas no pudieron verificarse.";
        return "No se detectaron observaciones en las pruebas completadas.";
    }

    private static void ConfigureButton(Button button, string text, Color back, Color fore, int width)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 38;
        button.Margin = new Padding(0, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.BackColor = back;
        button.ForeColor = fore;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.1.0";
}
