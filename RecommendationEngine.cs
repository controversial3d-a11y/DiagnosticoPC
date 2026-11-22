using System.Text.RegularExpressions;

namespace DiagnosticoPC;

internal sealed record RecommendationSource(string Name, string Url);

internal sealed record DiagnosticRecommendation(
    string Key,
    int SortOrder,
    string Priority,
    string Title,
    string Evidence,
    string WhyItMatters,
    IReadOnlyList<string> Steps,
    string? Command,
    string Caution,
    IReadOnlyList<RecommendationSource> Sources);

internal static class RecommendationEngine
{
    private static readonly RecommendationSource ChkdskSource =
        new("Microsoft Learn: comando CHKDSK", "https://learn.microsoft.com/windows-server/administration/windows-commands/chkdsk");
    private static readonly RecommendationSource NtfsSource =
        new("Microsoft: errores de corrupción y discos (incluye evento NTFS 98)", "https://learn.microsoft.com/troubleshoot/windows-server/backup-and-storage/troubleshoot-data-corruption-and-disk-errors");
    private static readonly RecommendationSource SfcSource =
        new("Microsoft Support: reparar archivos de Windows con DISM y SFC", "https://support.microsoft.com/es-es/windows/experience/backup-recovery/using-system-file-checker-in-windows");
    private static readonly RecommendationSource BugcheckSource =
        new("Microsoft Learn: Bug Check 0x119", "https://learn.microsoft.com/es-es/windows-hardware/drivers/debugger/bug-check-0x119---video-scheduler-internal-error");
    private static readonly RecommendationSource WindbgSource =
        new("Microsoft Learn: analizar un volcado de kernel con WinDbg", "https://learn.microsoft.com/es-es/windows-hardware/drivers/debugger/analyzing-a-kernel-mode-dump-file-with-windbg");
    private static readonly RecommendationSource GigabyteSource =
        new("GIGABYTE: soporte H610M K V2", "https://www.gigabyte.com/Motherboard/H610M-K-V2-rev-10/Support");
    private static readonly RecommendationSource SecureBootSource =
        new("Microsoft Support: habilitar Secure Boot", "https://support.microsoft.com/es-es/windows/security/secure-boot-enablement-guide");
    private static readonly RecommendationSource BitLockerSource =
        new("Microsoft Support: cifrado de unidad BitLocker", "https://support.microsoft.com/es-es/windows/security/encryption/bitlocker-drive-encryption");
    private static readonly RecommendationSource RecoveryKeySource =
        new("Microsoft Support: respaldar la clave de recuperación de BitLocker", "https://support.microsoft.com/es-es/windows/security/encryption/back-up-your-bitlocker-recovery-key");
    private static readonly RecommendationSource SmartScreenSource =
        new("Microsoft Support: Control de aplicaciones y explorador / SmartScreen", "https://support.microsoft.com/es-es/windows/security/windows-security/app-browser-control-in-the-windows-security-app");
    private static readonly RecommendationSource UpdateSource =
        new("Microsoft Support: reiniciar para aplicar actualizaciones", "https://support.microsoft.com/es-es/windows/deployment/updates-lifecycle/troubleshoot-problems-updating-windows");
    private static readonly RecommendationSource DriverSigningSource =
        new("Microsoft Learn: firma digital de controladores", "https://learn.microsoft.com/en-us/windows-hardware/drivers/install/driver-signing");
    private static readonly RecommendationSource AnyDeskSource =
        new("AnyDesk: seguridad y acceso desatendido", "https://support.anydesk.com/docs/es/unattended-access");
    private static readonly RecommendationSource MonectSource =
        new("Monect: funciones oficiales de control remoto y streaming", "https://www.monect.com/pc");
    private static readonly RecommendationSource DefenderSource =
        new("Microsoft Support: opciones de examen de Seguridad de Windows", "https://support.microsoft.com/es-es/windows/security/threat-malware-protection/virus-and-threat-protection-in-the-windows-security-app");

    private sealed record MissingStartupEntry(string Name, string Origin, string Executable, string RelatedSoftware);

    public static IReadOnlyList<DiagnosticRecommendation> Build(IEnumerable<DiagnosticEntry> entries)
    {
        var all = entries.ToList();
        var byId = all.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
        string Details(string id) => byId.TryGetValue(id, out var entry) ? entry.Details ?? string.Empty : string.Empty;
        bool Concern(string id) => byId.TryGetValue(id, out var entry) &&
            entry.Level is FindingLevel.Warning or FindingLevel.Critical or FindingLevel.NotVerified;
        bool Has(string id, params string[] terms) => terms.Any(term =>
            Details(id).Contains(term, StringComparison.OrdinalIgnoreCase));
        string allEvents = Details("events") + Environment.NewLine + Details("reliability");
        string allSecurity = Details("security-posture");
        string allDevices = Details("devices");
        string systemDrive = GetSystemDrive();
        var items = new List<DiagnosticRecommendation>();

        bool ntfsDetected =
            (Concern("chkdsk") && Has("chkdsk", "bitmap", "mapa de bits", "MFT", "CHKDSK_EXIT_CODE=3", "detectó problemas", "detecto problemas")) ||
            allEvents.Contains("Evento 98", StringComparison.OrdinalIgnoreCase) ||
            allEvents.Contains("needs to be taken offline", StringComparison.OrdinalIgnoreCase);

        if (ntfsDetected)
        {
            string foundPath = ExtractFoundFolder(Details("chkdsk"), systemDrive);
            string evidence = BuildEvidence(
                Details("chkdsk"),
                "CHKDSK informó inconsistencias en los metadatos NTFS.",
                new[] { "El atributo BITMAP de la tabla maestra de archivos (MFT) es incorrecto.",
                        "El mapa de bits del volumen es incorrecto.",
                        "El resultado de CHKDSK no es correcto y no se ejecutó con /F." });
            if (allEvents.Contains("Evento 98", StringComparison.OrdinalIgnoreCase))
                evidence += " También se registró el evento NTFS 98, que solicita una comprobación completa del volumen.";
            if (foundPath.Length > 0)
                evidence += " La salida menciona la carpeta " + foundPath + " con archivos huérfanos recuperados.";

            items.Add(new(
                "ntfs-repair", 10, "URGENTE", "Respaldar y reparar el sistema de archivos de " + systemDrive,
                evidence,
                "Las inconsistencias NTFS pueden afectar la estructura de archivos y provocar pérdida o inaccesibilidad de datos. El estado Healthy del disco físico no corrige ni descarta corrupción lógica.",
                new[]
                {
                    "Antes de reparar, copiá los documentos, fotografías y proyectos importantes a un disco externo u otro destino independiente. Abrí algunos archivos de la copia para comprobar que se copiaron bien.",
                    foundPath.Length > 0
                        ? "Revisá " + foundPath + " y copiá a otro lugar cualquier archivo que reconozcas y necesites. No borres esa carpeta."
                        : "Revisá si existe una carpeta found.* en la raíz de " + systemDrive + " y conservá cualquier archivo que necesites.",
                    "Una vez verificado el respaldo, abrí Terminal o Símbolo del sistema como administrador y ejecutá el comando indicado.",
                    "Si Windows pregunta si querés programar la comprobación para el próximo reinicio, confirmalo con Y y reiniciá cuando puedas dejar que termine sin interrumpirla.",
                    "Cuando Windows vuelva a iniciar, ejecutá otra vez Diagnóstico PC y comprobá que CHKDSK ya no informe inconsistencias."
                },
                "chkdsk " + systemDrive + " /f",
                "Este comando modifica el sistema de archivos. No lo ejecutes antes del respaldo. No agregues /r de entrada: primero resolvé la corrupción lógica y evaluá el estado físico con más evidencia.",
                new[] { ChkdskSource, NtfsSource }));
        }

        bool dismRepairable = Concern("dism") &&
            Has("dism", "componentes puede repararse", "se puede reparar", "component store is repairable");
        bool dismUnrepairable = Concern("dism") &&
            Has("dism", "no puede repararse", "no se puede reparar", "component store cannot be repaired");
        if (dismRepairable || dismUnrepairable)
        {
            items.Add(new(
                "dism-repair", 20, "ALTA", "Reparar el almacén de componentes de Windows (DISM)",
                dismUnrepairable
                    ? "DISM informa daños en el almacén de componentes que no puede reparar automáticamente."
                    : "DISM ScanHealth encontró daños reparables en el almacén de componentes. ScanHealth no realiza la reparación.",
                "El almacén de componentes proporciona archivos que Windows utiliza para mantener y reparar el sistema. Conviene resolver la integridad del volumen primero y después reparar Windows.",
                new[]
                {
                    ntfsDetected
                        ? "Hacé esto después de completar la copia de seguridad y la reparación de NTFS, no antes."
                        : "Hacé una copia de seguridad de los archivos importantes antes de iniciar reparaciones del sistema.",
                    "Abrí Terminal o Símbolo del sistema como administrador.",
                    "Ejecutá el comando indicado y esperá a que finalice sin cerrar la ventana.",
                    "Si DISM informa que la operación terminó correctamente, seguí con SFC /scannow, que aparece como la siguiente acción si también se detectaron daños en archivos protegidos."
                },
                "DISM.exe /Online /Cleanup-Image /RestoreHealth",
                "RestoreHealth sí modifica componentes de Windows. Si informa un error, conservá el código y el registro DISM.log; no repitas comandos de reparación al azar.",
                new[] { SfcSource }));
        }

        bool sfcDamaged = (Concern("sfc") || byId.ContainsKey("sfc")) &&
            IsSfcIntegrityViolation(Details("sfc"));
        bool sfcUnknown = byId.TryGetValue("sfc", out var sfcEntry) &&
            sfcEntry.Level == FindingLevel.NotVerified && !sfcDamaged;
        if (sfcDamaged)
        {
            items.Add(new(
                "sfc-repair", 30, "ALTA", "Reparar los archivos protegidos detectados por SFC",
                "SFC encontró infracciones de integridad en archivos protegidos de Windows. La prueba VerifyOnly solo verifica: no reemplaza ni repara archivos.",
                "Los archivos protegidos dañados pueden causar inestabilidad, errores de Windows y fallos de funciones del sistema.",
                new[]
                {
                    "Primero completá DISM /RestoreHealth si la acción anterior está presente.",
                    "Abrí Terminal o Símbolo del sistema como administrador.",
                    "Ejecutá el comando indicado y esperá a que llegue al 100 %.",
                    "Leé el mensaje final: debe indicar si no encontró infracciones, si reparó archivos o si no pudo reparar algunos.",
                    "Reiniciá si Windows lo solicita y repetí el diagnóstico para confirmar el resultado."
                },
                "sfc /scannow",
                "No confundas este comando con /verifyonly: /scannow sí puede reemplazar archivos del sistema. Si quedan archivos sin reparar, conservá C:\\Windows\\Logs\\CBS\\CBS.log.",
                new[] { SfcSource }));
        }
        else if (sfcUnknown)
        {
            items.Add(new(
                "sfc-unverified", 55, "MEDIA", "Volver a comprobar la integridad de Windows (SFC)",
                "SFC no produjo una conclusión que la aplicación pudiera reconocer. No debe interpretarse como un resultado correcto.",
                "Sin el mensaje final no se puede saber si los archivos protegidos están íntegros.",
                new[]
                {
                    "Ejecutá el análisis como administrador después de completar las reparaciones pendientes.",
                    "Volvé a ejecutar el diagnóstico y revisá el resultado de SFC.",
                    "Si sigue sin haber una conclusión, revisá C:\\Windows\\Logs\\CBS\\CBS.log."
                },
                "sfc /verifyonly",
                "VerifyOnly es de solo verificación. No repara los archivos; para reparar se utiliza sfc /scannow después de DISM.",
                new[] { SfcSource }));
        }

        bool hasVideoBugcheck = allEvents.Contains("0x00000119", StringComparison.OrdinalIgnoreCase) ||
                                allEvents.Contains("VIDEO_SCHEDULER_INTERNAL_ERROR", StringComparison.OrdinalIgnoreCase);
        if (hasVideoBugcheck)
        {
            string dump = ExtractDumpPath(allEvents);
            string gpuText = Details("gpu");
            var sources = new List<RecommendationSource> { BugcheckSource, WindbgSource };
            bool amd = gpuText.Contains("AMD", StringComparison.OrdinalIgnoreCase);
            if (amd)
                sources.Add(new("AMD: controladores oficiales para la GPU detectada", "https://www.amd.com/es/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-5000-series/amd-radeon-rx-5700-xt.html"));

            items.Add(new(
                "video-bugcheck", 40, "ALTA", "Analizar la pantalla azul 0x119 antes de cambiar el controlador gráfico",
                "Windows registró VIDEO_SCHEDULER_INTERNAL_ERROR (0x119). El parámetro 1 es 0x5, que Microsoft describe como un fallo del controlador al procesar un comando del sistema o de paginación.",
                "El código apunta a la ruta de programación de vídeo, pero no demuestra que la GPU esté físicamente dañada ni identifica por sí solo qué controlador falló.",
                new[]
                {
                    dump.Length > 0
                        ? "Conservá el archivo de volcado " + dump + ". No lo borres."
                        : "Conservá los archivos .dmp que existan en C:\\Windows\\Minidump.",
                    "Instalá WinDbg desde Microsoft Store o desde las herramientas oficiales de depuración de Microsoft.",
                    "Abrí WinDbg, elegí File > Open dump file y cargá el archivo .dmp.",
                    "En la consola de comandos de WinDbg ejecutá !analyze -v.",
                    "Revisá MODULE_NAME, IMAGE_NAME y FAILURE_BUCKET_ID. Guardá el resultado.",
                    amd
                        ? "Solo si el análisis señala el controlador AMD, compará la versión instalada con el controlador recomendado para Radeon RX 5700 XT en el sitio oficial de AMD. No instales controladores de páginas de terceros."
                        : "Solo si el análisis identifica un controlador de vídeo, consultá primero la versión recomendada por el fabricante de esa GPU."
                },
                "!analyze -v",
                "No desinstales ni sustituyas el controlador gráfico basándote únicamente en el código 0x119. Primero identificá el módulo del volcado.",
                sources));
        }

        bool esentChecksum = allEvents.Contains("ESENT", StringComparison.OrdinalIgnoreCase) &&
            (allEvents.Contains("Evento 474", StringComparison.OrdinalIgnoreCase) ||
             allEvents.Contains("checksum", StringComparison.OrdinalIgnoreCase) ||
             allEvents.Contains("suma de comprobación", StringComparison.OrdinalIgnoreCase));
        if (esentChecksum)
        {
            items.Add(new(
                "esent-srudb", 50, "MEDIA", "Volver a comprobar la base SRUDB.dat (ESENT 474)",
                "Se repiten errores ESENT 474 de suma de comprobación en C:\\Windows\\System32\\SRU\\SRUDB.dat.",
                "Es una base de datos utilizada por Windows para datos de uso de recursos. Los eventos repetidos merecen seguimiento, pero no prueban una infección ni confirman por sí solos una avería física del SSD.",
                new[]
                {
                    "No borres, renombres ni reemplaces SRUDB.dat manualmente.",
                    "Primero completá la reparación NTFS y las comprobaciones DISM/SFC recomendadas arriba.",
                    "Reiniciá Windows y ejecutá un nuevo diagnóstico para verificar si los eventos 474 continúan.",
                    "Si siguen apareciendo, guardá el registro Aplicación con el proveedor ESENT, el evento 474 y las marcas horarias para soporte técnico."
                },
                null,
                "No se recomienda ejecutar esentutl /p ni borrar SRUDB.dat como una acción automática: el informe no aporta evidencia suficiente para justificar una reparación destructiva de esa base.",
                new[]
                {
                    new RecommendationSource("Microsoft Q&A: caso similar de ESENT 474 en SRUDB.dat (referencia comunitaria, no procedimiento oficial)", "https://learn.microsoft.com/en-us/answers/questions/3769871/malware-or-hardware-defect-or-both")
                }));
        }

        bool missingIntelHid = allDevices.Contains("ACPI\\INTC1078", StringComparison.OrdinalIgnoreCase);
        bool missingIntelSerialIo = allDevices.Contains("ACPI\\INTC1056", StringComparison.OrdinalIgnoreCase);
        if (missingIntelHid || missingIntelSerialIo)
        {
            var missing = new List<string>();
            if (missingIntelHid) missing.Add("Intel HID Event Filter (ACPI\\INTC1078)");
            if (missingIntelSerialIo) missing.Add("Intel Serial I/O GPIO Host Controller (ACPI\\INTC1056)");
            items.Add(new(
                "intel-platform-drivers", 60, "MEDIA", "Instalar los controladores Intel que faltan",
                "El Administrador de dispositivos informa código 28 para: " + string.Join("; ", missing) + ".",
                "Son dispositivos de plataforma de la placa madre. El código 28 significa que Windows no tiene un controlador instalado para el dispositivo indicado.",
                new[]
                {
                    "Completá primero las reparaciones de NTFS y Windows.",
                    "Confirmá la revisión física exacta impresa en la placa madre.",
                    "Abrí el soporte oficial de GIGABYTE para H610M K V2 y seleccioná Windows 11 de 64 bits.",
                    "Descargá solamente los paquetes Intel HID Event Filter y/o Intel Serial I/O que correspondan a los dispositivos detectados.",
                    "Instalalos desde los instaladores oficiales, reiniciá y repetí la consulta de dispositivos."
                },
                null,
                "No uses instaladores automáticos de controladores ni paquetes de sitios de terceros. La revisión exacta de la placa debe coincidir con la página elegida.",
                new[] { GigabyteSource }));
        }
        if (allDevices.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Any(line =>
            Regex.IsMatch(line, @"(?i)\[(?:AVISO|WARNING)\].*(?:Código|Code)\s*28\b") &&
            !line.Contains("INTC1078", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("INTC1056", StringComparison.OrdinalIgnoreCase)))
        {
            string affected = string.Join(" | ", allDevices.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => Regex.IsMatch(line, @"(?i)\[(?:AVISO|WARNING)\].*(?:Código|Code)\s*28\b") &&
                    !line.Contains("INTC1078", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("INTC1056", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Trim()));
            items.Add(new(
                "generic-code28", 60, "MEDIA", "Identificar e instalar el controlador del dispositivo con código 28",
                string.IsNullOrWhiteSpace(affected) ? "Windows informa que falta un controlador para uno o más dispositivos." : affected,
                "El código 28 significa que el controlador no está instalado o no se encontró un paquete compatible. Primero hay que identificar el dispositivo mediante su identificador de hardware.",
                new[]
                {
                    "Abrí Administrador de dispositivos desde el menú Inicio.",
                    "Abrí Propiedades del dispositivo con el código 28 y entrá en la pestaña Detalles.",
                    "Seleccioná Id. de hardware y anotá el primer identificador de la lista.",
                    "Con ese identificador, buscá el controlador en Windows Update o en la página oficial del fabricante de la PC, placa madre o dispositivo.",
                    "Instalá únicamente el paquete correspondiente al modelo y a Windows 11 x64; reiniciá y volvé a ejecutar el diagnóstico."
                },
                null,
                "No uses paquetes genéricos de actualización de controladores ni elimines dispositivos de sistema que todavía no hayas identificado.",
                new[] { new RecommendationSource("Microsoft Support: códigos de error del Administrador de dispositivos", "https://support.microsoft.com/es-es/windows/hardware/drivers/error-codes-in-device-manager-in-windows") }));
        }

        if (byId.TryGetValue("updates", out var updates) &&
            (updates.Level is FindingLevel.Warning or FindingLevel.Critical) &&
            (updates.Details.Contains("reinicio pendiente", StringComparison.OrdinalIgnoreCase) ||
             updates.Details.Contains("operaciones de archivos pendientes", StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(new(
                "pending-restart", 70, "MEDIA", "Completar el reinicio pendiente de Windows",
                "Windows informa operaciones de archivos pendientes de reinicio. Es posible que una actualización o instalación aún no haya terminado de aplicarse.",
                "El reinicio completa operaciones de mantenimiento que Windows dejó programadas. No confirma por sí solo que una actualización haya fallado.",
                new[]
                {
                    "Completá primero el respaldo y la reparación programada de NTFS, si aparece en la lista.",
                    "Guardá tus trabajos y cerrá las aplicaciones.",
                    "Usá Inicio > Inicio/apagado > Actualizar y reiniciar si Windows lo ofrece.",
                    "Al volver a iniciar, abrí Configuración > Windows Update y comprobá si todavía solicita otro reinicio."
                },
                null,
                "No fuerces el apagado durante una comprobación de disco o una actualización. El reinicio programado para CHKDSK puede coincidir con este mantenimiento.",
                new[] { UpdateSource }));
        }

        if (allSecurity.Contains("Secure Boot habilitado: False", StringComparison.OrdinalIgnoreCase) ||
            allSecurity.Contains("Secure Boot: False", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new(
                "secure-boot", 80, "PREVENTIVA", "Revisar Secure Boot en UEFI",
                "La consulta del firmware indica que Secure Boot está desactivado.",
                "Secure Boot ayuda a impedir que se carguen componentes de arranque no confiables. Es una mejora de seguridad, no la causa demostrada de los problemas NTFS o SFC.",
                new[]
                {
                    "Dejá esta tarea para después de estabilizar NTFS y Windows.",
                    "En Windows abrí Información del sistema (msinfo32) y verificá que Modo de BIOS indique UEFI.",
                    "Consultá la guía del fabricante para el modelo exacto de placa madre.",
                    "Solo si el sistema ya arranca en UEFI y el firmware presenta la opción compatible, evaluá habilitar Secure Boot desde la configuración UEFI."
                },
                null,
                "No cambies CSM/Legacy, el modo de arranque ni el orden de arranque a ciegas. Un cambio incorrecto puede impedir que Windows inicie.",
                new[] { SecureBootSource }));
        }

        var unencryptedVolumes = Regex.Matches(allSecurity,
            @"(?im)^\s*([A-Z]:)\s+(?:OperatingSystem|Data)\s+FullyDecrypted\s+Off\b")
            .Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (unencryptedVolumes.Count > 0)
        {
            items.Add(new(
                "bitlocker", 90, "PREVENTIVA", "Evaluar cifrado BitLocker para las unidades sin cifrar",
                "BitLocker informa FullyDecrypted / Protection Off para: " + string.Join(", ", unencryptedVolumes) + ".",
                "El cifrado protege los datos si se pierde o roba el equipo o la unidad. No repara Windows ni evita el malware.",
                new[]
                {
                    "Considerá esta mejora después de resolver los errores del sistema de archivos.",
                    "Antes de activarlo, hacé una copia de seguridad de los datos importantes.",
                    "Si decidís cifrar, activá BitLocker desde Administrar BitLocker en Windows Pro.",
                    "Guardá y comprobá la clave de recuperación en una ubicación separada y segura, a la que puedas acceder si el equipo pide recuperación."
                },
                null,
                "No actives el cifrado antes de tener respaldo y una clave de recuperación guardada fuera de la unidad cifrada. Esta es una mejora opcional de protección de datos.",
                new[] { BitLockerSource, RecoveryKeySource }));
        }

        if (allSecurity.Contains("estado no determinado", StringComparison.OrdinalIgnoreCase) ||
            allSecurity.Contains("no expuso un valor legible", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new(
                "smartscreen-check", 100, "MEDIA", "Comprobar manualmente Microsoft Defender SmartScreen",
                "Windows no expuso un valor legible en las ubicaciones consultadas. El diagnóstico no puede afirmar que SmartScreen esté desactivado.",
                "SmartScreen y la protección basada en reputación pueden advertir sobre aplicaciones, archivos y sitios conocidos por ser maliciosos o no deseados.",
                new[]
                {
                    "Abrí Seguridad de Windows.",
                    "Entrá en Control de aplicaciones y explorador > Protección basada en reputación.",
                    "Revisá el estado de Comprobar aplicaciones y archivos y las opciones de protección correspondientes.",
                    "Si el equipo está administrado por una organización, no cambies las políticas sin consultar al administrador."
                },
                null,
                "El resultado de la prueba es indeterminado, no una detección de protección apagada.",
                new[] { SmartScreenSource }));
        }

        string startupDetails = Details("startup");
        string networkDetails = Details("network");
        string serviceDetails = Details("services");
        bool anyDeskFound =
            startupDetails.Contains("AnyDesk", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(networkDetails, @"(?im)^.*\|\s*PID\s+\d+\s*\|\s*AnyDesk(?:\s*\||\s*$)") ||
            Regex.IsMatch(serviceDetails, @"(?im)^\s*AnyDesk\s+\|.*\|\s*Estado:");
        if (anyDeskFound)
        {
            items.Add(new(
                "remote-access", 110, "MEDIA", "Verificar el acceso remoto de AnyDesk",
                "Se encontró AnyDesk configurado para iniciar y/o con su servicio activo. Su presencia no demuestra acceso no autorizado ni exposición desde Internet.",
                "Una herramienta de acceso remoto puede permitir controlar el equipo sin estar delante de él si se configuró acceso desatendido.",
                new[]
                {
                    "Confirmá que instalaste AnyDesk y que reconocés su uso.",
                    "Si lo usás, revisá Configuración > Seguridad > Acceso desatendido y limitá los permisos al mínimo necesario.",
                    "Si necesitás acceso desatendido, usá una contraseña única y robusta, habilitá autenticación de dos factores y restringí los dispositivos autorizados.",
                    "Si no lo reconocés o no lo necesitás, revisá su procedencia y desinstalalo desde Configuración > Aplicaciones > Aplicaciones instaladas; no borres sus archivos manualmente."
                },
                null,
                "Un puerto local en escucha no significa que sea accesible desde Internet. No se debe clasificar AnyDesk como malware solo por estar instalado.",
                new[] { AnyDeskSource }));
        }

        if (Details("driver-audit").Contains("Scp Virtual Bus Driver", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(Details("driver-audit"), @"(?im)^\s*IsSigned\s*:\s*False\s*$"))
        {
            items.Add(new(
                "unsigned-scp-driver", 120, "MEDIA", "Comprobar el controlador Scp Virtual Bus Driver",
                "Windows marcó explícitamente Scp Virtual Bus Driver como no firmado. El registro identifica al proveedor Scarlet.Crush Productions.",
                "Una firma ayuda a comprobar la identidad del editor y la integridad del paquete del controlador. La ausencia de firma no demuestra por sí sola que sea malware.",
                new[]
                {
                    "Comprobá si instalaste SCPToolkit u otro software antiguo para mandos/controladores.",
                    "Si reconocés el software y lo necesitás, no elimines el controlador solo por este resultado.",
                    "Si no lo reconocés, verificá su origen y el programa asociado antes de quitarlo mediante el desinstalador de Windows."
                },
                null,
                "No borres el archivo .sys ni elimines el dispositivo manualmente. La aplicación no deshabilita controladores.",
                new[] { DriverSigningSource }));
        }
        else if (Regex.IsMatch(Details("driver-audit"), @"(?im)^\s*IsSigned\s*:\s*False\s*$"))
        {
            items.Add(new(
                "unsigned-driver-review", 120, "MEDIA", "Revisar el origen del controlador sin firma",
                "Windows marcó uno o más controladores como no firmados. La evidencia técnica identifica el dispositivo y proveedor; la ausencia de firma no demuestra por sí sola que sea malware.",
                "La firma digital permite comprobar la identidad del editor y la integridad del paquete. Los controladores de kernel sin firma requieren especial cautela.",
                new[]
                {
                    "Identificá el nombre del controlador, el proveedor, el archivo INF y el dispositivo asociado en la evidencia técnica.",
                    "Confirmá qué programa o hardware lo instaló y si lo seguís utilizando.",
                    "Comprobá si el fabricante ofrece una versión firmada y compatible con Windows 11 x64.",
                    "Si no reconocés el controlador, verificá su procedencia con el proveedor o soporte técnico antes de retirarlo."
                },
                null,
                "No borres archivos .sys ni elimines el controlador manualmente. Algunos programas antiguos o dispositivos virtuales legítimos pueden tener controladores sin firma.",
                new[] { DriverSigningSource }));
        }

        if (Details("defender").Contains("Protección en tiempo real: False", StringComparison.OrdinalIgnoreCase) ||
            Details("defender").Contains("Antivirus habilitado: False", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new(
                "defender-disabled", 15, "URGENTE", "Restablecer la protección antivirus de Microsoft Defender",
                "La consulta indica que el antivirus o la protección en tiempo real está deshabilitada.",
                "Sin protección en tiempo real, los archivos y procesos no reciben la supervisión continua habitual de Defender.",
                new[]
                {
                    "Abrí Seguridad de Windows > Protección contra virus y amenazas > Administrar la configuración.",
                    "Comprobá si Protección en tiempo real está desactivada y si hay otro antivirus instalado.",
                    "Si no hay otro antivirus administrando la protección, activá Defender desde Seguridad de Windows.",
                    "Después ejecutá un examen completo. Si sospechás una infección persistente, considerá Microsoft Defender sin conexión."
                },
                null,
                "No desinstales otro antivirus ni cambies políticas de organización sin identificar primero quién administra la protección.",
                new[] { DefenderSource }));
        }

        if (Regex.IsMatch(Details("firewall"), @"(?im)^\s*(Domain|Private|Public)\s+False\b"))
        {
            items.Add(new(
                "firewall-disabled", 16, "URGENTE", "Revisar los perfiles desactivados del Firewall de Windows",
                "Al menos un perfil consultado aparece deshabilitado.",
                "El Firewall de Windows filtra conexiones de red según el perfil activo.",
                new[]
                {
                    "Abrí Seguridad de Windows > Firewall y protección de red.",
                    "Identificá qué perfil está desactivado y si otro producto de seguridad administra el firewall.",
                    "Si no hay un firewall alternativo, activá el perfil usado por la conexión."
                },
                null,
                "No cambies reglas de entrada o salida a ciegas; primero identificá si un producto de terceros administra la protección.",
                new[] { new RecommendationSource("Microsoft Support: Firewall y protección de red", "https://support.microsoft.com/es-es/windows/security/firewall-and-network-protection-in-windows-security") }));
        }

        if (Details("defender-exclusions").Contains("Total de exclusiones explícitas consultadas: ", StringComparison.OrdinalIgnoreCase) &&
            !Details("defender-exclusions").Contains("Total de exclusiones explícitas consultadas: 0", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new(
                "defender-exclusions", 130, "MEDIA", "Revisar las exclusiones de Microsoft Defender",
                "Se encontraron exclusiones configuradas para rutas, procesos, extensiones o direcciones IP.",
                "Una exclusión puede impedir que Defender examine elementos concretos. Las exclusiones amplias reducen la cobertura.",
                new[]
                {
                    "Abrí Seguridad de Windows > Protección contra virus y amenazas > Administrar la configuración > Exclusiones.",
                    "Revisá el motivo y el editor de cada exclusión.",
                    "Quitá solamente las que no reconozcas o ya no sean necesarias, después de confirmar que no las necesita un programa legítimo."
                },
                null,
                "No elimines exclusiones de software profesional o de trabajo sin confirmar su función.",
                new[] { DefenderSource }));
        }

        var missingStartupEntries = ExtractMissingStartupEntries(
            Details("startup-audit"), Details("software-inventory"));
        if (missingStartupEntries.Count > 0)
        {
            string evidence = "Se detectaron " + missingStartupEntries.Count +
                " entradas cuyo ejecutable no existe en la ruta configurada:" + Environment.NewLine +
                string.Join(Environment.NewLine, missingStartupEntries.Select(x =>
                    "• " + x.Name + " | Origen: " + x.Origin +
                    " | Ruta inexistente: " + x.Executable +
                    " | " + x.RelatedSoftware));
            var steps = missingStartupEntries.Select(x =>
                "Revisá la entrada «" + x.Name + "» (" + x.Origin + "): el ejecutable " +
                x.Executable + " no existe en esa ubicación. " + x.RelatedSoftware).ToList();
            steps.Add("Abrí Configuración > Aplicaciones > Inicio y localizá cada entrada identificada.");
            steps.Add("Si confirmás que una entrada quedó obsoleta, deshabilitá únicamente esa entrada desde la interfaz de Windows o desde la propia aplicación.");
            items.Add(new(
                "stale-startup", 140, "BAJA", "Revisar individualmente las entradas de inicio con rutas inexistentes",
                evidence,
                "Una ruta ausente puede corresponder a una actualización, un cambio de carpeta o un resto de una versión anterior. No demuestra infección ni significa que toda la aplicación esté desinstalada.",
                steps,
                null,
                "No edites claves Run del Registro ni borres archivos manualmente. Si el inventario muestra que el programa sigue instalado, no lo desinstales por este hallazgo: solo se comprobó que ese ejecutable de inicio no existe en la ruta registrada.",
                new[] { new RecommendationSource("Microsoft Support: configurar las aplicaciones de inicio en Windows", "https://support.microsoft.com/es-es/windows/configurar-las-aplicaciones-de-inicio-en-windows-115a420a-0bff-4a6f-90e0-1934c844e473") }));
        }

        bool monectFound =
            startupDetails.Contains("MonectServerService", StringComparison.OrdinalIgnoreCase) ||
            startupDetails.Contains("PC Remote Receiver", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(networkDetails, @"(?im)^.*\|\s*PID\s+\d+\s*\|\s*MonectServerService(?:\s*\||\s*$)") ||
            Regex.IsMatch(serviceDetails, @"(?im)^\s*MonectServerService\s+\|.*\|\s*Estado:") ||
            Regex.IsMatch(serviceDetails, @"(?im)^\s*\S+\s+\|\s*PC Remote Receiver[^|]*\|\s*Estado:");
        if (monectFound)
        {
            items.Add(new(
                "remote-access-monect", 111, "MEDIA", "Verificar Monect PC Remote / PC Remote Receiver",
                "Se encontró el servicio o proceso MonectServerService. Monect ofrece control remoto de PC y streaming entre dispositivos. La presencia del servicio no demuestra actividad maliciosa ni exposición desde Internet.",
                "Este software puede permitir controlar o transmitir la pantalla del equipo desde otro dispositivo. Conviene confirmar que lo instalaste y que reconocés sus conexiones y permisos.",
                new[]
                {
                    "Confirmá si instalaste Monect PC Remote o PC Remote Receiver y si lo utilizás para controlar el equipo desde un teléfono u otro dispositivo.",
                    "Si lo reconocés y lo necesitás, revisá la configuración de conexión y permití el uso únicamente en las redes y dispositivos que correspondan.",
                    "Si no lo reconocés, comprobá el editor y la ubicación del ejecutable asociado al servicio antes de tomar medidas.",
                    "Si confirmás que no lo necesitás, desinstalá PC Remote Receiver desde Configuración > Aplicaciones > Aplicaciones instaladas; no borres el ejecutable ni el servicio manualmente."
                },
                null,
                "Los puertos TCP locales en escucha no prueban que el servicio sea accesible desde Internet. No clasifiques Monect como malware únicamente por el nombre del servicio o por los puertos que utiliza.",
                new[] { MonectSource }));
        }

        if (allEvents.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase) &&
            allEvents.Contains("Evento 41", StringComparison.OrdinalIgnoreCase) &&
            !hasVideoBugcheck)
        {
            items.Add(new(
                "unexpected-shutdown", 150, "MEDIA", "Investigar los reinicios inesperados",
                "El registro incluye Kernel-Power 41 y/o EventLog 6008. Esos eventos indican que Windows no se apagó limpiamente; no identifican por sí solos la causa.",
                "Puede deberse a un bloqueo, pérdida de energía, reinicio forzado o una comprobación de errores.",
                new[]
                {
                    "Anotá la hora exacta y qué estabas haciendo cuando ocurrió el reinicio.",
                    "Buscá eventos BugCheck/WHEA inmediatamente anteriores o posteriores.",
                    "Si se repite, conservá los minidumps y revisá temperatura, alimentación y controladores con pruebas específicas."
                },
                null,
                "No reemplaces la fuente ni declares averiado un componente basándote solo en Kernel-Power 41.",
                new[] { new RecommendationSource("Microsoft Learn: solución de problemas de eventos de almacenamiento y sistema", "https://learn.microsoft.com/troubleshoot/windows-server/backup-and-storage/troubleshoot-data-corruption-and-disk-errors") }));
        }

        return items.OrderBy(x => x.SortOrder).ThenBy(x => x.Title, StringComparer.CurrentCulture).ToList();
    }

    private static List<MissingStartupEntry> ExtractMissingStartupEntries(string details, string softwareInventory)
    {
        var result = new List<MissingStartupEntry>();
        var sections = Regex.Matches(details,
            @"(?ms)^=== (?<name>[^\r\n]+) ===\s*\r?\n(?<block>.*?)(?=^=== |\z)");

        foreach (Match section in sections)
        {
            string block = section.Groups["block"].Value;
            if (!block.Contains("El archivo no se encontró en la ruta indicada", StringComparison.OrdinalIgnoreCase))
                continue;

            string name = section.Groups["name"].Value.Trim();
            string origin = Regex.Match(block, @"(?im)^\s*Origen:\s*(?<value>.+)$").Groups["value"].Value.Trim();
            string executable = Regex.Match(block, @"(?im)^\s*Ejecutable referenciado:\s*(?<value>.+)$").Groups["value"].Value.Trim();
            string related = FindRelatedInstalledSoftware(name, executable, softwareInventory);
            result.Add(new MissingStartupEntry(
                name,
                string.IsNullOrWhiteSpace(origin) ? "Origen no informado" : origin,
                string.IsNullOrWhiteSpace(executable) ? "Ruta no extraída" : executable,
                related));
        }

        return result;
    }

    private static string FindRelatedInstalledSoftware(string name, string executable, string inventory)
    {
        string probe = name + " " + executable;
        string[] lines = inventory.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        string? MatchLine(params string[] terms) =>
            lines.FirstOrDefault(line => terms.Any(term =>
                line.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Programas inventariados:", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("APLICACIONES INSTALADAS", StringComparison.OrdinalIgnoreCase)));

        if (probe.Contains("Opera", StringComparison.OrdinalIgnoreCase))
        {
            string? found = MatchLine("Opera GX");
            return found is null
                ? "No aparece una coincidencia Opera GX en la muestra del inventario (esto no confirma que esté desinstalado)."
                : "Coincidencia en inventario: " + found.Trim() + ". La aplicación figura instalada; la ruta de inicio concreta no existe.";
        }

        if (probe.Contains("Roblox", StringComparison.OrdinalIgnoreCase))
        {
            string? found = MatchLine("Roblox");
            return found is null
                ? "No aparece una coincidencia Roblox en la muestra del inventario (esto no confirma que esté desinstalado)."
                : "Coincidencia en inventario: " + found.Trim() + ". La aplicación figura instalada; la ruta de inicio concreta no existe.";
        }

        if (probe.Contains("Nefarius", StringComparison.OrdinalIgnoreCase) ||
            probe.Contains("DsHidMini", StringComparison.OrdinalIgnoreCase))
        {
            string? found = MatchLine("Nefarius", "DsHidMini");
            return found is null
                ? "No aparece una coincidencia Nefarius/DsHidMini en la muestra del inventario (esto no confirma que esté desinstalado)."
                : "Coincidencia en inventario: " + found.Trim() + ". El componente o paquete relacionado figura instalado; verificá la versión y la ruta del actualizador.";
        }

        if (probe.Contains("AMDNoiseSuppression", StringComparison.OrdinalIgnoreCase))
        {
            string? found = MatchLine("AMD");
            return found is null
                ? "No aparece software AMD en la muestra del inventario."
                : "Coincidencia de fabricante en inventario: " + found.Trim() + ". Esto no confirma que AMD Noise Suppression esté instalado ni que ese ejecutable sea necesario.";
        }

        return "No se identificó una aplicación asociada con suficiente precisión en la muestra del inventario.";
    }

    private static bool IsSfcIntegrityViolation(string text)
    {
        string compact = Compact(text);
        if (compact.Contains("noseencontraroninfraccionesdeintegridad", StringComparison.Ordinal) ||
            compact.Contains("didnotfindanyintegrityviolations", StringComparison.Ordinal))
            return false;

        return compact.Contains("infraccionesdeintegridad", StringComparison.Ordinal) ||
               compact.Contains("foundintegrityviolations", StringComparison.Ordinal) ||
               compact.Contains("foundcorruptfiles", StringComparison.Ordinal) ||
               compact.Contains("encontroarchivosdanados", StringComparison.Ordinal);
    }

    private static string Compact(string value)
    {
        string decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        string withoutMarks = Regex.Replace(decomposed, @"\p{Mn}", "");
        return Regex.Replace(withoutMarks, @"[^\p{L}\p{N}]", "").ToLowerInvariant();
    }

    private static string GetSystemDrive()
    {
        try
        {
            string? root = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrWhiteSpace(root))
                return root.TrimEnd('\\');
        }
        catch { }
        return "C:";
    }

    private static string ExtractFoundFolder(string text, string drive)
    {
        var match = Regex.Match(text, @"(?i)(?:\\|/)(found\.\d+)");
        return match.Success ? drive + "\\" + match.Groups[1].Value : string.Empty;
    }

    private static string ExtractDumpPath(string text)
    {
        var match = Regex.Match(text, @"(?i)[A-Z]:\\Windows\\Minidump\\[^\s""']+\.dmp");
        return match.Success ? match.Value.TrimEnd('.', ',', ';') : string.Empty;
    }

    private static string BuildEvidence(string details, string fallback, IReadOnlyList<string> preferred)
    {
        var found = preferred.Where(x => details.Contains(x, StringComparison.OrdinalIgnoreCase)).ToList();
        return found.Count == 0 ? fallback + " " + string.Join(" ", preferred) : string.Join(" ", found);
    }
}
