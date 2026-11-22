# Diagnóstico PC 2.0

Aplicación de escritorio para Windows 11 x64. Ejecuta diagnósticos locales de hardware, almacenamiento, seguridad, conectividad, rendimiento y estabilidad de Windows.

## Características

- Interfaz en español con secciones independientes, progreso, cancelación y resumen de hallazgos.
- Inventario de Windows, equipo, BIOS/UEFI, placa madre, CPU, RAM, GPU y monitores.
- Procesos ordenados por memoria y tiempo de CPU acumulado, con aclaración de que este último no es consumo instantáneo.
- Aplicaciones configuradas para iniciarse con Windows: solo lectura, sin deshabilitarlas.
- Volúmenes, espacio libre, discos físicos, estado que expone Storage y contadores de fiabilidad/SMART cuando están disponibles.
- Dispositivos con códigos de error del Administrador de dispositivos, con explicación de códigos habituales.
- Microsoft Defender: estado de protección, firmas, fechas de análisis y detecciones registradas que expone el producto.
- Firewall de Windows, TPM, Secure Boot, BitLocker (sin consultar claves de recuperación), UAC, SmartScreen y VBS cuando Windows permite consultarlos.
- Ethernet, Wi-Fi, configuración IP, DNS, proxy, conectividad ICMP/HTTPS y puertos locales en escucha.
- Servicios relevantes, registros de eventos System/Application de los últimos siete días, actualizaciones instaladas y reinicio pendiente.
- DISM ScanHealth, SFC VerifyOnly y CHKDSK /scan, disponibles únicamente al ejecutar voluntariamente como administrador.
- Exportación HTML imprimible y TXT. El anonimizado de datos está activado por defecto al exportar.

## Seguridad y privacidad

La aplicación está diseñada para consultar el estado del equipo. No incluye funciones para limpiar, reparar, optimizar automáticamente, instalar/desinstalar, borrar, mover o modificar archivos personales; tampoco cambia registro, servicios, DNS, Wi-Fi, Firewall, controladores o políticas de Windows.

No inicia análisis antivirus ni pone archivos en cuarentena. Consulta el estado y el historial que Microsoft Defender expone. No obtiene contraseñas Wi-Fi, claves de BitLocker ni claves privadas. No transmite informes ni datos a servidores. Los informes solo se guardan cuando el usuario elige explícitamente una ruta de exportación.

El botón de administrador solicita elevación de forma explícita mediante UAC. No eleva privilegios automáticamente. Si se ejecuta sin privilegios, las pruebas que requieren administrador se marcan como **No verificadas**, no como fallas.

## Interpretación y limitaciones

- **Correcto** significa que esa prueba concreta devolvió evidencia compatible con un resultado correcto; no es una certificación global del equipo.
- **Advertencia** o **Crítico** se asignan según señales identificadas por la consulta, no mediante una puntuación arbitraria.
- **No verificado** indica falta de permisos, timeout, soporte del controlador o datos del firmware.
- La lectura de temperaturas, desgaste y SMART depende de que el firmware, dispositivo y controlador expongan esos valores. La ausencia de un contador no demuestra que un disco esté sano ni averiado.
- Los eventos aislados de DCOM no se consideran automáticamente errores críticos. Los eventos se agrupan para reducir duplicados.
- Un ping sin respuesta puede deberse a filtrado ICMP; se contrasta con DNS y conectividad HTTPS.
- El estado de Defender no garantiza ausencia total de malware y no sustituye un examen completo o sin conexión.
- DISM ScanHealth, SFC VerifyOnly y CHKDSK /scan son comprobaciones; la aplicación no ejecuta comandos de reparación.

## Ejecución

Descargar DiagnosticoPC.exe desde la sección Releases y ejecutarlo. Es portable y no requiere instalación. Para completar DISM, SFC, CHKDSK y algunas consultas de seguridad, usar **Ejecutar como administrador** y aceptar el aviso de Windows.

## Compilación

El workflow de GitHub Actions compila con .NET 10 LTS y publica un ejecutable autocontenido para win-x64:

dotnet publish DiagnosticoPC.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

El ejecutable se genera en bin/Release/net10.0-windows/win-x64/publish/DiagnosticoPC.exe.
