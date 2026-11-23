# Diagnóstico PC 3.1.2

Aplicación de escritorio portable para Windows 11 x64, en español. Reúne más de 20 comprobaciones locales para revisar hardware, almacenamiento, Windows, seguridad, red, rendimiento, controladores y mecanismos habituales de inicio automático.

## Qué aporta esta versión

- **Plan de acción numerado y priorizado:** las acciones se ordenan por gravedad y por dependencias técnicas. Cada recomendación explica qué detectó el programa, por qué importa, qué pasos seguir, qué comando se puede ejecutar manualmente, las precauciones y las referencias técnicas.
- **Informe reorganizado:** primero muestra el plan de acción, después una lista única y compacta de resultados y, al final, la evidencia técnica original en un apéndice separado.
- **Referencias oficiales:** enlaces a documentación de Microsoft, AMD, GIGABYTE, AnyDesk y Monect según el hallazgo. Las referencias están incluidas en el programa; no se consulta Internet para generar el diagnóstico ni para construir el plan. Los enlaces se abren solo cuando el usuario los selecciona.
- **Muestreo de eventos por categoría:** separa ESENT, errores de aplicaciones, NTFS, almacenamiento, WHEA y apagados/bugchecks; filtra proveedores antes de calcular saturación y limita por separado las búsquedas generales. Un origen de alto volumen no oculta fallos de aplicaciones y no se informa saturación en categorías con cero coincidencias.
- **Servicios de acceso remoto adicionales:** identifica `MonectServerService` junto con AnyDesk y explica que un servicio o puerto local no prueba malware ni exposición a Internet.
- **Entradas de inicio más precisas:** enumera el nombre, origen y ruta inexistente de cada entrada afectada, y contrasta coincidencias conocidas con el inventario de aplicaciones sin concluir que el programa completo esté desinstalado.
- **Estado parcial de SmartScreen:** cuando Windows no expone un valor verificable, el informe lo cuenta explícitamente como no verificado en lugar de dejarlo como dato neutro.
- **Interpretación mejorada de SFC:** el resultado de `SFC /verifyonly` contempla las salidas de consola con caracteres separados y distingue infracciones de integridad de un resultado limpio.
- **Anonimización más precisa:** evita ocultar letras de unidades y otros datos técnicos por confundirlos con direcciones IPv6.

## Diagnósticos incluidos

### Hardware y almacenamiento
- Windows, compilación, BIOS/UEFI, placa madre, CPU, RAM, módulos de memoria y zonas térmicas ACPI cuando el firmware las expone.
- GPU, controlador, pantallas y resolución.
- Unidades lógicas, espacio libre, discos físicos, estado de salud y contadores SMART/fiabilidad cuando el dispositivo y el controlador los publican.
- Dispositivos con códigos de error del Administrador de dispositivos.
- Inventario PnP y estado de firma digital de controladores.

### Rendimiento
- Captura de CPU y RAM.
- Muestra temporal de tres segundos para medir CPU total y consumo relativo por proceso, además de procesos con mayor uso de memoria.
- Programas configurados para iniciar con Windows.
- Verificación local de firma Authenticode y cálculo SHA-256 de ejecutables asociados a entradas de inicio.
- Tareas programadas de terceros activas, con revisión de comandos codificados y rutas temporales.
- Inventario de programas instalados desde el registro de Windows.

### Seguridad
- Microsoft Defender: estado de protecciones, firmas, fechas de análisis e historial de detecciones.
- Exclusiones configuradas en Defender, con atención a rutas excesivamente amplias.
- Firewall de Windows, TPM, Secure Boot, BitLocker (sin consultar claves de recuperación), UAC, SmartScreen y VBS cuando Windows permite consultarlos.
- Puertos TCP locales en escucha asociados a procesos; la escucha local no demuestra exposición a Internet.

### Red
- Ethernet, Wi-Fi, dirección IP, puerta de enlace, DNS, proxy y servicio WLAN.
- Resolución DNS, ping de referencia y conexión TCP 443 de prueba a microsoft.com.
- La prueba ICMP se interpreta con cautela porque algunos routers y proveedores bloquean ping.

### Windows y estabilidad
- Servicios relevantes, actualizaciones instaladas y reinicio pendiente.
- Eventos System/Application de los últimos siete días.
- Patrones de cierres de aplicaciones, reinicios inesperados y eventos de almacenamiento/hardware durante los últimos 30 días.
- Plan de energía, solicitudes que impiden la suspensión y último motivo de reactivación.
- DISM ScanHealth y SFC VerifyOnly, disponibles al aceptar voluntariamente la elevación.
- CHKDSK sin parámetros: consulta de estado del volumen, sin opciones de reparación.

## Plan de acción y referencias

El motor de recomendaciones aplica reglas locales a los resultados recopilados. Según la evidencia, puede sugerir acciones para:
- corrupción NTFS, eventos NTFS 98 y archivos recuperados en carpetas `found.*`;
- daños reparables del almacén de componentes DISM y archivos protegidos detectados por SFC;
- pantallas azules VIDEO_SCHEDULER_INTERNAL_ERROR y análisis del minidump;
- eventos ESENT 474 repetidos;
- controladores faltantes de dispositivos Intel de la placa madre;
- reinicios pendientes;
- revisión preventiva de Secure Boot, BitLocker y SmartScreen;
- herramientas de acceso remoto, controladores sin firma confirmada, exclusiones de Defender y entradas de inicio antiguas.

**Las sugerencias no son diagnósticos infalibles.** Si Windows no aporta evidencia suficiente, el programa lo debe indicar como no verificado. Una aplicación sin firma, un puerto en escucha o un dispositivo deshabilitado no se clasifican automáticamente como malware o avería.

Las recomendaciones distinguen acciones correctivas de mejoras preventivas. Los comandos que pueden modificar Windows o el sistema de archivos se muestran como texto para ejecución manual y se acompañan de advertencias. La aplicación no los ejecuta.

## Solo lectura, privacidad y límites

La aplicación está diseñada para diagnosticar y explicar. No limpia, optimiza ni repara automáticamente; no instala ni desinstala programas; no borra, mueve ni modifica archivos personales; no cambia registro, servicios, controladores, DNS, Wi-Fi, firewall, políticas, tareas programadas ni planes de energía.

El botón de administrador solicita elevación solo cuando el usuario lo pulsa. Sin permisos, las pruebas que los requieren se marcan como **No verificadas**.

No consulta contraseñas Wi-Fi, claves de BitLocker ni claves privadas. No sube informes, archivos ni hashes a servicios externos. Los hashes SHA-256 se calculan localmente. No incluye telemetría, cuenta, licencia ni actualización automática.

Las únicas pruebas que generan tráfico de red son las consultas de conectividad explícitamente descritas (DNS, ICMP a 1.1.1.1 y TCP 443 a microsoft.com). Las referencias de las recomendaciones no se descargan en segundo plano.

La exportación HTML/TXT es manual, mediante una ruta elegida por el usuario. El anonimizado está activado por defecto y oculta direcciones IP, MAC, SID, usuario, identificadores de equipo y SSID en el informe, preservando letras de unidades y valores técnicos que no sean datos personales.

## Interpretación y limitaciones

- **Correcto:** la consulta concreta devolvió evidencia compatible con un resultado correcto.
- **Observación / Crítico:** se identificó una señal que conviene revisar. No equivale a un diagnóstico infalible.
- **Informativo:** dato para contexto o revisión manual; no es una detección de amenaza.
- **No verificado:** faltan permisos, soporte del controlador, datos del firmware o una respuesta concluyente.
- El estado de Defender y su historial no garantizan ausencia total de malware. La aplicación no inicia un examen antivirus completo ni pone archivos en cuarentena.
- Las temperaturas, desgaste y SMART dependen de los sensores y controladores que el equipo exponga. La ausencia de esos datos no confirma ni descarta una falla física.
- La muestra de CPU dura tres segundos; no detecta necesariamente picos esporádicos.
- DISM ScanHealth y SFC VerifyOnly verifican, no reparan. CHKDSK sin parámetros muestra el estado del volumen; no realiza una reparación ni equivale a una comprobación offline exhaustiva.

## Ejecución

Descargá `DiagnosticoPC.exe` desde **Releases**. Es un ejecutable autocontenido para Windows x64 y no requiere instalación. Para DISM, SFC, CHKDSK y algunas consultas de seguridad, pulsá **Ejecutar como administrador** y aceptá UAC.

## Compilación

La publicación oficial se valida mediante GitHub Actions en un entorno Windows x64 antes de generar el ejecutable portable.

```powershell
dotnet publish DiagnosticoPC.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
