using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace DiagnosticoPC;

public enum FindingLevel
{
    Ok,
    Info,
    Warning,
    Critical,
    NotVerified
}

public sealed record DiagnosticEntry(
    string Id,
    string Area,
    string Title,
    FindingLevel Level,
    string Details,
    TimeSpan Duration,
    int? ExitCode,
    bool TimedOut);

public sealed class DiagnosticReport
{
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset FinishedAt { get; set; }
    public bool RanElevated { get; init; }
    public bool Cancelled { get; set; }
    public List<DiagnosticEntry> Entries { get; } = new();
    public TimeSpan Duration => FinishedAt == default ? DateTimeOffset.Now - StartedAt : FinishedAt - StartedAt;
}

public sealed record DiagnosticProgress(int Completed, int Total, string CurrentTitle);

internal sealed record DiagnosticCheck(
    string Id,
    string Area,
    string Title,
    string Script,
    TimeSpan Timeout,
    bool RequiresAdmin = false);

internal sealed record CommandResult(string Output, int? ExitCode, bool TimedOut, bool Cancelled);

public sealed class DiagnosticEngine
{
    private readonly string powershellPath = Path.Combine(
        Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    private static readonly DiagnosticCheck[] Checks =
    {
        new("system", "Hardware", "Sistema, BIOS, placa madre y procesador", """
$ErrorActionPreference = 'Continue'
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$cpu = Get-CimInstance Win32_Processor
$board = Get-CimInstance Win32_BaseBoard
$bios = Get-CimInstance Win32_BIOS
$product = Get-CimInstance Win32_ComputerSystemProduct
'=== WINDOWS ==='
"Equipo: $env:COMPUTERNAME"
"Fabricante: $($cs.Manufacturer)"
"Modelo: $($cs.Model)"
"Usuario: $([Security.Principal.WindowsIdentity]::GetCurrent().Name)"
"Edición: $($os.Caption)"
"Versión: $($os.Version)"
"Compilación: $($os.BuildNumber)"
"Arquitectura: $($os.OSArchitecture)"
"Último inicio: $($os.LastBootUpTime)"
"Instalación: $($os.InstallDate)"
"RAM instalada (GB): $([math]::Round($cs.TotalPhysicalMemory / 1GB, 2))"
"RAM disponible (GB): $([math]::Round($os.FreePhysicalMemory / 1MB, 2))"
"RAM usada aproximada (%): $([math]::Round((1 - $os.FreePhysicalMemory / $os.TotalVisibleMemorySize) * 100, 1))"
'=== CPU ==='
$cpu | Select-Object Name,Manufacturer,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,CurrentClockSpeed,LoadPercentage | Format-List | Out-String -Width 200
'=== PLACA MADRE ==='
$board | Select-Object Manufacturer,Product,Version,SerialNumber | Format-List | Out-String -Width 200
'=== BIOS / UEFI ==='
$bios | Select-Object Manufacturer,SMBIOSBIOSVersion,ReleaseDate | Format-List | Out-String -Width 200
'=== IDENTIFICACIÓN DEL EQUIPO ==='
$product | Select-Object Vendor,Name,Version,UUID | Format-List | Out-String -Width 200
""", TimeSpan.FromSeconds(60)),

        new("gpu", "Hardware", "Tarjetas gráficas y pantallas", """
$ErrorActionPreference = 'Continue'
'=== GPU ==='
Get-CimInstance Win32_VideoController | Select-Object Name,AdapterCompatibility,DriverVersion,DriverDate,AdapterRAM,VideoModeDescription,Status,PNPDeviceID | Format-List | Out-String -Width 220
'=== MONITORES ==='
Get-CimInstance Win32_DesktopMonitor | Select-Object Name,MonitorManufacturer,ScreenWidth,ScreenHeight,Status | Format-Table -AutoSize | Out-String -Width 180
'Nota: la memoria de vídeo informada por WMI puede estar limitada por el controlador. Las temperaturas y frecuencias en tiempo real no están disponibles en todos los equipos.'
""", TimeSpan.FromSeconds(60)),

        new("performance", "Rendimiento", "Uso de CPU, memoria y procesos", """
$ErrorActionPreference = 'Continue'
$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average
$used = 1 - ($os.FreePhysicalMemory / $os.TotalVisibleMemorySize)
"Uso de CPU informado por Windows (%): $([math]::Round($cpu.Average, 1))"
"RAM instalada (GB): $([math]::Round($os.TotalVisibleMemorySize / 1MB, 2))"
"RAM disponible (GB): $([math]::Round($os.FreePhysicalMemory / 1MB, 2))"
"RAM utilizada aproximada (%): $([math]::Round($used * 100, 1))"
if ($used -ge 0.95) { '[AVISO] La memoria RAM está muy ocupada en esta captura.' }
elseif ($used -ge 0.90) { '[AVISO] La memoria RAM presenta una ocupación elevada.' }
if ($cpu.Average -ge 95) { '[AVISO] La CPU presenta una carga muy alta en esta captura.' }
'=== PROCESOS CON MAYOR CONSUMO DE MEMORIA ==='
Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 25 ProcessName,Id,@{N='RAM_MB';E={[math]::Round($_.WorkingSet64 / 1MB, 1)}},@{N='CPU_acumulada_seg';E={if ($null -ne $_.CPU) {[math]::Round($_.CPU, 1)} else {0}}} | Format-Table -AutoSize | Out-String -Width 220
'=== PROCESOS CON MAYOR TIEMPO DE CPU ACUMULADO ==='
Get-Process | Sort-Object CPU -Descending | Select-Object -First 12 ProcessName,Id,@{N='CPU_acumulada_seg';E={if ($null -ne $_.CPU) {[math]::Round($_.CPU, 1)} else {0}}},@{N='RAM_MB';E={[math]::Round($_.WorkingSet64 / 1MB, 1)}} | Format-Table -AutoSize | Out-String -Width 180
'Nota: CPU acumulada representa tiempo consumido desde que inició cada proceso; no es el porcentaje de uso instantáneo.'
""", TimeSpan.FromSeconds(90)),

        new("storage", "Almacenamiento", "Unidades, espacio libre y estado físico", """
$ErrorActionPreference = 'Continue'
'=== VOLÚMENES ==='
$vols = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3'
foreach ($v in $vols) {
  $freePct = if ($v.Size -gt 0) { [math]::Round(($v.FreeSpace / $v.Size) * 100, 1) } else { 0 }
  $freeGB = [math]::Round($v.FreeSpace / 1GB, 1)
  "$($v.DeviceID) | $($v.VolumeName) | $($v.FileSystem) | Total: $([math]::Round($v.Size / 1GB, 1)) GB | Libre: $freeGB GB ($freePct %)"
  if ($freePct -lt 5 -or $freeGB -lt 5) { "[CRITICO] $($v.DeviceID) tiene muy poco espacio disponible." }
  elseif ($freePct -lt 15 -or $freeGB -lt 20) { "[AVISO] $($v.DeviceID) tiene poco espacio disponible." }
}
'=== DISCOS FÍSICOS ==='
try {
  $disks = Get-PhysicalDisk -ErrorAction Stop
  $disks | Select-Object FriendlyName,MediaType,BusType,HealthStatus,OperationalStatus,@{N='TamañoGB';E={[math]::Round($_.Size / 1GB, 1)}} | Format-Table -AutoSize | Out-String -Width 200
  foreach ($d in $disks) {
    "Fiabilidad de: $($d.FriendlyName)"
    try {
      $rel = Get-StorageReliabilityCounter -PhysicalDisk $d -ErrorAction Stop
      if ($rel) { $rel | Select-Object Temperature,TemperatureMax,Wear,PowerOnHours,ReadErrorsTotal,ReadErrorsUncorrected,WriteErrorsTotal,WriteErrorsUncorrected | Format-List | Out-String -Width 180 }
      else { 'El dispositivo no entregó contadores de fiabilidad.' }
    } catch { "Contadores no disponibles: $($_.Exception.Message)" }
  }
} catch { "Inventario físico no disponible mediante Storage: $($_.Exception.Message)" }
'=== SMART / PREDICCIÓN DE FALLO (SI EL CONTROLADOR LO EXPONE) ==='
try {
  Get-CimInstance -Namespace root/wmi -ClassName MSStorageDriver_FailurePredictStatus -ErrorAction Stop | Select-Object InstanceName,PredictFailure,Reason | Format-Table -AutoSize | Out-String -Width 200
} catch { 'El controlador no expone predicción SMART por esta interfaz. Esto no demuestra que el disco esté sano ni averiado.' }
'=== IDENTIFICACIÓN DE DISCOS ==='
Get-CimInstance Win32_DiskDrive | Select-Object Model,InterfaceType,MediaType,Status,Size,Partitions,FirmwareRevision | Format-List | Out-String -Width 180
""", TimeSpan.FromSeconds(120)),

        new("devices", "Dispositivos", "Dispositivos y controladores con incidencias", """
$ErrorActionPreference = 'Continue'
$bad = @(Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.ConfigManagerErrorCode -and $_.ConfigManagerErrorCode -ne 0 })
if ($bad.Count -eq 0) {
  '[OK] No se encontraron dispositivos con códigos de error en el Administrador de dispositivos.'
} else {
  "Dispositivos con incidencias: $($bad.Count)"
  foreach ($d in $bad) {
    $code = [int]$d.ConfigManagerErrorCode
    $meaning = switch ($code) {
      22 { 'Dispositivo deshabilitado. Puede ser intencional.' }
      28 { 'No hay un controlador instalado para este dispositivo.' }
      51 { 'El dispositivo está esperando que otro dispositivo se inicie.' }
      10 { 'El dispositivo no pudo iniciarse.' }
      31 { 'Windows no pudo cargar el controlador necesario.' }
      43 { 'Windows detuvo el dispositivo porque informó un problema.' }
      19 { 'La configuración del registro del dispositivo está incompleta o dañada.' }
      39 { 'Windows no pudo cargar el controlador.' }
      default { 'Código de estado de dispositivo que requiere revisión.' }
    }
    $level = if ($code -eq 22) { 'INFO' } elseif ($code -eq 51) { 'INFO' } else { 'AVISO' }
    "[$level] $($d.Name) | Código $code | $meaning | Fabricante: $($d.Manufacturer)"
  }
}
'Los códigos 22 y 51 no se interpretan automáticamente como fallas de hardware. No se modificó ni habilitó ningún dispositivo.'
""", TimeSpan.FromSeconds(90)),

        new("defender", "Seguridad", "Microsoft Defender y detecciones", """
$ErrorActionPreference = 'Continue'
try {
  $s = Get-MpComputerStatus -ErrorAction Stop
  '=== ESTADO DE PROTECCIÓN ==='
  "Servicio antimalware activo: $($s.AMServiceEnabled)"
  "Antivirus habilitado: $($s.AntivirusEnabled)"
  "Protección en tiempo real: $($s.RealTimeProtectionEnabled)"
  "Supervisión de comportamiento: $($s.BehaviorMonitorEnabled)"
  "Análisis de descargas y archivos: $($s.IoavProtectionEnabled)"
  "Protección contra acceso no autorizado: $($s.IsTamperProtected)"
  "Versión de firmas: $($s.AntivirusSignatureVersion)"
  "Última actualización de firmas: $($s.AntivirusSignatureLastUpdated)"
  "Antigüedad del análisis rápido (días): $($s.QuickScanAge)"
  "Antigüedad del análisis completo (días): $($s.FullScanAge)"
  if (-not $s.AMServiceEnabled -or -not $s.AntivirusEnabled -or -not $s.RealTimeProtectionEnabled) { '[CRITICO] Una o más protecciones principales de Defender están desactivadas.' }
  if ($s.AntivirusSignatureLastUpdated -and ((Get-Date) - $s.AntivirusSignatureLastUpdated).TotalDays -gt 7) { '[AVISO] Las firmas de Defender tienen más de 7 días.' }
  if ($s.FullScanAge -ge 30) { '[AVISO] No se registra un análisis completo de Defender en los últimos 30 días.' }
} catch { "No se pudo consultar Defender: $($_.Exception.Message)" }
'=== DETECCIONES REGISTRADAS ==='
try {
  $d = @(Get-MpThreatDetection -ErrorAction Stop | Sort-Object InitialDetectionTime -Descending)
  "Registros de detecciones: $($d.Count)"
  if ($d.Count -gt 0) { $d | Select-Object -First 20 ThreatID,ThreatStatusID,InitialDetectionTime,ActionSuccess,Resources | Format-List | Out-String -Width 200 }
  else { 'No hay detecciones registradas en el historial que expone Defender.' }
} catch { "Historial de detecciones no disponible: $($_.Exception.Message)" }
'Esta consulta no inicia un análisis antivirus ni garantiza ausencia total de malware.'
""", TimeSpan.FromSeconds(120)),

        new("firewall", "Seguridad", "Firewall de Windows", """
$ErrorActionPreference = 'Continue'
try {
  $profiles = Get-NetFirewallProfile -ErrorAction Stop
  $profiles | Select-Object Name,Enabled,DefaultInboundAction,DefaultOutboundAction,NotifyOnListen | Format-Table -AutoSize | Out-String -Width 180
  if (@($profiles | Where-Object { -not $_.Enabled }).Count -gt 0) { '[CRITICO] Hay uno o más perfiles del Firewall desactivados.' }
  else { '[OK] Los perfiles consultados del Firewall están habilitados.' }
} catch { "No se pudo consultar Firewall: $($_.Exception.Message)" }
""", TimeSpan.FromSeconds(90)),

        new("security-posture", "Seguridad", "TPM, Secure Boot, BitLocker y protecciones", """
$ErrorActionPreference = 'Continue'
'=== SECURE BOOT ==='
try {
  $sb = Confirm-SecureBootUEFI -ErrorAction Stop
  "Secure Boot habilitado: $sb"
  if (-not $sb) { '[AVISO] Secure Boot está desactivado.' }
} catch { "Secure Boot no verificable en esta sesión: $($_.Exception.Message)" }
'=== TPM ==='
try {
  $t = Get-Tpm -ErrorAction Stop
  $t | Select-Object TpmPresent,TpmReady,TpmEnabled,TpmActivated,ManufacturerIdTxt,ManufacturerVersion | Format-List | Out-String -Width 180
  if (-not $t.TpmPresent -or -not $t.TpmReady) { '[AVISO] TPM no presente o no preparado, revisar compatibilidad y configuración.' }
} catch { "No se pudo consultar TPM: $($_.Exception.Message)" }
'=== BITLOCKER (SIN CLAVES DE RECUPERACIÓN) ==='
try {
  Get-BitLockerVolume -ErrorAction Stop | Select-Object MountPoint,VolumeType,VolumeStatus,ProtectionStatus,EncryptionPercentage,EncryptionMethod | Format-Table -AutoSize | Out-String -Width 200
} catch { "Estado BitLocker no disponible: $($_.Exception.Message)" }
'=== UAC Y SMARTSCREEN ==='
try {
  $u = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -ErrorAction Stop
  "UAC EnableLUA: $($u.EnableLUA)"
  if ($u.EnableLUA -eq 0) { '[AVISO] UAC aparece desactivado.' }
} catch { "Estado UAC no disponible: $($_.Exception.Message)" }
try {
  $ss = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer' -ErrorAction Stop
  "SmartScreen: $($ss.SmartScreenEnabled)"
} catch { 'No se encontró el valor de configuración de SmartScreen en esa ubicación.' }
'=== SEGURIDAD BASADA EN VIRTUALIZACIÓN ==='
try {
  Get-CimInstance -Namespace root\Microsoft\Windows\DeviceGuard -ClassName Win32_DeviceGuard -ErrorAction Stop | Select-Object VirtualizationBasedSecurityStatus,SecurityServicesConfigured,SecurityServicesRunning | Format-List | Out-String -Width 180
} catch { "Estado de seguridad basada en virtualización no disponible: $($_.Exception.Message)" }
'La ausencia de BitLocker, Secure Boot o VBS se informa como postura de seguridad; no se modifica ninguna configuración.'
""", TimeSpan.FromSeconds(120)),

        new("network", "Red", "Ethernet, Wi-Fi, DNS, conectividad y puertos", """
$ErrorActionPreference = 'Continue'
'=== ADAPTADORES ==='
try { Get-NetAdapter -IncludeHidden | Select-Object Name,InterfaceDescription,Status,LinkSpeed,MacAddress,Virtual | Format-Table -AutoSize | Out-String -Width 200 } catch { "Adaptadores no disponibles: $($_.Exception.Message)" }
'=== CONFIGURACIÓN IP, PUERTA DE ENLACE Y DNS ==='
try {
  Get-NetIPConfiguration | ForEach-Object {
    "Interfaz: $($_.InterfaceAlias)"
    "IPv4: $(($_.IPv4Address | ForEach-Object IPAddress) -join ', ')"
    "Puerta de enlace: $(($_.IPv4DefaultGateway | ForEach-Object NextHop) -join ', ')"
    "DNS: $(($_.DNSServer.ServerAddresses) -join ', ')"
  }
} catch { "Configuración IP no disponible: $($_.Exception.Message)" }
'=== SERVICIO Y ESTADO WI-FI ==='
try {
  $w = Get-Service WlanSvc -ErrorAction Stop
  "Servicio WLAN: $($w.Status)"
  if ($w.Status -eq 'Running') { netsh wlan show interfaces }
  else { 'El servicio WLAN está detenido. No se inició ni se modificó.' }
} catch { "No se pudo consultar WLAN: $($_.Exception.Message)" }
'=== RESOLUCIÓN DNS ==='
try {
  Resolve-DnsName microsoft.com -ErrorAction Stop | Select-Object -First 6 Name,Type,IPAddress | Format-Table -AutoSize | Out-String -Width 180
  '[OK] La resolución DNS respondió.'
} catch { "[AVISO] Falló la resolución DNS: $($_.Exception.Message)" }
'=== CONECTIVIDAD ICMP ==='
try {
  $p = Test-Connection 1.1.1.1 -Count 2 -ErrorAction Stop
  $p | Select-Object Address,ResponseTime,Status | Format-Table -AutoSize | Out-String -Width 160
  "Respuestas ICMP: $($p.Count)"
} catch { 'No hubo respuesta ICMP. Algunos routers o proveedores bloquean ping; esto por sí solo no demuestra falta de Internet.' }
'=== CONECTIVIDAD HTTPS ==='
try {
  $tcp = Test-NetConnection microsoft.com -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue
  "Conexión TCP 443 a microsoft.com: $tcp"
  if (-not $tcp) { '[AVISO] No se pudo establecer la conexión TCP de prueba al puerto 443.' }
} catch { "Prueba TCP no disponible: $($_.Exception.Message)" }
'=== PROXY DEL USUARIO ==='
try {
  $proxy = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -ErrorAction Stop
  "Proxy habilitado: $($proxy.ProxyEnable)"
  "Servidor proxy: $($proxy.ProxyServer)"
} catch { "Configuración proxy no disponible: $($_.Exception.Message)" }
'=== PUERTOS LOCALES EN ESCUCHA (MUESTRA DE HASTA 40) ==='
try {
  Get-NetTCPConnection -State Listen -ErrorAction Stop | Sort-Object LocalPort | Select-Object -First 40 LocalAddress,LocalPort,OwningProcess | Format-Table -AutoSize | Out-String -Width 180
} catch { "No se pudieron consultar puertos en escucha: $($_.Exception.Message)" }
'No se consultan contraseñas Wi-Fi ni se cambian adaptadores, DNS, proxy o firewall.'
""", TimeSpan.FromSeconds(150)),

        new("startup", "Rendimiento", "Aplicaciones configuradas para iniciar con Windows", """
$ErrorActionPreference = 'Continue'
try {
  $items = @(Get-CimInstance Win32_StartupCommand -ErrorAction Stop | Sort-Object Name)
  "Entradas de inicio detectadas: $($items.Count)"
  $items | Select-Object Name,Command,Location,User | Format-List | Out-String -Width 220
  'Las entradas se muestran para revisión. No se deshabilita ni elimina ninguna.'
} catch { "No se pudo consultar el inicio automático: $($_.Exception.Message)" }
""", TimeSpan.FromSeconds(90)),

        new("services", "Windows", "Servicios relevantes del sistema", """
$ErrorActionPreference = 'Continue'
$names = 'WinDefend','mpssvc','wuauserv','BITS','EventLog','Schedule','Dhcp','Dnscache','WlanSvc','CryptSvc','Winmgmt'
foreach ($n in $names) {
  try {
    $s = Get-Service -Name $n -ErrorAction Stop
    "$($s.Name) | $($s.DisplayName) | Estado: $($s.Status) | Inicio: $($s.StartType)"
  } catch { "$n | No disponible en esta instalación." }
}
'Nota: algunos servicios se inician bajo demanda; estar detenidos no significa necesariamente que exista una falla.'
""", TimeSpan.FromSeconds(90)),

        new("events", "Windows", "Errores críticos y advertencias recientes (7 días)", """
$ErrorActionPreference = 'Continue'
$since = (Get-Date).AddDays(-7)
'=== EVENTOS IMPORTANTES DEL SISTEMA Y APLICACIONES ==='
try {
  $events = @(Get-WinEvent -FilterHashtable @{LogName='System','Application'; Level=1,2; StartTime=$since} -MaxEvents 250 -ErrorAction SilentlyContinue)
  "Eventos críticos/errores consultados: $($events.Count)"
  if ($events.Count -eq 0) { '[OK] No se encontraron eventos críticos o errores en la muestra consultada.' }
  else {
    $groups = $events | Group-Object ProviderName,Id | Sort-Object Count -Descending | Select-Object -First 35
    foreach ($g in $groups) {
      $e = $g.Group | Sort-Object TimeCreated -Descending | Select-Object -First 1
      $msg = ($e.Message -replace '\s+',' ').Trim()
      if ($msg.Length -gt 300) { $msg = $msg.Substring(0,300) + '…' }
      $important = ($e.ProviderName -match 'WHEA|Disk|storahci|stornvme|Ntfs|Kernel-Power|BugCheck') -or ($e.Id -in 7,41,51,55,1001,129,153)
      $tag = if ($important) { 'AVISO' } elseif ($e.Id -eq 10010 -or $e.Id -eq 2042) { 'INFO' } else { 'INFO' }
      "[$tag] $($e.ProviderName) | Evento $($e.Id) | Repeticiones en la muestra: $($g.Count) | Último: $($e.TimeCreated) | $msg"
    }
    if (@($events | Where-Object { $_.ProviderName -match 'WHEA|Disk|storahci|stornvme|Ntfs|Kernel-Power|BugCheck' -or $_.Id -in 7,41,51,55,1001,129,153 }).Count -gt 0) { '[AVISO] Se encontraron eventos relacionados con estabilidad, hardware o almacenamiento que merecen revisión.' }
  }
} catch { "No se pudieron consultar los registros: $($_.Exception.Message)" }
'Los eventos se agrupan para reducir duplicados. Los eventos DCOM 10010 no se consideran automáticamente fallas críticas.'
""", TimeSpan.FromSeconds(150)),

        new("updates", "Windows", "Actualizaciones instaladas y reinicio pendiente", """
$ErrorActionPreference = 'Continue'
'=== ACTUALIZACIONES INSTALADAS (MÁS RECIENTES) ==='
try { Get-HotFix -ErrorAction Stop | Sort-Object InstalledOn -Descending | Select-Object -First 20 HotFixID,Description,InstalledOn,InstalledBy | Format-Table -AutoSize | Out-String -Width 200 } catch { "Historial HotFix no disponible: $($_.Exception.Message)" }
'=== REINICIO PENDIENTE ==='
$pending = $false
$paths = @(
 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending',
 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
)
foreach ($p in $paths) { if (Test-Path $p) { $pending = $true; "Marcador detectado: $p" } }
try {
  $sm = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue
  if ($sm.PendingFileRenameOperations) { $pending = $true; 'Hay operaciones de archivos pendientes de reinicio.' }
} catch {}
if ($pending) { '[AVISO] Windows informa que puede haber un reinicio pendiente.' }
else { '[OK] No se encontraron los marcadores habituales de reinicio pendiente.' }
""", TimeSpan.FromSeconds(120)),

        new("dism", "Windows", "Integridad del almacén de componentes (DISM ScanHealth)", """
$ErrorActionPreference = 'Continue'
'DISM /ScanHealth analiza el almacén de componentes. No se ejecuta RestoreHealth.'
& dism.exe /Online /Cleanup-Image /ScanHealth 2>&1 | Out-String -Width 200
"DISM_EXIT_CODE=$LASTEXITCODE"
""", TimeSpan.FromMinutes(20), true),

        new("sfc", "Windows", "Integridad de archivos protegidos (SFC VerifyOnly)", """
$ErrorActionPreference = 'Continue'
'SFC /verifyonly verifica archivos protegidos y no intenta repararlos.'
& sfc.exe /verifyonly 2>&1 | Out-String -Width 200
"SFC_EXIT_CODE=$LASTEXITCODE"
""", TimeSpan.FromMinutes(20), true),

        new("chkdsk", "Almacenamiento", "Verificación en línea del sistema de archivos (CHKDSK)", """
$ErrorActionPreference = 'Continue'
'CHKDSK /scan solicita una exploración en línea de NTFS. No se utilizan /f ni /r.'
& chkdsk.exe $env:SystemDrive /scan 2>&1 | Out-String -Width 200
"CHKDSK_EXIT_CODE=$LASTEXITCODE"
""", TimeSpan.FromMinutes(15), true)
    };

    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public async Task<DiagnosticReport> RunAsync(
        IProgress<DiagnosticProgress>? progress,
        IProgress<DiagnosticEntry>? entryProgress,
        CancellationToken cancellationToken)
    {
        bool elevated = IsAdministrator();
        var report = new DiagnosticReport { RanElevated = elevated };
        int completed = 0;

        foreach (var check in Checks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiagnosticProgress(completed, Checks.Length, check.Title));
            var timer = Stopwatch.StartNew();

            if (check.RequiresAdmin && !elevated)
            {
                timer.Stop();
                var skipped = new DiagnosticEntry(
                    check.Id, check.Area, check.Title, FindingLevel.NotVerified,
                    "No se ejecutó porque esta sesión no tiene permisos de administrador. " +
                    "No es un error del sistema: la prueba requiere elevación voluntaria para poder verificarlo.",
                    timer.Elapsed, null, false);
                report.Entries.Add(skipped);
                entryProgress?.Report(skipped);
            }
            else
            {
                CommandResult command;
                try
                {
                    command = await RunPowerShellAsync(check.Script, check.Timeout, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    report.Cancelled = true;
                    break;
                }

                timer.Stop();
                var details = command.Output;
                if (command.TimedOut)
                    details += Environment.NewLine + "La prueba superó el tiempo máximo permitido y fue detenida.";
                if (command.Cancelled)
                {
                    report.Cancelled = true;
                    break;
                }

                var level = Evaluate(check.Id, details, command);
                var entry = new DiagnosticEntry(
                    check.Id, check.Area, check.Title, level, details,
                    timer.Elapsed, command.ExitCode, command.TimedOut);
                report.Entries.Add(entry);
                entryProgress?.Report(entry);
            }

            completed++;
            progress?.Report(new DiagnosticProgress(completed, Checks.Length, check.Title));
        }

        report.Cancelled |= cancellationToken.IsCancellationRequested;
        report.FinishedAt = DateTimeOffset.Now;
        return report;
    }

    private async Task<CommandResult> RunPowerShellAsync(
        string script,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        string prepared =
            "$ProgressPreference = 'SilentlyContinue'\r\n" +
            "$ErrorActionPreference = 'Continue'\r\n" +
            "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)\r\n" +
            "$OutputEncoding = New-Object System.Text.UTF8Encoding($false)\r\n" +
            script;

        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(prepared));
        var start = new ProcessStartInfo
        {
            FileName = powershellPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(encoded);

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                string partialOut = await SafeRead(stdoutTask);
                string partialErr = await SafeRead(stderrTask);
                bool userCancelled = cancellationToken.IsCancellationRequested;
                return new CommandResult(
                    JoinOutput(partialOut, partialErr),
                    process.HasExited ? process.ExitCode : null,
                    !userCancelled, userCancelled);
            }

            string output = await stdoutTask;
            string error = await stderrTask;
            return new CommandResult(JoinOutput(output, error), process.ExitCode, false, false);
        }
        catch (Exception ex)
        {
            return new CommandResult("No se pudo ejecutar PowerShell: " + ex.Message, null, false, false);
        }
    }

    private static async Task<string> SafeRead(Task<string> task)
    {
        try { return await task; }
        catch { return string.Empty; }
    }

    private static string JoinOutput(string output, string error)
    {
        var text = output ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(error) &&
            !error.Contains("#< CLIXML", StringComparison.OrdinalIgnoreCase))
            text += Environment.NewLine + error;

        int clixml = text.IndexOf("#< CLIXML", StringComparison.OrdinalIgnoreCase);
        if (clixml >= 0) text = text[..clixml];

        return string.IsNullOrWhiteSpace(text)
            ? "La consulta terminó sin texto de salida. Consulte el código de salida y los permisos requeridos."
            : text.Trim();
    }

    private static FindingLevel Evaluate(string id, string output, CommandResult command)
    {
        if (command.TimedOut) return FindingLevel.NotVerified;
        if (output.Contains("[CRITICO]", StringComparison.OrdinalIgnoreCase)) return FindingLevel.Critical;
        if (output.Contains("[AVISO]", StringComparison.OrdinalIgnoreCase)) return FindingLevel.Warning;
        if (output.Contains("no se pudo ejecutar PowerShell", StringComparison.OrdinalIgnoreCase)) return FindingLevel.NotVerified;

        if (id == "dism")
        {
            if (ContainsAny(output, "No se detectó daño en el almacén de componentes", "No component store corruption detected"))
                return FindingLevel.Ok;
            if (ContainsAny(output, "El almacén de componentes se puede reparar", "The component store is repairable"))
                return FindingLevel.Warning;
            if (ContainsAny(output, "El almacén de componentes no se puede reparar", "The component store cannot be repaired"))
                return FindingLevel.Critical;
            return command.ExitCode == 0 ? FindingLevel.Info : FindingLevel.NotVerified;
        }

        if (id == "sfc")
        {
            if (ContainsAny(output, "No se encontraron infracciones de integridad", "did not find any integrity violations"))
                return FindingLevel.Ok;
            if (ContainsAny(output, "encontró archivos dañados", "found integrity violations", "cannot perform the requested operation"))
                return FindingLevel.Warning;
            return command.ExitCode == 0 ? FindingLevel.Info : FindingLevel.NotVerified;
        }

        if (id == "chkdsk")
        {
            if (ContainsAny(output, "Windows ha examinado el sistema de archivos y no encontró problemas", "Windows has scanned the file system and found no problems"))
                return FindingLevel.Ok;
            if (ContainsAny(output, "found problems", "se encontraron problemas"))
                return FindingLevel.Warning;
            return command.ExitCode == 0 ? FindingLevel.Info : FindingLevel.NotVerified;
        }

        if (id == "firewall" && output.Contains("[OK]")) return FindingLevel.Ok;
        if (id == "devices" && output.Contains("[OK]")) return FindingLevel.Ok;
        if (id == "storage" && output.Contains("[OK]")) return FindingLevel.Ok;
        if (id == "network" && output.Contains("[OK]") && output.Contains("Conexión TCP 443 a microsoft.com: True"))
            return FindingLevel.Ok;
        if (id == "updates" && output.Contains("[OK]")) return FindingLevel.Ok;

        if (string.IsNullOrWhiteSpace(output) ||
            output.Contains("La consulta terminó sin texto", StringComparison.OrdinalIgnoreCase))
            return FindingLevel.NotVerified;

        if (command.ExitCode is not null && command.ExitCode != 0 &&
            id is not "system" and not "gpu" and not "performance" and not "storage")
            return FindingLevel.NotVerified;

        return FindingLevel.Info;
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
