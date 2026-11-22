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
                if (text.Contains("740", StringComparison.OrdinalIgnoreCase))
                    facts.Add("La comprobación requiere permisos de administrador y no pudo iniciarse.");
                else if (Contains(text, "no se detectó daño", "no component store corruption detected"))
                    facts.Add("DISM no informó daños en el almacén de componentes.");
                else if (Contains(text, "se puede reparar", "component store is repairable"))
                    facts.Add("DISM encontró daños que pueden requerir reparación.");
                else if (Contains(text, "no se puede reparar", "component store cannot be repaired"))
                    facts.Add("DISM informó daños que no puede reparar automáticamente.");
                else facts.Add("El resultado no contiene una conclusión reconocible. Consultá la evidencia técnica.");
                recommendation = "Si el resultado no es concluyente, ejecutá el análisis como administrador. No se inició ninguna reparación.";
                break;

            case "sfc":
                headline = "Integridad de archivos protegidos de Windows";
                explanation = "SFC VerifyOnly comprueba archivos protegidos del sistema sin intentar repararlos.";
                if (Contains(text, "no se encontraron infracciones", "did not find any integrity violations"))
                    facts.Add("Windows no informó infracciones de integridad.");
                else if (Contains(text, "encontró archivos dañados", "found integrity violations"))
                    facts.Add("Windows informó archivos protegidos con problemas de integridad.");
                else if (text.Contains("La consulta terminó sin texto", StringComparison.OrdinalIgnoreCase))
                    facts.Add("SFC no devolvió un resultado legible. La comprobación no puede considerarse aprobada.");
                else facts.Add("No se obtuvo una conclusión reconocible del verificador.");
                recommendation = "Si SFC no informa un resultado explícito, repetí la prueba como administrador y revisá el registro CBS de Windows. No se repararon archivos.";
                break;

            case "chkdsk":
                headline = "Sistema de archivos de la unidad del sistema";
                explanation = "CHKDSK /scan solicita una exploración en línea de NTFS. No se utilizan opciones de reparación.";
                if (Contains(text, "no encontró problemas", "found no problems"))
                    facts.Add("La exploración no informó problemas en el sistema de archivos.");
                else if (Contains(text, "found problems", "se encontraron problemas"))
                    facts.Add("La exploración informó problemas que requieren revisión.");
                else facts.Add("No se obtuvo una conclusión reconocible. Consultá la evidencia técnica.");
                recommendation = "Si se detectan problemas, guardá una copia de seguridad y evaluá los pasos de mantenimiento por separado. Esta aplicación no corrige errores.";
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
        var match = Regex.Match(text, @"(?im)^\s*" + Regex.Escape(key) + @"\s*:\s*(.*?)\s*$");
        return match.Success ? match.Groups[1].Value : null;
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
