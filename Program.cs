using System.Diagnostics;
using System.Text;

namespace DiagnosticoPC;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly RichTextBox output = new();
    private readonly Button runButton = new();
    private readonly Button exportButton = new();
    private readonly Label status = new();

    public MainForm()
    {
        Text = "Diagnóstico PC";
        MinimumSize = new Size(820, 600);
        Size = new Size(1000, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 20, 25);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 10F);

        var header = new Label {
            Text = "DIAGNÓSTICO DEL SISTEMA",
            Font = new Font("Segoe UI Semibold", 18F),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(18, 14, 0, 0)
        };

        var sub = new Label {
            Text = "Análisis de solo lectura · No elimina archivos ni cambia configuraciones",
            ForeColor = Color.Silver,
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(20, 0, 0, 0)
        };

        output.Dock = DockStyle.Fill;
        output.ReadOnly = true;
        output.BackColor = Color.FromArgb(25, 28, 34);
        output.ForeColor = Color.FromArgb(220, 224, 230);
        output.BorderStyle = BorderStyle.FixedSingle;
        output.Font = new Font("Consolas", 9.5F);
        output.WordWrap = false;

        runButton.Text = "INICIAR DIAGNÓSTICO";
        runButton.Width = 220;
        runButton.Height = 42;
        runButton.FlatStyle = FlatStyle.Flat;
        runButton.BackColor = Color.FromArgb(47, 91, 145);
        runButton.ForeColor = Color.White;
        runButton.Click += async (_, _) => await RunDiagnostic();

        exportButton.Text = "EXPORTAR INFORME";
        exportButton.Width = 180;
        exportButton.Height = 42;
        exportButton.FlatStyle = FlatStyle.Flat;
        exportButton.BackColor = Color.FromArgb(55, 62, 73);
        exportButton.ForeColor = Color.White;
        exportButton.Enabled = false;
        exportButton.Click += (_, _) => ExportReport();

        status.Text = "Listo. Ejecutá el diagnóstico cuando quieras.";
        status.AutoEllipsis = true;
        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.ForeColor = Color.Silver;

        var buttons = new FlowLayoutPanel {
            Dock = DockStyle.Bottom,
            Height = 64,
            Padding = new Padding(14, 10, 14, 8),
            BackColor = Color.FromArgb(22, 25, 31),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        buttons.Controls.Add(runButton);
        buttons.Controls.Add(exportButton);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(16, 0, 12, 0) };
        statusPanel.Controls.Add(status);

        Controls.Add(output);
        Controls.Add(statusPanel);
        Controls.Add(buttons);
        Controls.Add(sub);
        Controls.Add(header);

        Append("DIAGNÓSTICO PC\r\n");
        Append("Este programa realiza consultas de solo lectura. No repara, limpia, borra ni modifica el sistema.\r\n");
        Append("Algunas pruebas pueden tardar varios minutos. No cierres la aplicación mientras se ejecutan.\r\n\r\n");
    }

    private void Append(string text)
    {
        output.AppendText(text);
        output.SelectionStart = output.TextLength;
        output.ScrollToCaret();
    }

    private async Task<string> PowerShell(string script, int timeoutMs = 60000)
    {
        // Evita que PowerShell envíe progreso en formato CLIXML y fuerza UTF-8 real
        // para conservar tildes, eñes y caracteres del informe.
        string preparedScript =
            "$ProgressPreference = 'SilentlyContinue'\r\n" +
            "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)\r\n" +
            "$OutputEncoding = New-Object System.Text.UTF8Encoding($false)\r\n" +
            script;
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(preparedScript));
        return await RunProcess("powershell.exe",
            "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
            timeoutMs);
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private async Task<string> RunProcess(string file, string args, int timeoutMs)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo(file, args) {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            p.Start();
            Task<string> stdout = p.StandardOutput.ReadToEndAsync();
            Task<string> stderr = p.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(timeoutMs);
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) {
                try { p.Kill(true); } catch { }
                return "Tiempo de espera agotado. La prueba fue detenida.";
            }
            var result = await stdout;
            var error = await stderr;
            if (string.IsNullOrWhiteSpace(result)) result = error;
            else if (!string.IsNullOrWhiteSpace(error) && !error.Contains("#< CLIXML", StringComparison.OrdinalIgnoreCase))
                result += Environment.NewLine + error;

            // Elimina cualquier bloque de progreso serializado que pudiera emitir Windows PowerShell.
            int clixml = result.IndexOf("#< CLIXML", StringComparison.OrdinalIgnoreCase);
            if (clixml >= 0) result = result[..clixml].Trim();

            return string.IsNullOrWhiteSpace(result) ? "(La consulta no devolvió datos.)" : result.Trim();
        }
        catch (Exception ex) { return "No se pudo ejecutar la consulta: " + ex.Message; }
    }

    private async Task Section(string title, string script, int timeoutMs = 60000)
    {
        Append("\r\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\r\n");
        Append(title + "\r\n");
        Append("Consultando...\r\n");
        status.Text = "Ejecutando: " + title;
        var timer = Stopwatch.StartNew();
        var result = await PowerShell(script, timeoutMs);
        timer.Stop();
        result += "\r\nDuración de esta prueba: " + timer.Elapsed.ToString(@"mm\:ss");
        var marker = "Consultando...\r\n";
        var text = output.Text;
        int idx = text.LastIndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0) {
            output.Select(idx, marker.Length);
            output.SelectedText = result + "\r\n";
        } else Append(result + "\r\n");
        output.SelectionStart = output.TextLength;
        output.ScrollToCaret();
    }

    private async Task RunDiagnostic()
    {
        runButton.Enabled = false;
        exportButton.Enabled = false;
        output.Clear();
        Append("DIAGNÓSTICO PC — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n");
        Append("Modo: SOLO LECTURA. No se realizan reparaciones ni cambios.\r\n");

        await Section("1. WINDOWS Y EQUIPO", @"
'Equipo: ' + $env:COMPUTERNAME
'Usuario actual: ' + [Security.Principal.WindowsIdentity]::GetCurrent().Name
'Windows: ' + (Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty Caption)
'Versión: ' + (Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty Version)
'Compilación: ' + (Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty BuildNumber)
'Arquitectura: ' + (Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty OSArchitecture)
'Último inicio: ' + (Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty LastBootUpTime)
");

        await Section("2. CPU, MEMORIA Y PLACA MADRE", @"
Get-CimInstance Win32_Processor | Format-List Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,LoadPercentage
Get-CimInstance Win32_ComputerSystem | Format-List Manufacturer,Model,TotalPhysicalMemory
Get-CimInstance Win32_BaseBoard | Format-List Manufacturer,Product,Version
$os=Get-CimInstance Win32_OperatingSystem
'RAM disponible (GB): ' + [math]::Round($os.FreePhysicalMemory/1MB,2)
'RAM usada aproximada (%): ' + [math]::Round((1-$os.FreePhysicalMemory/$os.TotalVisibleMemorySize)*100,1)
");

        await Section("3. GPU Y PANTALLAS", @"
Get-CimInstance Win32_VideoController | Format-List Name,DriverVersion,AdapterRAM,VideoModeDescription,Status
");

        await Section("4. DISCOS Y ESPACIO", @"
Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | Select-Object DeviceID,VolumeName,FileSystem,@{N='TotalGB';E={[math]::Round($_.Size/1GB,1)}},@{N='LibreGB';E={[math]::Round($_.FreeSpace/1GB,1)}},@{N='LibrePorcentaje';E={if($_.Size){[math]::Round($_.FreeSpace/$_.Size*100,1)}else{0}}} | Format-Table -AutoSize
");

        await Section("5. PROCESOS Y APLICACIONES DE INICIO", @"
'Procesos con mayor memoria utilizada:'
Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 20 ProcessName,Id,@{N='RAM_MB';E={[math]::Round($_.WorkingSet64/1MB,1)}},@{N='CPU_acumulada_s';E={if($_.CPU){[math]::Round($_.CPU,1)}else{0}}} | Format-Table -AutoSize
'Aplicaciones configuradas para iniciar con Windows:'
Get-CimInstance Win32_StartupCommand | Select-Object Name,Command,Location,User | Format-List
");

        await Section("6. MICROSOFT DEFENDER", @"
try {
  Get-MpComputerStatus | Select-Object AMServiceEnabled,AntivirusEnabled,RealTimeProtectionEnabled,BehaviorMonitorEnabled,IoavProtectionEnabled,AntivirusSignatureVersion,AntivirusSignatureLastUpdated,QuickScanAge,FullScanAge | Format-List
  'Detecciones registradas (últimas 20):'
  Get-MpThreatDetection -ErrorAction Stop | Sort-Object InitialDetectionTime -Descending | Select-Object -First 20 ThreatID,ThreatStatusID,InitialDetectionTime,ActionSuccess,Resources | Format-List
} catch { 'No se pudo consultar Defender: ' + $_.Exception.Message }
", 90000);

        await Section("7. FIREWALL DE WINDOWS", @"
try { Get-NetFirewallProfile | Format-Table Name,Enabled,DefaultInboundAction,DefaultOutboundAction -AutoSize }
catch { 'No se pudo consultar Firewall: ' + $_.Exception.Message }
");

        await Section("8. ESTADO FÍSICO Y SALUD DE LOS DISCOS", @"
'Unidades físicas detectadas:'
try {
  Get-PhysicalDisk | Select-Object FriendlyName,MediaType,BusType,HealthStatus,OperationalStatus,@{N='TamañoGB';E={[math]::Round($_.Size/1GB,1)}} | Format-Table -AutoSize
} catch { 'Estado de almacenamiento no disponible: ' + $_.Exception.Message }
'Indicadores de fallo predictivo SMART (si el controlador los expone):'
try {
  Get-CimInstance -Namespace root/wmi -ClassName MSStorageDriver_FailurePredictStatus -ErrorAction Stop | Select-Object InstanceName,PredictFailure,Reason | Format-Table -AutoSize
} catch { 'El controlador no expone indicadores SMART por esta interfaz.' }
", 90000);

        await Section("9. CONTROLADORES CON ESTADO ANORMAL", @"
$bad = Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.ConfigManagerErrorCode -ne 0 }
if ($bad) { $bad | Select-Object Name,Manufacturer,ConfigManagerErrorCode,Status | Format-Table -AutoSize }
else { 'No se detectaron dispositivos con código de error en el Administrador de dispositivos.' }
", 90000);

        await Section("10. ARRANQUE SEGURO (SECURE BOOT)", @"
try { 'Secure Boot habilitado: ' + (Confirm-SecureBootUEFI) }
catch { 'No disponible o no compatible en esta sesión: ' + $_.Exception.Message }
");

        await Section("11. RED Y WI-FI (SIN CONTRASEÑAS)", @"
Get-NetAdapter | Select-Object Name,InterfaceDescription,Status,LinkSpeed,MacAddress | Format-Table -AutoSize
'Configuración IP:'
Get-NetIPConfiguration | Format-List InterfaceAlias,IPv4Address,IPv4DefaultGateway,DNSServer
'Estado Wi-Fi:'
try { netsh wlan show interfaces } catch { $_.Exception.Message }
'Prueba DNS (microsoft.com):'
try { Resolve-DnsName microsoft.com -ErrorAction Stop | Select-Object -First 3 Name,Type,IPAddress | Format-Table -AutoSize } catch { 'Fallo DNS: ' + $_.Exception.Message }
'Conectividad IP (1.1.1.1):'
Test-Connection 1.1.1.1 -Count 2 -ErrorAction SilentlyContinue | Select-Object Address,ResponseTime,Status | Format-Table -AutoSize
", 90000);

        await Section("12. DIAGNÓSTICO PROFUNDO DE COMPONENTES WINDOWS (DISM SCANHEALTH)", @"
'DISM ScanHealth inspecciona el almacén de componentes. No ejecuta RestoreHealth ni realiza reparaciones.'
DISM.exe /Online /Cleanup-Image /ScanHealth
", 900000);

        await Section("13. VERIFICACIÓN COMPLETA DE ARCHIVOS PROTEGIDOS (SFC VERIFYONLY)", @"
' SFC VerifyOnly comprueba archivos protegidos sin intentar repararlos.'
sfc.exe /verifyonly
", 900000);

        await Section("14. ERRORES RECIENTES DE WINDOWS (ÚLTIMOS 7 DÍAS)", @"
$since = (Get-Date).AddDays(-7)
try {
  Get-WinEvent -FilterHashtable @{LogName='System'; Level=1,2; StartTime=$since} -MaxEvents 40 -ErrorAction Stop |
    Select-Object TimeCreated,ProviderName,Id,LevelDisplayName,@{N='Mensaje';E={($_.Message -replace '\\s+',' ').Substring(0,[Math]::Min(260,($_.Message -replace '\\s+',' ').Length))}} |
    Format-List
} catch { 'No se encontraron eventos críticos/errores o no se pudo leer el registro: ' + $_.Exception.Message }
", 120000);

        await Section("15. ACTUALIZACIONES INSTALADAS RECIENTEMENTE", @"
Get-HotFix -ErrorAction SilentlyContinue | Sort-Object InstalledOn -Descending | Select-Object -First 15 HotFixID,Description,InstalledOn,InstalledBy | Format-Table -AutoSize
", 120000);

        Append("\r\nFIN DEL DIAGNÓSTICO.\r\n");
        Append("El análisis incluye comprobaciones profundas que pueden tardar varios minutos. DISM ScanHealth y SFC VerifyOnly verifican el sistema sin ejecutar reparaciones.\r\n");
        Append("Importante: un resultado sin errores en estas consultas no garantiza ausencia total de malware. Para un análisis antimalware profundo, ejecutá un examen completo o sin conexión desde Seguridad de Windows.\r\n");
        status.Text = "Diagnóstico finalizado. Podés exportar el informe.";
        exportButton.Enabled = true;
        runButton.Enabled = true;
    }

    private void ExportReport()
    {
        using var dialog = new SaveFileDialog {
            Title = "Guardar informe de diagnóstico",
            Filter = "Informe de texto (*.txt)|*.txt",
            FileName = "DiagnosticoPC_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try {
            File.WriteAllText(dialog.FileName, output.Text, new UTF8Encoding(true));
            status.Text = "Informe guardado correctamente.";
        } catch (Exception ex) {
            MessageBox.Show(this, "No se pudo guardar el informe: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
