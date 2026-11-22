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
    private readonly Button runButton = new();
    private readonly Button cancelButton = new();
    private readonly Button exportButton = new();
    private readonly Button adminButton = new();
    private readonly Label statusLabel = new();
    private readonly Label pageTitle = new();
    private readonly Label pageSubtitle = new();
    private readonly ProgressBar progressBar = new();
    private readonly FlowLayoutPanel metricPanel = new();
    private readonly RichTextBox overviewOutput = new();
    private readonly CheckBox anonymizeExport = new();
    private CancellationTokenSource? cancellation;
    private DiagnosticReport? report;
    private string activePage = "Inicio";
    private bool isRunning;
    private readonly bool isAdmin = DiagnosticEngine.IsAdministrator();

    private static readonly Color Background = Color.FromArgb(14, 17, 23);
    private static readonly Color Sidebar = Color.FromArgb(19, 23, 31);
    private static readonly Color Surface = Color.FromArgb(25, 30, 40);
    private static readonly Color SurfaceRaised = Color.FromArgb(31, 37, 49);
    private static readonly Color Border = Color.FromArgb(48, 57, 72);
    private static readonly Color Primary = Color.FromArgb(70, 130, 210);
    private static readonly Color Muted = Color.FromArgb(155, 166, 182);
    private static readonly Color Good = Color.FromArgb(75, 190, 135);
    private static readonly Color Warning = Color.FromArgb(232, 176, 75);
    private static readonly Color Danger = Color.FromArgb(230, 100, 100);
    private static readonly Color Accent = Color.FromArgb(105, 155, 225);

    public MainForm()
    {
        Text = "Diagnóstico PC";
        MinimumSize = new Size(1180, 760);
        Size = new Size(1360, 880);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9.5F);
        DoubleBuffered = true;

        BuildShell();
        ShowPage("Inicio");
        UpdateAdminButton();
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebar = BuildSidebar();
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Background,
            Margin = Padding.Empty
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var header = BuildHeader();
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(20, 12, 20, 10) };
        var footer = BuildFooter();

        BuildPages(content);

        main.Controls.Add(header, 0, 0);
        main.Controls.Add(content, 0, 1);
        main.Controls.Add(footer, 0, 2);
        root.Controls.Add(sidebar, 0, 0);
        root.Controls.Add(main, 1, 0);
        Controls.Add(root);
    }

    private Control BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(16, 22, 16, 14) };
        var brand = new Label
        {
            Text = "DIAGNÓSTICO\\r\\nPC",
            Font = new Font("Segoe UI Semibold", 21F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            Height = 66,
            Dock = DockStyle.Top
        };
        var version = new Label
        {
            Text = "CENTRO DE DIAGNÓSTICO  ·  v" + GetVersion(),
            Font = new Font("Segoe UI", 8F),
            ForeColor = Muted,
            Dock = DockStyle.Top,
            Height = 26
        };

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Sidebar,
            Padding = new Padding(0, 14, 0, 0)
        };

        AddNav(nav, "Inicio", "Resumen general");
        AddNav(nav, "Hardware", "Componentes y discos");
        AddNav(nav, "Seguridad", "Protección del equipo");
        AddNav(nav, "Red", "Conectividad y Wi-Fi");
        AddNav(nav, "Windows", "Integridad y estabilidad");
        AddNav(nav, "Rendimiento", "Uso y aplicaciones");
        AddNav(nav, "Dispositivos", "Controladores y errores");

        var bottom = new Label
        {
            Text = "Análisis local · Solo lectura\\r\\nNo se envían datos a servidores.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Muted,
            Dock = DockStyle.Bottom,
            Height = 48,
            TextAlign = ContentAlignment.BottomLeft
        };

        panel.Controls.Add(nav);
        panel.Controls.Add(version);
        panel.Controls.Add(brand);
        panel.Controls.Add(bottom);
        return panel;
    }

    private void AddNav(FlowLayoutPanel nav, string name, string description)
    {
        var button = new Button
        {
            Text = "  " + name + "\\r\\n     " + description,
            Width = 190,
            Height = 55,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Sidebar,
            ForeColor = Color.FromArgb(205, 213, 224),
            Font = new Font("Segoe UI", 9.5F),
            Margin = new Padding(0, 3, 0, 5),
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
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(20, 13, 18, 8) };
        pageTitle.Text = "Resumen general";
        pageTitle.Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold);
        pageTitle.ForeColor = Color.White;
        pageTitle.AutoSize = true;
        pageTitle.Location = new Point(0, 5);

        pageSubtitle.Text = "Análisis técnico del equipo y de Windows";
        pageSubtitle.Font = new Font("Segoe UI", 9F);
        pageSubtitle.ForeColor = Muted;
        pageSubtitle.AutoSize = true;
        pageSubtitle.Location = new Point(2, 44);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 675,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Background,
            Padding = new Padding(0, 8, 0, 0)
        };

        ConfigureButton(runButton, "INICIAR ANÁLISIS", Primary, Color.White, 164);
        runButton.Click += async (_, _) => await StartDiagnosticAsync();
        ConfigureButton(cancelButton, "CANCELAR", SurfaceRaised, Color.White, 105);
        cancelButton.Enabled = false;
        cancelButton.Click += (_, _) => cancellation?.Cancel();
        ConfigureButton(exportButton, "EXPORTAR", SurfaceRaised, Color.White, 112);
        exportButton.Enabled = false;
        exportButton.Click += (_, _) => ExportReport();
        ConfigureButton(adminButton, isAdmin ? "ADMINISTRADOR ACTIVO" : "EJECUTAR COMO ADMIN", SurfaceRaised, Color.White, 180);
        adminButton.Click += (_, _) => RelaunchAsAdministrator();

        buttons.Controls.Add(adminButton);
        buttons.Controls.Add(exportButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(runButton);

        header.Controls.Add(buttons);
        header.Controls.Add(pageTitle);
        header.Controls.Add(pageSubtitle);
        return header;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            Padding = new Padding(16, 8, 16, 6)
        };
        statusLabel.Text = "Listo. El análisis se inicia manualmente.";
        statusLabel.ForeColor = Muted;
        statusLabel.AutoEllipsis = true;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        progressBar.Dock = DockStyle.Right;
        progressBar.Width = 190;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        progressBar.Margin = new Padding(8, 5, 8, 5);

        anonymizeExport.Text = "Anonimizar datos al exportar";
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
        var dashboard = BuildDashboard();
        pages["Inicio"] = dashboard;
        pages["Hardware"] = BuildReportPage("Hardware y almacenamiento", "Inventario, componentes, unidades, espacio y fiabilidad.");
        pages["Seguridad"] = BuildReportPage("Seguridad", "Microsoft Defender, firewall, TPM, Secure Boot, BitLocker y protecciones.");
        pages["Red"] = BuildReportPage("Red y conectividad", "Ethernet, Wi-Fi, IP, DNS, proxy, conectividad y puertos locales.");
        pages["Windows"] = BuildReportPage("Windows y estabilidad", "Integridad del sistema, servicios, actualizaciones y eventos.");
        pages["Rendimiento"] = BuildReportPage("Rendimiento", "Uso de recursos, procesos y aplicaciones de inicio.");
        pages["Dispositivos"] = BuildReportPage("Dispositivos y controladores", "Estados del Administrador de dispositivos y códigos de error.");

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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var intro = new Label
        {
            Text = "Visión general del estado del equipo. Los resultados se actualizan al finalizar cada prueba.",
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

        var summary = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(14, 8, 14, 8) };
        var summaryLabel = new Label
        {
            Text = "INFORME DE ANÁLISIS",
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Accent,
            Dock = DockStyle.Top,
            Height = 22
        };
        var summaryText = new Label
        {
            Text = "Todavía no se ejecutó ningún diagnóstico.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.Gainsboro,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Name = "dashboardSummary"
        };
        summary.Controls.Add(summaryText);
        summary.Controls.Add(summaryLabel);

        var detailsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(12) };
        var detailsTitle = new Label
        {
            Text = "HALLAZGOS Y RECOMENDACIONES",
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Accent,
            Dock = DockStyle.Top,
            Height = 24
        };
        overviewOutput.Dock = DockStyle.Fill;
        overviewOutput.ReadOnly = true;
        overviewOutput.BackColor = Surface;
        overviewOutput.ForeColor = Color.Gainsboro;
        overviewOutput.BorderStyle = BorderStyle.None;
        overviewOutput.Font = new Font("Segoe UI", 9.5F);
        overviewOutput.WordWrap = true;
        overviewOutput.Text = "Cuando termine el análisis, acá vas a encontrar los puntos que requieren atención y las pruebas que no pudieron verificarse.";
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var intro = new Label
        {
            Text = title + "\\r\\n" + description,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var output = CreateOutputBox();
        pageOutputs[title] = output;

        var container = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(14) };
        container.Controls.Add(output);
        layout.Controls.Add(intro, 0, 0);
        layout.Controls.Add(container, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private static RichTextBox CreateOutputBox()
    {
        return new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Surface,
            ForeColor = Color.FromArgb(218, 224, 233),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9.2F),
            WordWrap = true,
            DetectUrls = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            HideSelection = false
        };
    }

    private void ShowPage(string name)
    {
        if (!pages.TryGetValue(name, out var selected)) return;
        activePage = name;
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
            ? "Análisis técnico del equipo y de Windows"
            : "Resultados detallados · Solo lectura";

        foreach (Control control in FindNavButtons())
        {
            if (control is Button button)
            {
                bool active = Equals(button.Tag, name);
                button.BackColor = active ? SurfaceRaised : Sidebar;
                button.ForeColor = active ? Color.White : Color.FromArgb(205, 213, 224);
                button.FlatAppearance.BorderColor = active ? Primary : Sidebar;
                button.FlatAppearance.BorderSize = active ? 1 : 0;
            }
        }
    }

    private IEnumerable<Control> FindNavButtons()
    {
        var sidebar = Controls.OfType<TableLayoutPanel>().FirstOrDefault();
        if (sidebar is null || sidebar.Controls.Count == 0) yield break;
        var panel = sidebar.Controls[0] as Panel;
        if (panel is null) yield break;
        foreach (Control child in panel.Controls)
        {
            if (child is FlowLayoutPanel flow)
                foreach (Control item in flow.Controls) yield return item;
        }
    }

    private async Task StartDiagnosticAsync()
    {
        if (isRunning) return;
        isRunning = true;
        cancellation = new CancellationTokenSource();
        report = null;
        runButton.Enabled = false;
        cancelButton.Enabled = true;
        exportButton.Enabled = false;
        progressBar.Value = 0;
        ClearOutputs();
        ShowPage("Inicio");
        statusLabel.Text = "Preparando diagnóstico...";
        overviewOutput.Text = "Ejecutando las pruebas en secuencia. Las comprobaciones profundas pueden tardar varios minutos.";
        AddMetricCards(0, 0, 0, 0);

        try
        {
            var progress = new Progress<DiagnosticProgress>(p =>
            {
                int value = p.Total <= 0 ? 0 : (int)Math.Round((double)p.Completed / p.Total * 100);
                progressBar.Value = Math.Clamp(value, 0, 100);
                statusLabel.Text = p.Completed >= p.Total
                    ? "Finalizando informe..."
                    : $"Prueba {Math.Min(p.Completed + 1, p.Total)} de {p.Total}: {p.CurrentTitle}";
            });

            var entryProgress = new Progress<DiagnosticEntry>(entry => AppendEntry(entry));
            report = await engine.RunAsync(progress, entryProgress, cancellation.Token);
            RefreshDashboard(report);
            exportButton.Enabled = report.Entries.Count > 0;
            progressBar.Value = report.Cancelled ? Math.Min(progressBar.Value, 99) : 100;
            statusLabel.Text = report.Cancelled
                ? "Análisis cancelado. Se conservaron los resultados obtenidos."
                : $"Análisis finalizado en {report.Duration:hh\\:mm\\:ss}.";
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
        }
    }

    private void ClearOutputs()
    {
        foreach (var output in pageOutputs.Values) output.Clear();
    }

    private void AppendEntry(DiagnosticEntry entry)
    {
        string page = entry.Area switch
        {
            "Hardware" or "Almacenamiento" => "Hardware",
            "Seguridad" => "Seguridad",
            "Red" => "Red",
            "Rendimiento" => "Rendimiento",
            "Dispositivos" => "Dispositivos",
            _ => "Windows"
        };

        string pageKey = page switch
        {
            "Hardware" => "Hardware y almacenamiento",
            "Seguridad" => "Seguridad",
            "Red" => "Red y conectividad",
            "Rendimiento" => "Rendimiento",
            "Dispositivos" => "Dispositivos y controladores",
            _ => "Windows y estabilidad"
        };

        if (!pageOutputs.TryGetValue(pageKey, out var output)) return;
        output.SelectionStart = output.TextLength;
        output.SelectionColor = LevelColor(entry.Level);
        output.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        output.AppendText($"{LevelText(entry.Level)}  {entry.Title}\\r\\n");
        output.SelectionColor = Muted;
        output.SelectionFont = new Font("Segoe UI", 8.5F);
        output.AppendText($"Duración: {entry.Duration:mm\\:ss}  |  Código de salida: {(entry.ExitCode?.ToString() ?? "No disponible")}\\r\\n");
        output.SelectionColor = Color.FromArgb(218, 224, 233);
        output.SelectionFont = new Font("Consolas", 9F);
        output.AppendText(entry.Details + "\\r\\n\\r\\n");
        output.SelectionStart = output.TextLength;
        output.ScrollToCaret();
    }

    private void RefreshDashboard(DiagnosticReport value)
    {
        int ok = value.Entries.Count(x => x.Level == FindingLevel.Ok);
        int warnings = value.Entries.Count(x => x.Level == FindingLevel.Warning);
        int critical = value.Entries.Count(x => x.Level == FindingLevel.Critical);
        int notVerified = value.Entries.Count(x => x.Level == FindingLevel.NotVerified);
        AddMetricCards(ok, warnings, critical, notVerified);

        var summary = Controls.Find("dashboardSummary", true).FirstOrDefault() as Label;
        if (summary is not null)
        {
            summary.Text = $"Inicio: {value.StartedAt:dd/MM/yyyy HH:mm:ss}    |    Duración: {value.Duration:hh\\:mm\\:ss}    |    " +
                           $"Permisos: {(value.RanElevated ? "Administrador" : "Usuario estándar")}    |    " +
                           $"Pruebas: {value.Entries.Count}    |    Estado: {(value.Cancelled ? "Cancelado" : "Finalizado")}";
        }

        overviewOutput.Clear();
        var important = value.Entries
            .Where(x => x.Level is FindingLevel.Critical or FindingLevel.Warning or FindingLevel.NotVerified)
            .OrderByDescending(x => x.Level)
            .ToList();

        if (important.Count == 0)
        {
            overviewOutput.Text = "No se detectaron advertencias en las comprobaciones realizadas. Esto no equivale a una garantía de ausencia de malware ni sustituye pruebas de hardware específicas del fabricante.";
        }
        else
        {
            foreach (var item in important)
            {
                overviewOutput.SelectionColor = LevelColor(item.Level);
                overviewOutput.SelectionFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                overviewOutput.AppendText($"{LevelText(item.Level)}  {item.Title}\\r\\n");
                overviewOutput.SelectionColor = Color.Gainsboro;
                overviewOutput.SelectionFont = new Font("Segoe UI", 9F);
                overviewOutput.AppendText(GetRecommendation(item) + "\\r\\n\\r\\n");
            }
        }

        if (value.Entries.Any(x => (x.Id is "dism" or "sfc") && x.Level == FindingLevel.NotVerified))
        {
            overviewOutput.AppendText("\\r\\nAlgunas pruebas de integridad requieren iniciar la aplicación como administrador y volver a ejecutar el análisis.");
        }
    }

    private void AddMetricCards(int ok, int warnings, int critical, int notVerified)
    {
        metricPanel.SuspendLayout();
        metricPanel.Controls.Clear();
        metricPanel.Controls.Add(CreateMetricCard("CORRECTAS", ok.ToString(), "Pruebas verificadas", Good));
        metricPanel.Controls.Add(CreateMetricCard("ADVERTENCIAS", warnings.ToString(), "Revisar los detalles", Warning));
        metricPanel.Controls.Add(CreateMetricCard("CRÍTICAS", critical.ToString(), "Requieren atención", Danger));
        metricPanel.Controls.Add(CreateMetricCard("NO VERIFICADAS", notVerified.ToString(), "Permisos o datos no disponibles", Muted));
        metricPanel.ResumeLayout();
    }

    private Control CreateMetricCard(string title, string number, string subtitle, Color color)
    {
        var card = new Panel
        {
            Width = 205,
            Height = 94,
            BackColor = Surface,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(12, 8, 10, 7)
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
            Filter = "Informe HTML imprimible (*.html)|*.html|Informe de texto (*.txt)|*.txt",
            FilterIndex = 1,
            FileName = "DiagnosticoPC_" + report.StartedAt.ToString("yyyyMMdd_HHmm") + (anonymizeExport.Checked ? "_anonimo" : "")
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
            MessageBox.Show(this, "El informe se guardó correctamente.", "Exportación completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo guardar el informe: " + ex.Message, "Error de exportación", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string BuildTextReport(DiagnosticReport value)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DIAGNÓSTICO PC");
        sb.AppendLine("Inicio: " + value.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"));
        sb.AppendLine("Fin: " + value.FinishedAt.ToString("dd/MM/yyyy HH:mm:ss"));
        sb.AppendLine("Duración: " + value.Duration.ToString(@"hh\:mm\:ss"));
        sb.AppendLine("Permisos de administrador: " + (value.RanElevated ? "Sí" : "No"));
        sb.AppendLine("Estado: " + (value.Cancelled ? "Cancelado" : "Finalizado"));
        sb.AppendLine();
        sb.AppendLine("RESUMEN");
        foreach (var level in Enum.GetValues<FindingLevel>())
            sb.AppendLine($"{LevelText(level)}: {value.Entries.Count(x => x.Level == level)}");
        foreach (var entry in value.Entries)
        {
            sb.AppendLine();
            sb.AppendLine(new string('=', 84));
            sb.AppendLine($"{LevelText(entry.Level)} | {entry.Area} | {entry.Title}");
            sb.AppendLine("Duración: " + entry.Duration.ToString(@"mm\:ss"));
            sb.AppendLine("Código de salida: " + (entry.ExitCode?.ToString() ?? "No disponible"));
            sb.AppendLine(entry.Details);
        }
        sb.AppendLine();
        sb.AppendLine("LÍMITES");
        sb.AppendLine("Las pruebas dependen de los datos que expongan Windows, el firmware y los controladores.");
        sb.AppendLine("Un resultado no verificado no significa que el componente esté fallando.");
        sb.AppendLine("Este informe no garantiza ausencia total de malware ni sustituye un análisis especializado.");
        sb.AppendLine("Diagnóstico PC funciona en modo de consulta: no repara ni modifica la configuración del sistema.");
        return sb.ToString();
    }

    private static string BuildHtmlReport(DiagnosticReport value)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Informe de Diagnóstico PC</title>
<style>
:root{color-scheme:light}*{box-sizing:border-box}body{margin:0;background:#edf1f5;color:#202938;font:14px/1.55 "Segoe UI",Arial,sans-serif}
main{max-width:1100px;margin:28px auto;padding:0 18px}.hero{background:#172233;color:#fff;padding:28px 32px;border-radius:14px}
h1{font-size:28px;margin:0 0 8px}.muted{color:#aab7c8}.meta{display:flex;flex-wrap:wrap;gap:18px;margin-top:16px}
.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:18px 0}.card,.section{background:#fff;border:1px solid #d9e0e8;border-radius:12px;padding:16px}
.card b{display:block;font-size:27px}.ok{color:#188454}.warning{color:#a66b0a}.critical{color:#bd3434}.notverified{color:#687587}.info{color:#426b9d}
.section{margin:14px 0}.section h2{font-size:17px;margin:0 0 10px}.entry{border-top:1px solid #e6ebf0;padding:14px 0;page-break-inside:avoid}.entry:first-of-type{border-top:0}
.entry h3{font-size:14px;margin:0 0 5px}.detail{white-space:pre-wrap;overflow-wrap:anywhere;background:#f7f9fb;border-radius:8px;padding:12px;font:12px/1.5 Consolas,monospace;max-height:none}
.small{color:#64748b;font-size:12px}.footer{color:#64748b;font-size:12px;margin:20px 2px}
@media(max-width:700px){.grid{grid-template-columns:repeat(2,1fr)}.hero{padding:20px}}
@media print{body{background:#fff}main{margin:0 auto;max-width:none}.hero{border-radius:0}.section,.card{break-inside:avoid}.detail{background:#f6f6f6}}
</style></head><body><main>
""");
        sb.Append("<div class=\"hero\"><h1>Diagnóstico PC</h1><div class=\"muted\">Informe técnico local · Solo lectura</div><div class=\"meta\">");
        sb.Append("<span>Inicio: ").Append(WebUtility.HtmlEncode(value.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"))).Append("</span>");
        sb.Append("<span>Duración: ").Append(WebUtility.HtmlEncode(value.Duration.ToString(@"hh\:mm\:ss"))).Append("</span>");
        sb.Append("<span>Permisos: ").Append(value.RanElevated ? "Administrador" : "Usuario estándar").Append("</span>");
        sb.Append("<span>Estado: ").Append(value.Cancelled ? "Cancelado" : "Finalizado").Append("</span></div></div>");

        sb.Append("<div class=\"grid\">");
        AddHtmlCard(sb, "Correctas", value.Entries.Count(x => x.Level == FindingLevel.Ok), "ok");
        AddHtmlCard(sb, "Advertencias", value.Entries.Count(x => x.Level == FindingLevel.Warning), "warning");
        AddHtmlCard(sb, "Críticas", value.Entries.Count(x => x.Level == FindingLevel.Critical), "critical");
        AddHtmlCard(sb, "No verificadas", value.Entries.Count(x => x.Level == FindingLevel.NotVerified), "notverified");
        sb.Append("</div>");

        foreach (var group in value.Entries.GroupBy(x => x.Area))
        {
            sb.Append("<section class=\"section\"><h2>").Append(WebUtility.HtmlEncode(group.Key)).Append("</h2>");
            foreach (var entry in group)
            {
                string css = entry.Level.ToString().ToLowerInvariant();
                sb.Append("<div class=\"entry\"><h3 class=\"").Append(css).Append("\">")
                  .Append(WebUtility.HtmlEncode(LevelText(entry.Level))).Append(" · ")
                  .Append(WebUtility.HtmlEncode(entry.Title)).Append("</h3>");
                sb.Append("<div class=\"small\">Duración: ").Append(WebUtility.HtmlEncode(entry.Duration.ToString(@"mm\:ss")))
                  .Append(" · Código de salida: ").Append(WebUtility.HtmlEncode(entry.ExitCode?.ToString() ?? "No disponible")).Append("</div>");
                sb.Append("<pre class=\"detail\">").Append(WebUtility.HtmlEncode(entry.Details)).Append("</pre></div>");
            }
            sb.Append("</section>");
        }

        sb.Append("<div class=\"footer\">Las pruebas dependen de los datos que expongan Windows, el firmware y los controladores. Un resultado no verificado no significa que el componente esté fallando. Este informe no garantiza ausencia total de malware. No se realizaron reparaciones ni cambios de configuración.</div>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AddHtmlCard(StringBuilder sb, string title, int count, string css)
    {
        sb.Append("<div class=\"card\"><span class=\"small\">").Append(WebUtility.HtmlEncode(title))
          .Append("</span><b class=\"").Append(css).Append("\">").Append(count).Append("</b></div>");
    }

    private static string Anonymize(string content)
    {
        content = Regex.Replace(content, @"\b(?:[0-9a-fA-F]{2}[:-]){5}[0-9a-fA-F]{2}\b", "[MAC OCULTA]");
        content = Regex.Replace(content, @"\b(?:\d{1,3}\.){3}\d{1,3}\b", "[IP OCULTA]");
        content = Regex.Replace(content, @"(?i)\bS-1-5-21-(?:\d+-){3}\d+\b", "[SID OCULTO]");
        content = Regex.Replace(content, @"(?i)C:\\Users\\[^\\\r\n]+", @"C:\Users\[USUARIO OCULTO]");
        content = Regex.Replace(content, @"(?im)^(Usuario|Usuario actual|Equipo|SerialNumber|UUID)\s*:\s*.*$", "$1: [OCULTO]");
        return content;
    }

    private void RelaunchAsAdministrator()
    {
        if (isAdmin || isRunning) return;
        var answer = MessageBox.Show(
            this,
            "Se abrirá otra instancia con permisos de administrador. Esto permite ejecutar DISM, SFC, Secure Boot y verificaciones adicionales.\\r\\n\\r\\nEl programa seguirá siendo de solo lectura: no reparará ni modificará el sistema.",
            "Permisos de administrador",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);
        if (answer != DialogResult.OK) return;

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = "--admin-relaunch",
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(start);
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
        adminButton.Text = isAdmin ? "ADMINISTRADOR ACTIVO" : "EJECUTAR COMO ADMIN";
        adminButton.Enabled = !isAdmin && !isRunning;
        adminButton.BackColor = isAdmin ? Color.FromArgb(38, 87, 70) : SurfaceRaised;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (isRunning) cancellation?.Cancel();
        base.OnFormClosing(e);
    }

    private static string GetRecommendation(DiagnosticEntry entry) => entry.Id switch
    {
        "storage" => "Revisá el espacio libre y los contadores de fiabilidad. La ausencia de datos SMART no significa que el disco esté sano ni averiado.",
        "defender" => "Comprobá que la protección en tiempo real esté habilitada y que las firmas estén actualizadas. Una consulta de estado no reemplaza un análisis completo.",
        "firewall" => "Revisá el perfil señalado desde Seguridad de Windows. No se modificó ninguna regla.",
        "devices" => "Abrí el Administrador de dispositivos y examiná el código concreto. El código 22 indica deshabilitado; puede ser intencional.",
        "security-posture" => "Revisá la compatibilidad de TPM, Secure Boot, UAC y BitLocker según tu configuración y necesidades.",
        "network" => "Compará DNS, puerta de enlace y conexión HTTPS. Un ping sin respuesta no implica necesariamente una falla de Internet.",
        "dism" or "sfc" => "Volvé a ejecutar el diagnóstico como administrador para obtener una verificación completa. Estas pruebas no reparan.",
        "chkdsk" => "Revisá el resultado de la exploración NTFS. No se utilizaron parámetros de reparación.",
        "events" => "Buscá recurrencia y relación temporal. Los eventos aislados de DCOM no deben considerarse automáticamente fallas críticas.",
        "updates" => "Si Windows indica reinicio pendiente, guardá tu trabajo y reiniciá cuando sea conveniente.",
        _ => "Abrí el detalle de esta sección para revisar la evidencia y las limitaciones de la consulta."
    };

    private static string LevelText(FindingLevel level) => level switch
    {
        FindingLevel.Ok => "CORRECTO",
        FindingLevel.Info => "INFORMATIVO",
        FindingLevel.Warning => "ADVERTENCIA",
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
        button.Height = 40;
        button.Margin = new Padding(6, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.BackColor = back;
        button.ForeColor = fore;
        button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
}
