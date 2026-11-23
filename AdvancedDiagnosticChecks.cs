namespace DiagnosticoPC;

internal static class AdvancedDiagnosticChecks
{
    internal static readonly DiagnosticCheck[] All =
    {
        new("performance-sample", "Rendimiento", "Muestra de CPU y consumo por proceso (3 segundos)", """
$ErrorActionPreference = 'Continue'
$logical = [math]::Max(1, [Environment]::ProcessorCount)
$before = @{}
try {
  Get-Process -ErrorAction SilentlyContinue | ForEach-Object {
    try { if ($null -ne $_.CPU) { $before[[int]$_.Id] = [double]$_.CPU } } catch {}
  }
  Start-Sleep -Seconds 3
  $after = @(Get-Process -ErrorAction SilentlyContinue)
  $rows = foreach ($p in $after) {
    try {
      if ($null -eq $p.CPU) { continue }
      $old = if ($before.ContainsKey([int]$p.Id)) { $before[[int]$p.Id] } else { [double]$p.CPU }
      $delta = [math]::Max(0, [double]$p.CPU - $old)
      [pscustomobject]@{Process=$p.ProcessName;PID=$p.Id;CPUPercent=[math]::Round(($delta / 3 / $logical) * 100,1);RAMMB=[math]::Round($p.WorkingSet64 / 1MB,1)}
    } catch {}
  }
  $total = Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'" -ErrorAction SilentlyContinue
  if ($total) {
    $cpu = [math]::Round([double]$total.PercentProcessorTime,1)
    "CPU total informada durante la muestra: $cpu %"
    if ($cpu -ge 95) { '[AVISO] La CPU permaneció con carga muy alta durante la muestra.' }
  } else { 'Porcentaje total de CPU no disponible mediante el contador de Windows.' }
  $os = Get-CimInstance Win32_OperatingSystem
  $ram = [math]::Round((1 - ($os.FreePhysicalMemory / $os.TotalVisibleMemorySize)) * 100,1)
  "RAM utilizada durante la muestra: $ram %"
  "RAM disponible: $([math]::Round($os.FreePhysicalMemory / 1MB,2)) GB"
  if ($ram -ge 95) { '[AVISO] La memoria física está casi agotada durante la muestra.' }
  '=== PROCESOS CON MAYOR CPU MEDIDA EN LA MUESTRA ==='
  $rows | Sort-Object CPUPercent -Descending | Select-Object -First 15 | Format-Table -AutoSize | Out-String -Width 180
  '=== PROCESOS CON MAYOR USO DE RAM ==='
  $after | Sort-Object WorkingSet64 -Descending | Select-Object -First 15 ProcessName,Id,@{N='RAM_MB';E={[math]::Round($_.WorkingSet64 / 1MB,1)}} | Format-Table -AutoSize | Out-String -Width 180
  'El porcentaje por proceso se normaliza sobre los procesadores lógicos. Es una muestra breve, no una medición permanente.'
} catch { "[NO VERIFICADO] No se pudo medir el rendimiento: $($_.Exception.Message)" }
""", TimeSpan.FromSeconds(45)),

        new("driver-audit", "Dispositivos", "Auditoría de firma digital de controladores", """
$ErrorActionPreference = 'Continue'
try {
  $all = @(Get-CimInstance Win32_PnPSignedDriver -ErrorAction Stop)
  if ($all.Count -eq 0) { '[NO VERIFICADO] Windows no devolvió inventario de controladores PnP.' }
  else {
    $unsigned = @($all | Where-Object { $_.IsSigned -eq $false })
    $unknown = @($all | Where-Object { $null -eq $_.IsSigned })
    "Controladores PnP inventariados: $($all.Count)"
    "Controladores sin firma digital confirmada: $($unsigned.Count)"
    "Registros cuya firma no pudo determinarse: $($unknown.Count)"
    if ($unsigned.Count -eq 0 -and $unknown.Count -eq 0) { '[OK] Todos los controladores PnP consultados figuran firmados.' }
    else {
      if ($unsigned.Count -gt 0) {
        '[INFO] Los controladores que Windows marca explícitamente como no firmados deben revisarse por fabricante y contexto; no implican automáticamente malware.'
        $unsigned | Select-Object -First 40 DeviceName,DeviceID,DriverProviderName,DriverVersion,IsSigned,InfName | Format-List | Out-String -Width 220
      }
      if ($unknown.Count -gt 0) {
        '[INFO] Hay registros sin un valor de firma devuelto por Windows. No se cuentan como controladores sin firma.'
        $unknown | Select-Object -First 15 DeviceName,DeviceID,DriverProviderName,DriverVersion,IsSigned,InfName | Format-List | Out-String -Width 220
      }
    }
  }
} catch { "[NO VERIFICADO] No se pudo auditar la firma de los controladores: $($_.Exception.Message)" }
'La consulta es de solo lectura. No instala, actualiza ni bloquea controladores.'
""", TimeSpan.FromSeconds(120)),

        new("startup-audit", "Rendimiento", "Firma digital y huella de programas de inicio", """
$ErrorActionPreference = 'Continue'
try {
  $items = @(Get-CimInstance Win32_StartupCommand -ErrorAction Stop | Sort-Object Name | Select-Object -First 30)
  "Entradas de inicio analizadas: $($items.Count)"
  foreach ($item in $items) {
    $raw = [Environment]::ExpandEnvironmentVariables([string]$item.Command)
    $exe = $null
    if ($raw -match '^\s*"([^"]+\.exe)"') { $exe = $matches[1] }
    elseif ($raw -match '^\s*(.+?\.exe)(?:\s|$)') { $exe = $matches[1].Trim('"') }
    "=== $($item.Name) ==="
    "Origen: $($item.Location)"
    if ($raw -match '(?i)-EncodedCommand|FromBase64String|IEX\s*\(') { '[AVISO] La línea contiene una instrucción codificada o dinámica. Revisá que la reconozcas.' }
    if ($exe -match '(?i)\\AppData\\Local\\Temp\\|\\Windows\\Temp\\') { '[AVISO] El ejecutable se inicia desde una carpeta temporal. Revisá su procedencia.' }
    if ($exe -and (Test-Path -LiteralPath $exe -PathType Leaf)) {
      try {
        $sig = Get-AuthenticodeSignature -LiteralPath $exe -ErrorAction Stop
        "Ejecutable: $exe"
        "Firma digital: $($sig.Status)"
        if ($sig.SignerCertificate) { "Firmante: $($sig.SignerCertificate.Subject)" }
        try { "SHA256: $((Get-FileHash -LiteralPath $exe -Algorithm SHA256 -ErrorAction Stop).Hash)" }
        catch { 'SHA256 no disponible para este archivo.' }
        if ($sig.Status -eq 'NotSigned') { '[INFO] No tiene firma Authenticode. Esto no demuestra que sea malicioso.' }
      } catch { "No se pudo verificar el ejecutable: $($_.Exception.Message)" }
    } elseif ($exe) {
      "Ejecutable referenciado: $exe"
      '[INFO] El archivo no se encontró en la ruta indicada; puede tratarse de una entrada antigua.'
    } else {
      "Comando: $raw"
      '[INFO] No se pudo extraer una ruta ejecutable simple de esta entrada.'
    }
  }
  'Los hashes se calculan localmente. No se envían a servicios externos.'
} catch { "[NO VERIFICADO] No se pudieron auditar los programas de inicio: $($_.Exception.Message)" }
""", TimeSpan.FromMinutes(5)),

        new("scheduled-task-audit", "Rendimiento", "Auditoría de tareas programadas de terceros", """
$ErrorActionPreference = 'Continue'
try {
  $tasks = @(Get-ScheduledTask -ErrorAction Stop | Where-Object {
    $_.State -ne 'Disabled' -and $_.TaskPath -notlike '\Microsoft\Windows\*'
  } | Sort-Object TaskPath,TaskName)
  "Tareas de terceros habilitadas o activas: $($tasks.Count)"
  $shown = 0
  foreach ($task in $tasks) {
    foreach ($action in @($task.Actions)) {
      if ($shown -ge 60) { break }
      $command = "$($action.Execute) $($action.Arguments)".Trim()
      if ([string]::IsNullOrWhiteSpace($command)) { continue }
      $shown++
      "$($task.TaskPath)$($task.TaskName) | Estado: $($task.State) | Acción: $command"
      if ($command -match '(?i)-EncodedCommand|FromBase64String|\\AppData\\Local\\Temp\\|\\Windows\\Temp\\') {
        "[AVISO] Revisar acción de tarea programada por comandos codificados o rutas temporales: $($task.TaskName)"
      }
    }
    if ($shown -ge 60) { break }
  }
  if ($tasks.Count -eq 0) { '[OK] No se encontraron tareas de terceros activas en la consulta.' }
  'Las tareas legítimas de software, controladores y actualizadores también pueden aparecer. No se deshabilitó ninguna.'
} catch { "[NO VERIFICADO] No se pudo consultar el Programador de tareas: $($_.Exception.Message)" }
""", TimeSpan.FromSeconds(150)),

        new("defender-exclusions", "Seguridad", "Exclusiones configuradas en Microsoft Defender", """
$ErrorActionPreference = 'Continue'
try {
  $p = Get-MpPreference -ErrorAction Stop
  $groups = @(
    @{Name='Rutas';Items=@($p.ExclusionPath)},
    @{Name='Procesos';Items=@($p.ExclusionProcess)},
    @{Name='Extensiones';Items=@($p.ExclusionExtension)},
    @{Name='Direcciones IP';Items=@($p.ExclusionIpAddress)}
  )
  $total = 0
  foreach ($g in $groups) {
    foreach ($item in $g.Items) {
      if ([string]::IsNullOrWhiteSpace([string]$item)) { continue }
      $total++
      "Exclusión $($g.Name): $item"
      if ($g.Name -eq 'Rutas' -and ([string]$item -match '(?i)^[A-Z]:\\?$|^[A-Z]:\\Windows(\\|$)|^[A-Z]:\\Users(\\|$)')) {
        "[AVISO] Exclusión de carpeta muy amplia: $item. Confirmá que sea intencional."
      } else { '[INFO] Exclusión configurada: revisá que sea necesaria y confiable.' }
    }
  }
  "Total de exclusiones explícitas consultadas: $total"
  if ($total -eq 0) { '[OK] No se encontraron exclusiones explícitas en la configuración consultada.' }
  "Protección en tiempo real configurada como deshabilitada: $($p.DisableRealtimeMonitoring)"
  "Análisis de descargas y adjuntos deshabilitado: $($p.DisableIOAVProtection)"
  'La consulta no cambia exclusiones ni políticas de Defender.'
} catch { "[NO VERIFICADO] No se pudieron consultar exclusiones de Defender: $($_.Exception.Message)" }
""", TimeSpan.FromSeconds(90)),

        new("reliability", "Windows", "Historial de fallos de aplicaciones, hardware y almacenamiento (30 días)", """
$ErrorActionPreference = 'Continue'
$since = (Get-Date).AddDays(-30)
function Test-ImportantDiagnosticEvent($e) {
  $provider = [string]$e.ProviderName
  $id = [int]$e.Id
  $message = [string]$e.Message
  if ($provider -match 'ESENT' -and $id -eq 474 -and $message -match '(?i)checksum|suma de comprobaci') { return $true }
  if ($provider -match 'Ntfs' -and $id -in 55,98,140) { return $true }
  if ($provider -match 'WHEA|Disk|storahci|stornvme|storport' -and $id -in 7,11,15,51,129,153,157) { return $true }
  if ($provider -match 'Kernel-Power' -and $id -eq 41) { return $true }
  if ($provider -match 'EventLog' -and $id -eq 6008) { return $true }
  if ($provider -match 'BugCheck|WER-SystemErrorReporting' -and $id -eq 1001) { return $true }
  return $false
}
try {
  # Categories are queried independently. Provider matching is counted before
  # deciding whether a category hit its sample cap, so unrelated event IDs cannot
  # falsely saturate NTFS/storage/WHEA groups.
  $definitions = @(
    @{ Name='ESENT 474'; LogName='Application'; Ids=@(474); Limit=120; Provider='ESENT' },
    @{ Name='Errores de aplicaciones'; LogName='Application'; Ids=@(1000,1001,1002); Limit=120; Provider='Application Error|Application Hang|Windows Error Reporting|WER' },
    @{ Name='NTFS'; LogName='System'; Ids=@(55,98,140); Limit=120; Provider='Ntfs' },
    @{ Name='Almacenamiento'; LogName='System'; Ids=@(7,11,15,51,129,153,157); Limit=120; Provider='Disk|storahci|stornvme|storport' },
    @{ Name='WHEA'; LogName='System'; Ids=@(1,17,18,19,20,46,47); Limit=120; Provider='WHEA' },
    @{ Name='Apagados y bugchecks'; LogName='System'; Ids=@(41,6008,1001); Limit=120; Provider='Kernel-Power|EventLog|BugCheck|WER-SystemErrorReporting|Windows Error Reporting' }
  )
  $records = New-Object System.Collections.Generic.List[object]
  $saturated = New-Object System.Collections.Generic.List[string]
  $scanBounded = New-Object System.Collections.Generic.List[string]
  $scanLimit = 1000

  foreach ($definition in $definitions) {
    $query = @(Get-WinEvent -FilterHashtable @{
      LogName=$definition.LogName
      Id=$definition.Ids
      StartTime=$since
    } -MaxEvents ($scanLimit + 1) -ErrorAction SilentlyContinue)
    $matching = @($query | Where-Object { $_.ProviderName -match $definition.Provider })
    foreach ($event in @($matching | Select-Object -First $definition.Limit)) {
      $records.Add($event)
    }
    "Grupo consultado: $($definition.Name) | Registros incluidos: $([math]::Min($matching.Count,$definition.Limit)) | Límite: $($definition.Limit)"
    if ($matching.Count -gt $definition.Limit) {
      $saturated.Add($definition.Name)
    }
    if ($query.Count -gt $scanLimit) {
      $scanBounded.Add($definition.Name)
    }
  }

  $events = @($records | Group-Object { "$($_.LogName)|$($_.RecordId)" } | ForEach-Object { $_.Group[0] })
  $app = @($events | Where-Object { $_.LogName -eq 'Application' })
  $sys = @($events | Where-Object { $_.LogName -eq 'System' })
  $importantSystem = @($sys | Where-Object { Test-ImportantDiagnosticEvent $_ })
  "Registros de aplicaciones incluidos en las muestras: $($app.Count)"
  "Registros del sistema incluidos en las muestras: $($sys.Count)"
  "Eventos del sistema/almacenamiento/hardware relevantes incluidos: $($importantSystem.Count)"
  if ($events.Count -eq 0) {
    '[OK] No se encontraron eventos de las categorías consultadas en los últimos 30 días.'
  } else {
    '=== EVENTOS DE APLICACIONES ==='
    foreach ($g in ($app | Group-Object ProviderName,Id | Sort-Object Count -Descending | Select-Object -First 30)) {
      $e = $g.Group | Sort-Object TimeCreated -Descending | Select-Object -First 1
      $msg = ($e.Message -replace '\s+',' ').Trim()
      if ($msg.Length -gt 260) { $msg = $msg.Substring(0,260) + '…' }
      $repeatedCrash = ($e.ProviderName -match 'Application Error|Application Hang' -and $g.Count -ge 3)
      $tag = if ((Test-ImportantDiagnosticEvent $e) -or $repeatedCrash) { 'AVISO' } else { 'INFO' }
      "[$tag] $($e.ProviderName) | Evento $($e.Id) | Registros en muestra: $($g.Count) | Último: $($e.TimeCreated) | $msg"
    }
    '=== EVENTOS DEL SISTEMA ==='
    foreach ($g in ($sys | Group-Object ProviderName,Id | Sort-Object Count -Descending | Select-Object -First 35)) {
      $e = $g.Group | Sort-Object TimeCreated -Descending | Select-Object -First 1
      $msg = ($e.Message -replace '\s+',' ').Trim()
      if ($msg.Length -gt 260) { $msg = $msg.Substring(0,260) + '…' }
      $tag = if (Test-ImportantDiagnosticEvent $e) { 'AVISO' } else { 'INFO' }
      "[$tag] $($e.ProviderName) | Evento $($e.Id) | Registros en muestra: $($g.Count) | Último: $($e.TimeCreated) | $msg"
    }
    if ($importantSystem.Count -gt 0) {
      '[AVISO] Se identificaron eventos específicos de almacenamiento, sistema de archivos, hardware o apagado inesperado.'
    } else {
      '[INFO] No se detectaron patrones de error de almacenamiento o hardware entre los eventos coincidentes.'
    }
  }
  if ($saturated.Count -gt 0) {
    '[INFO] Límite de muestra de la categoría alcanzado en: ' + ($saturated -join ', ') + '. Los registros mostrados son una muestra mínima observada; el total real puede ser mayor.'
  } else {
    '[INFO] Ninguna categoría dirigida alcanzó su límite de muestra.'
  }
  if ($scanBounded.Count -gt 0) {
    '[INFO] Alcanzado el límite de búsqueda preliminar de 1000 eventos en: ' + (($scanBounded | Select-Object -Unique) -join ', ') + '. Las categorías afectadas podrían contener coincidencias adicionales no incluidas.'
  } else {
    '[INFO] Ninguna búsqueda preliminar alcanzó el límite bruto de 1000 eventos.'
  }
} catch { "[NO VERIFICADO] No se pudo consultar el historial de confiabilidad: $($_.Exception.Message)" }
'Los eventos informativos de inicialización de VBS, Hyper-V y administración de energía del procesador no se clasifican como fallos. Los grupos se filtran por proveedor antes de calcular la saturación.'
""", TimeSpan.FromSeconds(180)),
        new("software-inventory", "Rendimiento", "Inventario de programas instalados", """
$ErrorActionPreference = 'Continue'
try {
  $paths = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
  )
  $apps = @(
    foreach ($path in $paths) {
      Get-ItemProperty $path -ErrorAction SilentlyContinue | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_.DisplayName) -and $_.SystemComponent -ne 1
      } | Select-Object DisplayName,DisplayVersion,Publisher,InstallDate,InstallLocation
    }
  ) | Sort-Object DisplayName,DisplayVersion -Unique
  "Programas inventariados: $($apps.Count)"
  '=== APLICACIONES INSTALADAS (MUESTRA DE HASTA 80) ==='
  $apps | Sort-Object InstallDate -Descending | Select-Object -First 80 DisplayName,DisplayVersion,Publisher,InstallDate | Format-Table -AutoSize | Out-String -Width 220
  'La presencia o antigüedad aparente no indica por sí sola que una aplicación esté desactualizada o sea peligrosa. No se desinstaló nada.'
} catch { "[NO VERIFICADO] No se pudo inventariar el software instalado: $($_.Exception.Message)" }
""", TimeSpan.FromSeconds(120)),

        new("power", "Windows", "Plan de energía, solicitudes activas y último despertar", """
$ErrorActionPreference = 'Continue'
'=== PLAN DE ENERGÍA ACTIVO ==='
try { & powercfg.exe /getactivescheme 2>&1 | Out-String -Width 180 } catch { "No se pudo consultar el plan: $($_.Exception.Message)" }
'=== PROCESOS O CONTROLADORES QUE SOLICITAN MANTENER EL EQUIPO ACTIVO ==='
try { & powercfg.exe /requests 2>&1 | Out-String -Width 200 } catch { "No se pudieron consultar solicitudes: $($_.Exception.Message)" }
'=== ÚLTIMO MOTIVO DE REACTIVACIÓN ==='
try { & powercfg.exe /lastwake 2>&1 | Out-String -Width 180 } catch { "No se pudo consultar la última reactivación: $($_.Exception.Message)" }
'Estas consultas no cambian el plan, la suspensión ni los temporizadores de energía.'
""", TimeSpan.FromSeconds(60))
    };
}