# DiagnosticoPC

Aplicación de escritorio portable para Windows 11 x64. Interfaz en español y diagnóstico de solo lectura.

## Qué revisa
- Versión y compilación de Windows, equipo, CPU, RAM, placa madre y GPU.
- Unidades de almacenamiento y espacio libre.
- Procesos con mayor consumo de CPU/RAM.
- Estado de Microsoft Defender y detecciones registradas disponibles.
- Perfiles del Firewall de Windows.
- Estado de Secure Boot cuando el firmware y la sesión permiten consultarlo.
- Adaptadores, configuración IP, DNS, estado Wi-Fi y conectividad básica.
- DISM CheckHealth y SFC VerifyOnly.

## Seguridad y límites
- No elimina, mueve ni modifica archivos personales.
- No cambia registro, servicios, DNS, Wi-Fi, Firewall ni configuraciones del sistema.
- No desinstala ni instala programas.
- No realiza reparaciones automáticas.
- No solicita ni muestra contraseñas Wi-Fi.
- No transmite informes ni datos a servidores.
- No eleva privilegios automáticamente.
- Las consultas de Defender muestran su estado y detecciones registradas; no sustituyen un análisis antimalware completo.
- Algunas consultas pueden devolver información limitada si Windows restringe el acceso o si Defender no está disponible.

## Compilar
El workflow de GitHub Actions genera un ejecutable portable para Windows x64 en cada push a `main`. El archivo se publica como artefacto `DiagnosticoPC-Windows-x64`.

También se puede compilar localmente con .NET 8 SDK:
```powershell
dotnet publish DiagnosticoPC.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

El ejecutable aparece en `bin/Release/net8.0-windows/win-x64/publish/DiagnosticoPC.exe`.
