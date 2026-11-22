using System.Text.RegularExpressions;

namespace DiagnosticoPC;

public sealed record FindingNarrative(
    string Headline,
    string Explanation,
    string Recommendation,
    IReadOnlyList<string> Facts);

public static class DiagnosticNarrator
{
    public static FindingNarrative Create(DiagnosticEntry entry)
    {
        string text = entry.Details ?? string.Empty;
        var facts = new List<string>();
        string headline;
        string explanation;
        string recommendation;

        switch (entry.Id)
        {
            case "system":
                headline = "Identificación del equipo recopilada";
                explanation = "Se consultó Windows, el procesador, la placa madre, el firmware y la memoria instalada.";
                Add(text, facts, "Edición", "Edición");
                Add(text, facts, "Versión de Windows", "Versión");
                Add(text, facts, "Compilación", "Compilación");
                Add(text, facts, "Procesador", "Name");
                Add(text, facts, "Placa madre", "Product");
                Add(text, facts, "Memoria instalada", "RAM instalada (GB)");
                Add(text, facts, "Memoria disponible", "RAM disponible (GB)");
                recommendation = "Estos datos son el inventario de referencia. No indican por sí solos que exista una falla.";
                break;

            case "gpu":
                headline = "Tarjeta gráfica detectada";
                explanation = "Se consultó el adaptador gráfico, su controlador, el modo de pantalla y el estado que informa Windows.";
                Add(text, facts, "GPU", "Name");
                Add(text, facts, "Controlador", "DriverVersion");
                Add(text, facts, "Fecha del controlador", "DriverDate");
                Add(text, facts, "Resolución", "VideoModeDescription");
                Add(text, facts, "Estado informado", "Status");
                if (text.Contains("Status : OK", StringComparison.OrdinalIgnoreCase))
                    recommendation = "Windows informa que el adaptador está funcionando. Esto no sustituye una prueba de carga o temperatura de la GPU.";
                else
                    recommendation = "Revisá el detalle técnico y el Administrador de dispositivos si el estado no figura como correcto.";
                break;

            case "performance":
                headline = "Consumo de recursos del equipo";
                explanation = "La memoria y la carga de CPU son una captura del momento del análisis. El tiempo de CPU por proceso es acumulado desde que se inició ese proceso.";
                Add(text, facts, "Uso de CPU", "Uso de CPU informado por Windows (%)");
                Add(text, facts, "RAM utilizada", "RAM utilizada aproximada (%)");
                Add(text, facts, "RAM disponible", "RAM disponible (GB)");
                Add(text, facts, "RAM instalada", "RAM instalada (GB)");
                recommendation = "Un valor aislado no demuestra una pérdida de rendimiento. Conviene comparar la carga en reposo y durante el uso habitual.";
                break;

            case "storage":
                headline = "Capacidad y estado de las unidades";
                explanation = "Se revisaron el espacio libre, las unidades físicas y los contadores que el dispositivo y su controlador ponen a disposición de Windows.";
                foreach (Match m in Regex.Matches(text, @"(?im)^\s*([A-Z]:)\s*\|\s*(.*?)\s*\|\s*([A-Z0-9]+)\s*\|\s*Total:\s*([\d,.]+)\s*GB\s*\|\s*Libre:\s*([\d,.]+)\s*GB\s*\(([\d,.]+)\s*%\)"))
                {
                    string drive = m.Groups[1].Value;
                    string name = string.IsNullOrWhiteSpace(m.Groups[2].Value) ? "Sin etiqueta" : m.Groups[2].Value;
                    string fs = m.Groups[3].Value;
                    string total = m.Groups[4].Value;
                    string free = m.Groups[5].Value;
                    string pct = m.Groups[6].Value;
                    facts.Add($"{drive} ({name}): {free} GB libres de {total} GB ({pct} %), {fs}.");
                }
                var physical = Regex.Matches(text, @"(?im)^\s*(.+?)\s+(Unspecified|SSD|HDD|SCM)\s+(SATA|NVMe|USB|SAS|RAID)\s+(Healthy|Warning|Unhealthy|Unknown)\s+(.+?)\s+([\d,.]+)\s*$");
                foreach (Match m in physical)
                    facts.Add($"Unidad física: {m.Groups[1].Value.Trim()} — salud informada: {m.Groups[4].Value}; conexión {m.Groups[3].Value}.");
                if (text.Contains("El controlador no expone", StringComparison.OrdinalIgnoreCase))
                    facts.Add("El controlador no expone los indicadores SMART consultados.");
                recommendation = "Una unidad con poco espacio puede afectar actualizaciones y funcionamiento. La falta de datos SMART no significa que el disco esté sano ni averiado.";
                break;

            case "devices":
                headline = "Estado de los dispositivos y controladores";
                explanation = "Se consultaron los códigos de estado del Administrador de dispositivos. Algunos estados pueden ser intencionales, por ejemplo, un dispositivo deshabilitado.";
                AddMarkedLines(text, facts);
                if (facts.Count == 0)
                    facts.Add(text.Contains("[OK]", StringComparison.OrdinalIgnoreCase)
                        ? "Windows no informó dispositivos con códigos de error."
                        : "No se obtuvo un resultado concluyente del inventario de dispositivos.");
                recommendation = "Revisá los nombres y códigos concretos antes de tomar medidas. No se habilitó, reinstaló ni modificó ningún dispositivo.";
                break;

            case "defender":
                headline = "Estado de Microsoft Defender";
                explanation = "Se consultaron las protecciones activas, las firmas y el historial de detecciones que Defender expone. Esta consulta no inicia un examen antivirus.";
                Add(text, facts, "Antivirus habilitado", "Antivirus habilitado");
                Add(text, facts, "Protección en tiempo real", "Protección en tiempo real");
                Add(text, facts, "Protección contra alteraciones", "Protección contra alteraciones");
                Add(text, facts, "Versión de firmas", "Versión de firmas");
                Add(text, facts, "Última actualización de firmas", "Última actualización de firmas");
                Add(text, facts, "Antigüedad del análisis rápido", "Antigüedad del análisis rápido (días)");
                Add(text, facts, "Antigüedad del análisis completo", "Antigüedad del análisis completo (días)");
                Add(text, facts, "Registros de detecciones", "Registros de detecciones");
                if (text.Contains("No hay detecciones registradas", StringComparison.OrdinalIgnoreCase))
                    facts.Add("No hay detecciones registradas en el historial consultado.");
                recommendation = "Mantené las firmas actualizadas y ejecutá periódicamente un análisis completo desde Seguridad de Windows. Un historial vacío no garantiza ausencia de malware.";
                break;

            case "firewall":
                headline = "Perfiles del Firewall de Windows";
                explanation = "Se consultó si están habilitados los perfiles de red de dominio, privada y pública.";
                foreach (string line in text.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (Regex.IsMatch(trimmed, @"^(Domain|Private|Public)\s+(True|False)\b", RegexOptions.IgnoreCase))
                    {
                        string[] parts = Regex.Split(trimmed, @"\s+");
                        if (parts.Length > 1)
                            facts.Add($"{TranslateProfile(parts[0])}: {(parts[1].Equals("True", StringComparison.OrdinalIgnoreCase) ? "habilitado" : "deshabilitado")}.");
                    }
                }
                if (facts.Count == 0) facts.Add(text.Contains("[OK]") ? "Los perfiles consultados aparecen habilitados." : "No se pudo obtener el estado de los perfiles.");
                recommendation = "Es importante mantener habilitado el firewall en los perfiles que se utilizan. No se cambiaron reglas ni perfiles.";
                break;

            case "security-posture":
                headline = "Configuración de seguridad de Windows";
                explanation = "Se consultaron funciones de seguridad del firmware y de Windows. La disponibilidad depende de la configuración del equipo y de los permisos.";
                Add(text, facts, "Secure Boot", "Secure Boot habilitado");
                Add(text, facts, "TPM presente", "TpmPresent");
                Add(text, facts, "TPM preparado", "TpmReady");
                Add(text, facts, "TPM habilitado", "TpmEnabled");
                Add(text, facts, "UAC", "UAC EnableLUA");
                Add(text, facts, "SmartScreen", "SmartScreen");
                Add(text, facts, "VBS", "VirtualizationBasedSecurityStatus");
                Add(text, facts, "Servicios de seguridad activos", "SecurityServicesRunning");
                foreach (Match m in Regex.Matches(text, @"(?im)^\s*([A-Z]:)\s+(OperatingSystem|Data)\s+(FullyDecrypted|FullyEncrypted|EncryptionInProgress)\s+(On|Off)\s+([\d.]+)\s+([A-Za-z0-9]+)"))
                    facts.Add($"{m.Groups[1].Value}: cifrado {m.Groups[3].Value}; protección {m.Groups[4].Value}; método {m.Groups[6].Value}.");
                if (!Regex.IsMatch(text, @"(?im)^\s*SmartScreen[^:]*:\s*[^\r\n]+"))
                    facts.Add("SmartScreen: el estado no pudo determinarse con la fuente consultada; no se considera desactivado.");
                if (text.Contains("[NO VERIFICADO]", StringComparison.OrdinalIgnoreCase))
                    facts.Add("Una o más comprobaciones no pudieron consultarse. Esto no significa que la protección esté desactivada.");
                recommendation = "Revisá las funciones marcadas como desactivadas o no verificadas. No se modificó firmware, cifrado ni políticas de seguridad.";
                break;

            case "network":
                headline = "Conectividad y configuración de red";
                explanation = "Se comprobaron los adaptadores, la configuración IP, DNS y una conexión HTTPS de prueba. Un ping fallido por sí solo no demuestra que Internet esté caído.";
                Add(text, facts, "Interfaz", "Interfaz");
                Add(text, facts, "Dirección IPv4", "IPv4");
                Add(text, facts, "Puerta de enlace", "Puerta de enlace");
                Add(text, facts, "DNS configurados", "DNS");
                Add(text, facts, "Servicio Wi-Fi", "Servicio WLAN");
                if (text.Contains("[OK] La resolución DNS respondió.", StringComparison.OrdinalIgnoreCase))
                    facts.Add("La resolución DNS respondió correctamente.");
                var https = Regex.Match(text, @"Conexión TCP 443 a microsoft\.com:\s*(True|False)", RegexOptions.IgnoreCase);
                if (https.Success) facts.Add("Conexión HTTPS de prueba: " + (https.Groups[1].Value.Equals("True", StringComparison.OrdinalIgnoreCase) ? "correcta" : "sin respuesta") + ".");
                if (text.Contains("El servicio WLAN está detenido", StringComparison.OrdinalIgnoreCase))
                    facts.Add("El servicio Wi-Fi está detenido. Si usás Ethernet y no necesitás Wi-Fi, puede ser intencional.");
                recommendation = "Si la navegación funciona, no cambies DNS ni adaptadores por una prueba ICMP aislada. Revisá los detalles solo si experimentás cortes.";
                break;

            case "startup":
                headline = "Aplicaciones configuradas para iniciar automáticamente";
                explanation = "Se listaron las aplicaciones de inicio. La presencia de una entrada no demuestra que sea maliciosa ni que esté causando lentitud.";
                Add(text, facts, "Entradas detectadas", "Entradas de inicio detectadas");
                AddMarkedLines(text, facts);
                if (text.Contains("[INFO] Hay", StringComparison.OrdinalIgnoreCase))
                    facts.Add("Se encontraron herramientas de acceso remoto configuradas para iniciar. Confirmá que reconocés esas aplicaciones.");
                recommendation = "Deshabilitá únicamente las aplicaciones que reconozcas y no necesites al iniciar Windows, desde Configuración o el Administrador de tareas. La aplicación no deshabilita nada.";
                break;

            case "services":
                headline = "Servicios relevantes de Windows";
                explanation = "Se consultó el estado de servicios relacionados con seguridad, red, actualizaciones y administración del sistema.";
                int running = Regex.Matches(text, @"Estado:\s*Running", RegexOptions.IgnoreCase).Count;
                int stopped = Regex.Matches(text, @"Estado:\s*Stopped", RegexOptions.IgnoreCase).Count;
                facts.Add($"{running} servicio(s) en ejecución y {stopped} detenido(s) en la lista consultada.");
                facts.Add("Algunos servicios se inician bajo demanda; estar detenidos no implica necesariamente un problema.");
                recommendation = "No cambies el estado de un servicio basándote únicamente en que figure detenido.";
                break;

            case "events":
                headline = "Eventos de errores y estabilidad de Windows";
                explanation = "Se agruparon eventos de Sistema y Aplicación de los últimos siete días para reducir repeticiones. Un evento aislado no equivale necesariamente a una falla activa.";
                int errors = Regex.Matches(text, @"\[(AVISO|CRITICO)\]", RegexOptions.IgnoreCase).Count;
                int informational = Regex.Matches(text, @"\[INFO\]", RegexOptions.IgnoreCase).Count;
                facts.Add($"{errors} grupo(s) marcados para revisar y {informational} grupo(s) informativos.");
                AddMarkedLines(text, facts);
                recommendation = "Prestá atención a eventos repetidos de disco, almacenamiento, WHEA, Kernel-Power o BugCheck. Los eventos DCOM 10010 suelen requerir contexto y no se consideran críticos por sí solos.";
                break;

            case "updates":
                headline = "Actualizaciones y reinicios pendientes";
                explanation = "Se consultó el historial de actualizaciones instaladas y los indicadores habituales de reinicio pendiente.";
                var hotfixes = Regex.Matches(text, @"\bKB\d{6,8}\b", RegexOptions.IgnoreCase).Select(x => x.Value).Distinct().Take(10);
                facts.AddRange(hotfixes.Select(x => "Actualización registrada: " + x));
                facts.Add(text.Contains("[AVISO]", StringComparison.OrdinalIgnoreCase)
                    ? "Windows informa un posible reinicio pendiente."
                    : "No se encontraron los indicadores habituales de reinicio pendiente.");
                recommendation = "Mantené Windows actualizado y reiniciá cuando haya actualizaciones que lo requieran.";
                break;

            case "dism":
                headline = "Integridad del almacén de componentes de Windows";
                explanation = "DISM ScanHealth examina el almacén de componentes de Windows. No ejecuta la reparación RestoreHealth.";
                if (text.Contains("No se ejecutó porque esta sesión", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("740", StringComparison.OrdinalIgnoreCase))
                    facts.Add("La comprobación no se ejecutó porque requiere permisos de administrador.");
                else if (Contains(text, "no se detectó daño", "no component store corruption detected"))
                    facts.Add("DISM no informó daños en el almacén de componentes.");
                else if (Contains(text, "componentes puede repararse", "se puede reparar", "component store is repairable"))
                    facts.Add("DISM encontró daños en el almacén de componentes que pueden repararse. La prueba no hizo ninguna reparación.");
                else if (Contains(text, "no se puede reparar", "component store cannot be repaired"))
                    facts.Add("DISM informó daños que no puede reparar automáticamente.");
                else facts.Add("El resultado no contiene una conclusión reconocible. Consultá la evidencia técnica.");
                recommendation = "Si el resultado no es concluyente, ejecutá el análisis como administrador. No se inició ninguna reparación.";
                break;

            case "sfc":
                headline = "Integridad de archivos protegidos de Windows";
                explanation = "SFC VerifyOnly comprueba archivos protegidos del sistema sin intentar repararlos.";
                if (text.Contains("No se ejecutó porque esta sesión", StringComparison.OrdinalIgnoreCase))
                    facts.Add("La comprobación no se ejecutó porque requiere permisos de administrador.");
                else if (Contains(text, "no se encontraron infracciones", "did not find any integrity violations"))
                    facts.Add("Windows no informó infracciones de integridad.");
                else if (Contains(text, "infracciones de integridad", "found integrity violations", "found corrupt files", "encontró archivos dañados"))
                    facts.Add("SFC detectó infracciones de integridad en archivos protegidos de Windows. VerifyOnly no los repara.");
                else if (text.Contains("La consulta terminó sin texto", StringComparison.OrdinalIgnoreCase))
                    facts.Add("SFC no devolvió un resultado legible. La comprobación no puede considerarse aprobada.");
                else facts.Add("No se obtuvo una conclusión reconocible del verificador.");
                recommendation = "Si SFC no informa un resultado explícito, repetí la prueba como administrador y revisá el registro CBS de Windows. No se repararon archivos.";
                break;

            case "chkdsk":
                headline = "Sistema de archivos de la unidad del sistema";
                explanation = "CHKDSK se ejecutó en modo de solo lectura. Consultó la estructura y los metadatos del sistema de archivos, pero no aplicó una reparación.";
                if (text.Contains("No se ejecutó porque esta sesión", StringComparison.OrdinalIgnoreCase))
                    facts.Add("La comprobación no se ejecutó porque requiere permisos de administrador.");
                else if (Contains(text, "no encontró problemas", "no encontro problemas", "found no problems"))
                    facts.Add("CHKDSK no informó inconsistencias en el sistema de archivos.");
                else if (Contains(text, "detectó problemas", "detecto problemas", "found problems", "bitmap del volumen es incorrecto", "atributo bitmap de la tabla maestra de archivos"))
                {
                    facts.Add("CHKDSK detectó inconsistencias en los metadatos NTFS. El resultado no debe considerarse correcto.");
                    if (Contains(text, "atributo bitmap", "bitmap de la tabla maestra de archivos", "mft"))
                        facts.Add("Se informó una inconsistencia en el bitmap de la tabla maestra de archivos (MFT).");
                    if (Contains(text, "bitmap del volumen es incorrecto", "volume bitmap is incorrect"))
                        facts.Add("Se informó que el bitmap del volumen es incorrecto.");
                    if (Contains(text, "archivos no indizados recuperados", "recovered in lost files", "found."))
                        facts.Add("La salida menciona archivos huérfanos asociados a la carpeta de recuperación found.*. No la borres; comprobá si existe y conservá cualquier archivo que necesites.");
                    facts.Add("Código de salida nativo de CHKDSK: " + (Regex.Match(text, @"(?im)^CHKDSK_EXIT_CODE=(-?\d+)").Groups[1].Value is string code && code.Length > 0 ? code : "no informado") + ".");
                }
                else facts.Add("No se obtuvo una conclusión reconocible. Consultá la evidencia técnica.");
                recommendation = "Antes de reparar, respaldá los archivos importantes. Después se puede evaluar una comprobación y reparación offline de NTFS por separado. Esta aplicación no ejecuta CHKDSK /F ni modifica el volumen.";
                break;

            case "performance-sample":
                headline = "Medición breve de actividad y consumo por proceso";
                explanation = "Se midió CPU durante tres segundos y se tomó una muestra de memoria. No sustituye una monitorización prolongada.";
                Add(text, facts, "CPU total", "CPU total informada durante la muestra");
                Add(text, facts, "RAM utilizada", "RAM utilizada durante la muestra");
                Add(text, facts, "RAM disponible", "RAM disponible");
                AddMarkedLines(text, facts);
                recommendation = "Compará la muestra con el uso habitual. Para detectar picos intermitentes conviene repetir el análisis mientras ocurre la lentitud.";
                break;

            case "driver-audit":
                headline = "Revisión de firma digital de controladores";
                explanation = "Se consultó el indicador de firma digital de los controladores PnP. Un controlador sin firma debe revisarse, pero no es prueba automática de malware.";
                Add(text, facts, "Controladores revisados", "Controladores PnP inventariados");
                Add(text, facts, "Sin firma confirmada", "Controladores sin firma digital confirmada");
                AddMarkedLines(text, facts);
                recommendation = "Verificá el origen y fabricante antes de actualizar o quitar un controlador. No se tocó ninguno.";
                break;

            case "startup-audit":
                headline = "Integridad básica de ejecutables de inicio";
                explanation = "Se consultaron firmas Authenticode y huellas SHA-256 de los ejecutables de inicio. Los hashes se calculan localmente y no se envían.";
                Add(text, facts, "Entradas analizadas", "Entradas de inicio analizadas");
                AddMarkedLines(text, facts);
                recommendation = "Revisá comandos codificados, rutas temporales o archivos inexistentes. Un ejecutable sin firma no equivale automáticamente a una amenaza.";
                break;

            case "scheduled-task-audit":
                headline = "Persistencia mediante tareas programadas";
                explanation = "Se revisaron tareas activas ajenas a la carpeta estándar de Windows y sus acciones. Los programas legítimos también crean tareas.";
                Add(text, facts, "Tareas detectadas", "Tareas de terceros habilitadas o activas");
                AddMarkedLines(text, facts);
                recommendation = "Confirmá que reconocés tareas que ejecutan scripts, comandos codificados o aplicaciones desde carpetas temporales. No se deshabilitó ninguna.";
                break;

            case "defender-exclusions":
                headline = "Exclusiones del antivirus";
                explanation = "Se consultaron rutas, procesos, extensiones y direcciones IP excluidas de Defender. Una exclusión amplia puede reducir la cobertura.";
                Add(text, facts, "Total de exclusiones", "Total de exclusiones explícitas consultadas");
                AddMarkedLines(text, facts);
                recommendation = "Mantené únicamente exclusiones necesarias e intencionales. No se modificó la configuración de Defender.";
                break;

            case "reliability":
                headline = "Patrones de cierres y fallos durante 30 días";
                explanation = "Se agruparon errores de aplicaciones, informes de Windows, apagados inesperados y eventos de almacenamiento/hardware. La recurrencia importa más que un evento aislado.";
                Add(text, facts, "Fallos de aplicaciones", "Fallos de aplicaciones / Windows Error Reporting");
                Add(text, facts, "Eventos del sistema", "Eventos de apagado inesperado / hardware / almacenamiento");
                AddMarkedLines(text, facts);
                recommendation = "Relacioná los eventos repetidos con la fecha y hora de los síntomas. Fallos WHEA o de disco repetidos merecen prioridad.";
                break;

            case "software-inventory":
                headline = "Inventario local de software";
                explanation = "Se listaron aplicaciones registradas por Windows. No se comparan versiones con catálogos de Internet ni se declara obsoleto un programa por su antigüedad.";
                Add(text, facts, "Programas inventariados", "Programas inventariados");
                recommendation = "Usá el inventario para identificar software que no reconocés o ya no necesitás. No se desinstaló ni actualizó nada.";
                break;

            case "power":
                headline = "Estado de energía y reactivación";
                explanation = "Se consultó el plan activo, las solicitudes que mantienen Windows despierto y el último motivo de reactivación.";
                AddMarkedLines(text, facts);
                recommendation = "Si el equipo no entra en suspensión o se despierta solo, revisá las solicitudes y el último motivo registrado antes de cambiar el plan.";
                break;

            default:
                headline = entry.Title;
                explanation = "Se ejecutó la comprobación correspondiente. Revisá el resultado y sus limitaciones.";
                recommendation = "Consultá la evidencia técnica si necesitás más detalle.";
                break;
        }

        if (entry.TimedOut)
        {
            headline = "La comprobación superó el tiempo límite";
            explanation = "La prueba se interrumpió antes de obtener una respuesta completa. No debe interpretarse como correcta ni como una falla confirmada.";
            recommendation = "Volvé a ejecutar esta comprobación cuando el equipo esté disponible y no cierres el programa durante la prueba.";
        }

        if (entry.Level == FindingLevel.NotVerified &&
            !facts.Any(x => x.Contains("No se obtuvo", StringComparison.OrdinalIgnoreCase) ||
                            x.Contains("requiere permisos", StringComparison.OrdinalIgnoreCase) ||
                            x.Contains("no pudo", StringComparison.OrdinalIgnoreCase)))
            facts.Add("Esta prueba no pudo verificarse en las condiciones actuales. No se cuenta como resultado correcto.");

        if (facts.Count == 0)
            facts.Add("No se extrajeron datos resumidos de esta consulta. La evidencia original sigue disponible en el detalle técnico.");

        return new FindingNarrative(headline, explanation, recommendation, facts);
    }

    private static void Add(string text, List<string> facts, string label, string key)
    {
        string? value = GetValue(text, key);
        if (!string.IsNullOrWhiteSpace(value))
            facts.Add(label + ": " + value.Trim().TrimEnd('.'));
    }

    private static string? GetValue(string text, string key)
    {
        var match = Regex.Match(text, @"(?im)^[ \t]*" + Regex.Escape(key) + @"[ \t]*:[ \t]*([^\r\n]*)");
        if (!match.Success) return null;
        string value = match.Groups[1].Value.Trim();
        if (value.Length == 0 || value.StartsWith("===", StringComparison.Ordinal)) return null;
        return value;
    }

    private static void AddMarkedLines(string text, List<string> facts)
    {
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("[AVISO]", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[CRITICO]", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[NO VERIFICADO]", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[INFO]", StringComparison.OrdinalIgnoreCase))
            {
                string readable = trimmed
                    .Replace("[CRITICO]", "Revisión prioritaria:", StringComparison.OrdinalIgnoreCase)
                    .Replace("[AVISO]", "Observación:", StringComparison.OrdinalIgnoreCase)
                    .Replace("[NO VERIFICADO]", "No se pudo comprobar:", StringComparison.OrdinalIgnoreCase)
                    .Replace("[INFO]", "Información:", StringComparison.OrdinalIgnoreCase);
                if (!facts.Contains(readable)) facts.Add(readable);
            }
        }
    }

    private static string TranslateProfile(string profile) => profile switch
    {
        "Domain" => "Dominio",
        "Private" => "Red privada",
        "Public" => "Red pública",
        _ => profile
    };

    private static bool Contains(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
}
