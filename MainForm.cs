using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
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

        AddNav(nav, "Inicio", "Plan de acción priorizado");
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
        pageTitle.Text = "Plan de acción";
        pageTitle.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
        pageTitle.ForeColor = Color.White;
        pageTitle.AutoSize = true;
        pageTitle.Location = new Point(0, 0);

        pageSubtitle.Text = "Auditoría local avanzada · Windows 10/11";
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

        ConfigureButton(runButton, "Análisis completo", Primary, Color.White, 155);
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
        pages["Seguridad"] = BuildReportPage("Seguridad", "Defender, exclusiones antivirus, firewall, TPM, Secure Boot, BitLocker y protecciones.");
        pages["Red"] = BuildReportPage("Red y conectividad", "Adaptadores, Wi-Fi, DNS, puerta de enlace y conectividad HTTPS.");
        pages["Windows"] = BuildReportPage("Windows y estabilidad", "Integridad, historial de fallos de 30 días, energía, servicios y actualizaciones.");
        pages["Rendimiento"] = BuildReportPage("Rendimiento", "CPU medida, memoria, firmas de inicio, tareas programadas y software instalado.");
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
            Text = "Lista numerada de acciones sugeridas, ordenadas por prioridad y por el orden seguro en que conviene realizarlas.",
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        metricPanel.Dock = DockStyle.Fill;
        metricPanel.FlowDirection = FlowDirection.LeftToRight;
        metricPanel.WrapContents = true;
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
            Text = "PLAN DE ACCIÓN — LISTA ORDENADA",
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
        overviewOutput.DetectUrls = true;
        overviewOutput.LinkClicked += OnReferenceLinkClicked;
        overviewOutput.Text = "Cuando finalice el análisis, esta pantalla mostrará una lista numerada de acciones concretas y referencias técnicas para cada hallazgo.";
        detailsPanel.Controls.Add(overviewOutput);
        detailsPanel.Controls.Add(detailsTitle);

        layout.Controls.Add(intro, 0, 0);
        layout.Controls.Add(metricPanel, 0, 1);
        layout.Controls.Add(summary, 0, 2);
        layout.Controls.Add(detailsPanel, 0, 3);
        panel.Controls.Add(layout);
        AddMetricCards(0, 0, 0, 0, 0);
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
        output.LinkClicked += OnReferenceLinkClicked;
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
        if (name != "Inicio") RenderPage(PageKeyFromNavigation(name));

        pageTitle.Text = name switch
        {
            "Inicio" => "Plan de acción",
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
        if (root is null || root.Controls.Count == 0) yield break;
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
        AddMetricCards(0, 0, 0, 0, 0);
        overviewOutput.Text = "Ejecutando comprobaciones. El plan de acción se ordenará a medida que se completen las pruebas.";
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

    private static string PageKeyFromNavigation(string name) => name switch
    {
        "Hardware" => "Hardware y almacenamiento",
        "Seguridad" => "Seguridad",
        "Red" => "Red y conectividad",
        "Windows" => "Windows y estabilidad",
        "Rendimiento" => "Rendimiento",
        "Dispositivos" => "Dispositivos y controladores",
        _ => name
    };

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
            output.AppendText("Todavía no hay resultados para esta sección.\r\n\r\nIniciá el análisis desde «Plan de acción». Las comprobaciones aparecerán acá al completarse.");
            return;
        }

        var recommendations = RecommendationEngine.Build(entries);
        if (recommendations.Count > 0)
        {
            output.SelectionColor = Accent;
            output.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            output.AppendText("ACCIONES RECOMENDADAS PARA ESTA SECCIÓN\r\n\r\n");
            AppendRichRecommendations(output, recommendations);
        }
        else
        {
            output.SelectionColor = Good;
            output.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            output.AppendText("No se generaron acciones correctivas para esta sección con la evidencia disponible.\r\n\r\n");
        }

        output.SelectionColor = Accent;
        output.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        output.AppendText("RESULTADOS DE LAS COMPROBACIONES\r\n\r\n");

        int index = 0;
        foreach (var entry in entries.OrderBy(x => SeverityOrder(x.Level)).ThenBy(x => x.Title, StringComparer.CurrentCulture))
        {
            var narrative = DiagnosticNarrator.Create(entry);
            index++;
            output.SelectionColor = LevelColor(entry.Level);
            output.SelectionFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            output.AppendText($"{index}. [{LevelText(entry.Level)}] {entry.Title}\r\n");

            output.SelectionColor = TextMain;
            output.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            output.AppendText(narrative.Headline + "\r\n");

            foreach (string fact in narrative.Facts)
            {
                output.SelectionColor = TextMain;
                output.SelectionFont = new Font("Segoe UI", 9F);
                output.AppendText("   • " + fact + "\r\n");
            }

            output.SelectionColor = Muted;
            output.SelectionFont = new Font("Segoe UI", 8F);
            output.AppendText($"   Duración: {entry.Duration:mm\\:ss}\r\n");

            if (showTechnicalDetails)
            {
                output.SelectionColor = Muted;
                output.SelectionFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
                output.AppendText("   EVIDENCIA TÉCNICA ORIGINAL\r\n");
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
        int info = liveEntries.Count(x => x.Level == FindingLevel.Info);
        AddMetricCards(ok, warnings, critical, notVerified, info);

        var recommendations = RecommendationEngine.Build(liveEntries);
        RenderOverview(recommendations, isRunning);
        dashboardSummary.Text = isRunning
            ? $"Comprobaciones completadas: {liveEntries.Count} · El plan se actualiza a medida que finalizan las pruebas."
            : $"Comprobaciones completadas: {liveEntries.Count} · Acciones sugeridas: {recommendations.Count}.";
    }

    private void RefreshDashboard(DiagnosticReport value)
    {
        int ok = value.Entries.Count(x => x.Level == FindingLevel.Ok);
        int warnings = value.Entries.Count(x => x.Level == FindingLevel.Warning);
        int critical = value.Entries.Count(x => x.Level == FindingLevel.Critical);
        int notVerified = value.Entries.Count(x => x.Level == FindingLevel.NotVerified);
        int info = value.Entries.Count(x => x.Level == FindingLevel.Info);
        AddMetricCards(ok, warnings, critical, notVerified, info);

        string general = critical > 0
            ? $"Se detectaron {critical} resultado(s) críticos. Seguí el plan de acción desde el primer punto."
            : warnings > 0
                ? $"Se detectaron {warnings} observación(es). Seguí las acciones en el orden numerado."
                : notVerified > 0
                    ? "Algunas pruebas no pudieron verificarse. Consultá las acciones de comprobación."
                    : "Las comprobaciones realizadas no detectaron observaciones.";

        var recommendations = RecommendationEngine.Build(value.Entries);
        dashboardSummary.Text =
            general + $"  Duración: {value.Duration:hh\\:mm\\:ss}. " +
            $"Permisos: {(value.RanElevated ? "administrador" : "usuario estándar")}. " +
            (value.Cancelled ? "El análisis fue cancelado; los resultados son parciales." : $"El análisis finalizó. Acciones sugeridas: {recommendations.Count}.");

        RenderOverview(recommendations, false);
    }

    private void RenderOverview(IReadOnlyList<DiagnosticRecommendation> recommendations, bool running)
    {
        overviewOutput.Clear();
        if (recommendations.Count == 0)
        {
            overviewOutput.SelectionColor = Good;
            overviewOutput.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            overviewOutput.AppendText(running
                ? "Todavía no hay acciones sugeridas. El plan se actualizará cuando terminen las comprobaciones."
                : "No se generaron acciones correctivas o preventivas a partir de las pruebas completadas.");
            overviewOutput.SelectionColor = Muted;
            overviewOutput.SelectionFont = new Font("Segoe UI", 9F);
            overviewOutput.AppendText("\r\n\r\nEsto no garantiza ausencia total de malware ni descarta fallos intermitentes. Los estados informativos o intencionales no se elevan a problemas.");
            return;
        }

        AppendRichRecommendations(overviewOutput, recommendations);

        if (running)
        {
            overviewOutput.SelectionColor = Muted;
            overviewOutput.SelectionFont = new Font("Segoe UI", 8.5F);
            overviewOutput.AppendText("\r\nEl análisis sigue en curso. El orden y el contenido del plan pueden cambiar cuando finalicen más comprobaciones.");
        }
    }

    private static void AppendRichRecommendations(RichTextBox output, IReadOnlyList<DiagnosticRecommendation> recommendations)
    {
        for (int index = 0; index < recommendations.Count; index++)
        {
            var item = recommendations[index];
            output.SelectionColor = item.Priority switch
            {
                "URGENTE" => Danger,
                "ALTA" => Warning,
                "MEDIA" => Accent,
                _ => Muted
            };
            output.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            output.AppendText($"{index + 1}. [{item.Priority}] {item.Title}\r\n");

            output.SelectionColor = TextMain;
            output.SelectionFont = new Font("Segoe UI", 9F);
            output.AppendText("   Qué detectó: " + item.Evidence + "\r\n");
            output.AppendText("   Por qué importa: " + item.WhyItMatters + "\r\n");
            output.AppendText("   Qué hacer:\r\n");
            for (int step = 0; step < item.Steps.Count; step++)
                output.AppendText($"      {step + 1}) {item.Steps[step]}\r\n");

            if (!string.IsNullOrWhiteSpace(item.Command))
            {
                output.SelectionColor = Accent;
                output.SelectionFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                output.AppendText("   Comando para ejecutar manualmente:\r\n");
                output.SelectionColor = Color.FromArgb(210, 220, 235);
                output.SelectionFont = new Font("Consolas", 9F);
                output.AppendText("      " + item.Command + "\r\n");
            }

            if (!string.IsNullOrWhiteSpace(item.Caution))
            {
                output.SelectionColor = Warning;
                output.SelectionFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
                output.AppendText("   Precaución: ");
                output.SelectionColor = TextMain;
                output.SelectionFont = new Font("Segoe UI", 8.5F);
                output.AppendText(item.Caution + "\r\n");
            }

            if (item.Sources.Count > 0)
            {
                output.SelectionColor = Muted;
                output.SelectionFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
                output.AppendText("   Referencias técnicas:\r\n");
                foreach (var source in item.Sources)
                {
                    output.SelectionColor = Accent;
                    output.SelectionFont = new Font("Segoe UI", 8.5F, FontStyle.Underline);
                    output.AppendText("      • " + source.Name + " — " + source.Url + "\r\n");
                }
            }
            output.SelectionColor = Border;
            output.SelectionFont = new Font("Segoe UI", 9F);
            output.AppendText("\r\n────────────────────────────────────────────────────────────\r\n\r\n");
        }
        output.SelectionStart = output.TextLength;
        output.ScrollToCaret();
    }

    private void OnReferenceLinkClicked(object? sender, LinkClickedEventArgs e)
    {
        if (!Uri.TryCreate(e.LinkText, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return;
        string host = uri.Host.ToLowerInvariant();
        bool allowed = host == "learn.microsoft.com" ||
                       host == "support.microsoft.com" ||
                       host == "microsoft.com" ||
                       host.EndsWith(".microsoft.com", StringComparison.Ordinal) ||
                       host == "gigabyte.com" || host.EndsWith(".gigabyte.com", StringComparison.Ordinal) ||
                       host == "amd.com" || host.EndsWith(".amd.com", StringComparison.Ordinal) ||
                       host == "support.anydesk.com";
        if (!allowed) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { statusLabel.Text = "No se pudo abrir la referencia: " + ex.Message; }
    }

    private static int SeverityOrder(FindingLevel level) => level switch
    {
        FindingLevel.Critical => 0,
        FindingLevel.Warning => 1,
        FindingLevel.NotVerified => 2,
        FindingLevel.Info => 3,
        _ => 4
    };

    private void AddMetricCards(int ok, int warnings, int critical, int notVerified, int info)
    {
        metricPanel.SuspendLayout();
        metricPanel.Controls.Clear();
        metricPanel.Controls.Add(CreateMetricCard("CORRECTAS", ok.ToString(), "Comprobaciones aprobadas", Good));
        metricPanel.Controls.Add(CreateMetricCard("OBSERVACIONES", warnings.ToString(), "Conviene revisarlas", Warning));
        metricPanel.Controls.Add(CreateMetricCard("CRÍTICAS", critical.ToString(), "Prioridad alta", Danger));
        metricPanel.Controls.Add(CreateMetricCard("NO VERIFICADAS", notVerified.ToString(), "Sin conclusión disponible", Muted));
        metricPanel.Controls.Add(CreateMetricCard("INFORMATIVAS", info.ToString(), "Datos sin evaluación", Accent));
        metricPanel.ResumeLayout();
    }

    private Control CreateMetricCard(string title, string number, string subtitle, Color color)
    {
        var card = new Panel
        {
            Width = 156,
            Height = 94,
            BackColor = Surface,
            Margin = new Padding(0, 0, 8, 0),
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
        int info = value.Entries.Count(x => x.Level == FindingLevel.Info);

        sb.AppendLine("DIAGNÓSTICO PC — INFORME");
        sb.AppendLine(new string('=', 72));
        sb.AppendLine("Inicio: " + value.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"));
        sb.AppendLine("Fin: " + value.FinishedAt.ToString("dd/MM/yyyy HH:mm:ss"));
        sb.AppendLine("Duración: " + value.Duration.ToString(@"hh\:mm\:ss"));
        sb.AppendLine("Permisos: " + (value.RanElevated ? "Administrador" : "Usuario estándar"));
        sb.AppendLine("Resultado: " + (value.Cancelled ? "Parcial — análisis cancelado" : "Análisis finalizado"));
        sb.AppendLine();
        sb.AppendLine("RESUMEN");
        sb.AppendLine($"Correctas: {ok} | Observaciones: {warnings} | Críticas: {critical} | No verificadas: {notVerified} | Informativas: {info}");
        sb.AppendLine(OverallText(critical, warnings, notVerified, value.Cancelled));
        sb.AppendLine();

        var recommendations = RecommendationEngine.Build(value.Entries);
        sb.AppendLine("PLAN DE ACCIÓN — ORDEN RECOMENDADO");
        sb.AppendLine("Las acciones están ordenadas por prioridad y por dependencias técnicas. No se ejecutó ninguna reparación.");
        if (recommendations.Count == 0)
            sb.AppendLine("No se generaron acciones a partir de las pruebas completadas.");
        for (int index = 0; index < recommendations.Count; index++)
        {
            var item = recommendations[index];
            sb.AppendLine();
            sb.AppendLine($"{index + 1}. [{item.Priority}] {item.Title}");
            sb.AppendLine("   Qué detectó: " + item.Evidence);
            sb.AppendLine("   Por qué importa: " + item.WhyItMatters);
            sb.AppendLine("   Qué hacer:");
            for (int step = 0; step < item.Steps.Count; step++)
                sb.AppendLine($"      {step + 1}) {item.Steps[step]}");
            if (!string.IsNullOrWhiteSpace(item.Command))
                sb.AppendLine("   Comando para ejecutar manualmente: " + item.Command);
            if (!string.IsNullOrWhiteSpace(item.Caution))
                sb.AppendLine("   Precaución: " + item.Caution);
            if (item.Sources.Count > 0)
            {
                sb.AppendLine("   Referencias técnicas:");
                foreach (var source in item.Sources)
                    sb.AppendLine("      - " + source.Name + ": " + source.Url);
            }
        }

        sb.AppendLine();
        sb.AppendLine("RESUMEN DE COMPROBACIONES");
        int checkIndex = 0;
        foreach (var entry in value.Entries.OrderBy(x => SeverityOrder(x.Level)).ThenBy(x => x.Area).ThenBy(x => x.Title))
        {
            var story = DiagnosticNarrator.Create(entry);
            checkIndex++;
            sb.AppendLine($"{checkIndex}. [{LevelText(entry.Level)}] {entry.Area} — {entry.Title}");
            foreach (var fact in story.Facts.Take(4))
                sb.AppendLine("   - " + fact);
        }

        sb.AppendLine();
        sb.AppendLine("EVIDENCIA TÉCNICA ORIGINAL");
        sb.AppendLine("Se conserva separada del plan para no mezclar comandos, mensajes sin interpretar y recomendaciones.");
        foreach (var entry in value.Entries)
        {
            sb.AppendLine();
            sb.AppendLine($"--- {entry.Id} | {entry.Title} ---");
            sb.AppendLine(entry.Details);
        }

        sb.AppendLine();
        sb.AppendLine("ALCANCE Y LIMITACIONES");
        sb.AppendLine("Las recomendaciones se generan con reglas locales basadas en las evidencias encontradas y referencias técnicas incluidas. No se consulta Internet para crear el plan.");
        sb.AppendLine("No verificado significa que no se obtuvo evidencia suficiente; no equivale a una falla confirmada.");
        sb.AppendLine("El estado de Defender no garantiza ausencia total de malware.");
        sb.AppendLine("Las pruebas no realizan reparaciones ni cambian configuraciones. Los comandos mostrados son sugerencias manuales.");
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
        int info = value.Entries.Count(x => x.Level == FindingLevel.Info);
        string verdictCss = critical > 0 ? "critical" : warnings > 0 ? "warning" : notVerified > 0 ? "pending" : "ok";
        var recommendations = RecommendationEngine.Build(value.Entries);

        sb.Append("""
<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Diagnóstico PC — Plan de acción</title>
<style>
:root{color-scheme:light;--ink:#182333;--muted:#68778b;--line:#dfe5ec;--paper:#fff;--bg:#eef2f6;--navy:#15253a;--blue:#346aa8;--green:#19734e;--amber:#93600b;--red:#a52e37;--gray:#5e6b7c}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.58 "Segoe UI",Arial,sans-serif}
main{max-width:1080px;margin:30px auto;padding:0 22px}.hero{background:var(--navy);color:#fff;padding:28px 34px;border-radius:14px}
.eyebrow{font-size:11px;letter-spacing:1.2px;color:#b9c9dc;font-weight:700}h1{font-size:27px;line-height:1.2;margin:8px 0}.meta{display:flex;gap:20px;flex-wrap:wrap;color:#c4d0df;font-size:12px;margin-top:16px}
.verdict{margin:18px 0;padding:16px 20px;border-radius:10px;background:#fff;border:1px solid var(--line);border-left:5px solid var(--gray)}
.verdict strong{display:block;font-size:16px;margin-bottom:3px}.verdict.ok{border-left-color:var(--green)}.verdict.warning{border-left-color:var(--amber)}.verdict.critical{border-left-color:var(--red)}.verdict.pending{border-left-color:var(--gray)}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(145px,1fr));gap:10px;margin:16px 0}.metric,.section{background:var(--paper);border:1px solid var(--line);border-radius:11px;padding:16px}
.metric span{display:block;color:var(--muted);font-size:11px;font-weight:700;letter-spacing:.5px}.metric b{display:block;font-size:26px;margin-top:3px}
.ok{color:var(--green)}.warning{color:var(--amber)}.critical{color:var(--red)}.pending{color:var(--gray)}.info{color:var(--blue)}
h2{font-size:18px;margin:0 0 12px}.section{margin:15px 0}.section-note{color:var(--muted);font-size:12px;margin-bottom:10px}
.actions{margin:0;padding-left:24px}.actions>li{padding:13px 0 16px 3px;border-top:1px solid var(--line);break-inside:avoid}.actions>li:first-child{border-top:0}
.priority{display:inline-block;font-size:10px;font-weight:800;letter-spacing:.5px;margin-right:7px}.evidence,.why{margin:7px 0;color:#39485b}.steps{margin:7px 0 8px;padding-left:24px}.steps li{margin:4px 0}
.command{margin:9px 0;padding:10px 12px;background:#eef3f8;border-radius:7px;color:#15253a}.command code{font:13px Consolas,monospace}.caution{margin:8px 0;padding:10px 12px;background:#fff8e9;border-left:3px solid var(--amber);border-radius:5px}
.refs{margin:5px 0;padding-left:20px;font-size:12px}.refs li{margin:3px 0}a{color:var(--blue);text-decoration:underline;overflow-wrap:anywhere}
.checks{list-style:none;padding:0;margin:0}.checks li{padding:9px 0;border-top:1px solid var(--line)}.checks li:first-child{border-top:0}
.badge{font-size:10px;font-weight:800;letter-spacing:.4px;display:inline-block;min-width:102px}.check-title{font-weight:600}.check-meta{color:var(--muted);font-size:11px}
details{margin:8px 0;border:1px solid var(--line);border-radius:7px;padding:10px 12px;background:#fbfcfd;break-inside:avoid}summary{cursor:pointer;color:var(--blue);font-weight:600;font-size:12px}
pre{white-space:pre-wrap;overflow-wrap:anywhere;margin:9px 0 0;padding:12px;border-radius:6px;background:#f1f4f7;color:#354254;font:11px/1.55 Consolas,monospace}
.footer{color:var(--muted);font-size:11px;margin:22px 2px}
@media(max-width:720px){.grid{grid-template-columns:repeat(2,1fr)}main{padding:0 12px}.hero{padding:22px}}
@media print{body{background:#fff}main{max-width:none;margin:0;padding:0}.hero{border-radius:0;print-color-adjust:exact;-webkit-print-color-adjust:exact}.section,.metric,.verdict,details,.actions>li{break-inside:avoid}.section{margin:10px 0}details:not([open]) pre{display:block}}
</style></head><body><main>
""");
        sb.Append("<header class=\"hero\"><div class=\"eyebrow\">INFORME DE DIAGNÓSTICO LOCAL</div><h1>Diagnóstico PC</h1><div>Plan de acción y resultados de comprobación</div><div class=\"meta\">");
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
        AddHtmlMetric(sb, "INFORMATIVAS", info, "info");
        sb.Append("</div>");

        sb.Append("<section class=\"section\"><h2>Plan de acción — orden recomendado</h2>");
        sb.Append("<div class=\"section-note\">Lista numerada de acciones. Seguí el orden indicado: algunas reparaciones dependen de que la anterior haya terminado.</div>");
        if (recommendations.Count == 0)
            sb.Append("<p>No se generaron acciones correctivas o preventivas a partir de las pruebas completadas.</p>");
        else
        {
            sb.Append("<ol class=\"actions\">");
            foreach (var item in recommendations) AppendHtmlRecommendation(sb, item);
            sb.Append("</ol>");
        }
        sb.Append("</section>");

        sb.Append("<section class=\"section\"><h2>Resumen de comprobaciones</h2><div class=\"section-note\">Cada comprobación aparece una sola vez. La evidencia técnica completa se encuentra al final.</div><ol class=\"checks\">");
        foreach (var entry in value.Entries.OrderBy(x => SeverityOrder(x.Level)).ThenBy(x => x.Area).ThenBy(x => x.Title))
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
            sb.Append("<li><span class=\"badge ").Append(css).Append("\">")
              .Append(WebUtility.HtmlEncode(LevelText(entry.Level))).Append("</span> ")
              .Append("<span class=\"check-title\">").Append(WebUtility.HtmlEncode(entry.Title)).Append("</span> ")
              .Append("<span class=\"check-meta\">— ").Append(WebUtility.HtmlEncode(entry.Area))
              .Append(" · ").Append(WebUtility.HtmlEncode(entry.Duration.ToString(@"mm\:ss"))).Append("</span>");
            if (story.Facts.Count > 0)
                sb.Append("<div class=\"check-meta\">").Append(WebUtility.HtmlEncode(string.Join(" · ", story.Facts.Take(2)))).Append("</div>");
            sb.Append("</li>");
        }
        sb.Append("</ol></section>");

        sb.Append("<section class=\"section\"><h2>Evidencia técnica original</h2><div class=\"section-note\">Se conserva separada de las recomendaciones para facilitar la lectura y permitir una revisión técnica posterior.</div>");
        foreach (var entry in value.Entries)
        {
            sb.Append("<details><summary>").Append(WebUtility.HtmlEncode(entry.Id + " — " + entry.Title + " [" + LevelText(entry.Level) + "]")).Append("</summary><pre>")
              .Append(WebUtility.HtmlEncode(entry.Details)).Append("</pre></details>");
        }
        sb.Append("</section>");

        sb.Append("<div class=\"footer\">Las recomendaciones se generan localmente a partir de reglas y referencias técnicas incluidas en la aplicación. No se consulta Internet para elaborar el plan. No se suben informes ni se ejecutan reparaciones. Los comandos mencionados son acciones manuales y pueden modificar Windows o el sistema de archivos si se ejecutan.</div>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendHtmlRecommendation(StringBuilder sb, DiagnosticRecommendation item)
    {
        string priorityColor = item.Priority switch
        {
            "URGENTE" => "critical",
            "ALTA" => "warning",
            "MEDIA" => "info",
            _ => "pending"
        };
        sb.Append("<li><div><span class=\"priority ").Append(priorityColor).Append("\">")
          .Append(WebUtility.HtmlEncode(item.Priority)).Append("</span><strong>")
          .Append(WebUtility.HtmlEncode(item.Title)).Append("</strong></div>")
          .Append("<p class=\"evidence\"><strong>Qué detectó:</strong> ")
          .Append(WebUtility.HtmlEncode(item.Evidence)).Append("</p>")
          .Append("<p class=\"why\"><strong>Por qué importa:</strong> ")
          .Append(WebUtility.HtmlEncode(item.WhyItMatters)).Append("</p>")
          .Append("<strong>Qué hacer</strong><ol class=\"steps\">");
        foreach (var step in item.Steps)
            sb.Append("<li>").Append(WebUtility.HtmlEncode(step)).Append("</li>");
        sb.Append("</ol>");
        if (!string.IsNullOrWhiteSpace(item.Command))
            sb.Append("<div class=\"command\"><strong>Comando para ejecutar manualmente:</strong><br><code>")
              .Append(WebUtility.HtmlEncode(item.Command)).Append("</code></div>");
        if (!string.IsNullOrWhiteSpace(item.Caution))
            sb.Append("<div class=\"caution\"><strong>Precaución:</strong> ")
              .Append(WebUtility.HtmlEncode(item.Caution)).Append("</div>");
        if (item.Sources.Count > 0)
        {
            sb.Append("<strong>Referencias técnicas</strong><ul class=\"refs\">");
            foreach (var source in item.Sources)
                sb.Append("<li><a href=\"").Append(WebUtility.HtmlEncode(source.Url))
                  .Append("\" target=\"_blank\" rel=\"noopener noreferrer\">")
                  .Append(WebUtility.HtmlEncode(source.Name)).Append("</a></li>");
            sb.Append("</ul>");
        }
        sb.Append("</li>");
    }

    private static void AddHtmlMetric(StringBuilder sb, string title, int count, string css)
    {
        sb.Append("<div class=\"metric\"><span>").Append(WebUtility.HtmlEncode(title))
          .Append("</span><b class=\"").Append(css).Append("\">").Append(count).Append("</b></div>");
    }

    private static string Anonymize(string content)
    {
        content = Regex.Replace(content, @"\b(?:[0-9a-fA-F]{2}[:-]){5}[0-9a-fA-F]{2}\b", "[MAC OCULTA]");
        content = Regex.Replace(content,
            @"(?<![\d.])(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?![\d.])",
            match =>
            {
                int lineStart = content.LastIndexOf('\n', Math.Max(0, match.Index - 1));
                string prefix = content.Substring(lineStart < 0 ? 0 : lineStart + 1, match.Index - (lineStart < 0 ? 0 : lineStart + 1));
                if (Regex.IsMatch(prefix, @"(?i)\b(version|versión|driver|controlador|compilación|build|firmware|release)\b[^\r\n]*$"))
                    return match.Value;
                return "[IP OCULTA]";
            });
        content = Regex.Replace(content,
            @"(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{0,4}:){2,7}[0-9A-Fa-f]{0,4}(?![0-9A-Fa-f])",
            match =>
            {
                string candidate = match.Value;
                int lineStart = content.LastIndexOf('\n', Math.Max(0, match.Index - 1));
                string prefix = content.Substring(lineStart < 0 ? 0 : lineStart + 1, match.Index - (lineStart < 0 ? 0 : lineStart + 1));
                if (Regex.IsMatch(prefix, @"(?im)^\s*[A-Z]:\s+(?:OperatingSystem|Data)\b") ||
                    Regex.IsMatch(prefix, @"(?i)\b(version|versión|driver|controlador|build|compilación|firmware)\b[^\r\n]*$"))
                    return candidate;
                if (TimeSpan.TryParseExact(candidate, new[] { @"h\:mm\:ss", @"hh\:mm\:ss", @"m\:ss" }, CultureInfo.InvariantCulture, out _))
                    return candidate;
                return IPAddress.TryParse(candidate, out var address) && address.AddressFamily == AddressFamily.InterNetworkV6
                    ? "[IPv6 OCULTA]"
                    : candidate;
            });
        content = Regex.Replace(content, @"(?i)\bS-1-5-21-(?:\d+-){3}\d+\b", "[SID OCULTO]");
        content = Regex.Replace(content, @"(?i)C:\\Users\\[^\\\r\n]+", @"C:\Users\[USUARIO OCULTO]");
        content = Regex.Replace(content, @"(?im)^(Usuario|Usuario actual|User|Equipo|ComputerName|SerialNumber|UUID)\s*:\s*.*$", "$1: [OCULTO]");
        content = Regex.Replace(content, @"(?im)^\s*Usuario\s*:\s*.*$", "Usuario: [OCULTO]");
        content = Regex.Replace(content, @"(?im)^\s*SSID\s*:\s*.*$", "SSID: [RED OCULTA]");
        content = Regex.Replace(content, @"(?im)^\s*Nombre de perfil\s*:\s*.*$", "Nombre de perfil: [RED OCULTA]");
        return content;
    }

    private void RelaunchAsAdministrator()
    {
        if (isAdmin || isRunning) return;
        var answer = MessageBox.Show(
            this,
            "Se abrirá otra instancia con permisos de administrador. Esto permite ejecutar DISM, SFC, la consulta de estado CHKDSK y comprobaciones de seguridad adicionales.\r\n\r\nLa aplicación seguirá siendo de solo lectura: no reparará ni modificará el sistema.",
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
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "3.1.0";
}
