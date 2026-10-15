# SWIFT Hourly Worker .NET 10 — Outlook clásico (sin permisos Graph)

Lee `[swift-connector].[SwiftMessages]` en Azure SQL con identidad corporativa y envía un reporte horario mediante **Outlook clásico de escritorio**, usando la cuenta ya agregada al perfil de Windows. No solicita permiso Microsoft Graph `Mail.Send` ni administra tokens del correo. **No es compatible con Nuevo Outlook, Docker ni servicio de Windows no interactivo.**

## Requisitos

- Windows 10/11, .NET SDK 10, **Outlook clásico instalado, abierto y autenticado** con cuenta remitente.
- Ejecutar con el mismo usuario que inició Outlook, en sesión interactiva (no SYSTEM ni servicio Session 0).
- Permiso SELECT sobre Azure SQL, conectividad a SQL y autenticación `Active Directory Default` (puede requerir `az login` con Azure CLI).
- La política corporativa de Outlook debe permitir el envío mediante automatización COM. Algunas configuraciones muestran alertas de seguridad o bloquean esta operación.

## Prueba sin enviar correo

Abrir PowerShell con tu sesión de Windows:

```powershell
$env:Report__Recipients__0 = 'christianye@iadb.org'
$env:Report__SenderUserId = 'christianye@iadb.org'
$env:Report__RunOnStartup = 'true'
$env:Report__DryRun = 'true'
$env:Report__StateFile = 'state/checkpoint-test.json'
az login
dotnet restore .\src\SwiftHourlyWorker\SwiftHourlyWorker.csproj
dotnet build .\src\SwiftHourlyWorker\SwiftHourlyWorker.csproj -c Release
dotnet run --project .\src\SwiftHourlyWorker\SwiftHourlyWorker.csproj
```

## Enviar con Outlook clásico

Verifica primero que `Outlook (clásico)` esté abierto con la cuenta de envío y que puedes enviar manualmente. Configura:

```powershell
$env:Report__DryRun = 'false'
$env:Report__StateFile = 'state/checkpoint-live.json'
dotnet run --project .\src\SwiftHourlyWorker\SwiftHourlyWorker.csproj
```

**Advertencia**: el correo se entrega a la bandeja de salida de Outlook. Puede quedarse en cola si Outlook está desconectado; esto no garantiza la entrega final. Un fallo entre envío y checkpoint puede duplicar el correo.

## Ejecución por hora

- `Report__RunOnStartup=true`: procesa la última hora completa al arrancar; posteriormente cada cambio de hora UTC.
- `false`: comienza en el siguiente cambio de hora.
- Recupera hasta `MaxCatchUpHours` ventanas desde el checkpoint local. Un solo proceso puede compartir ese estado.
- Para automatizar en Windows, usar **Programador de tareas** con opción `Ejecutar solo cuando el usuario haya iniciado sesión`, iniciando el Worker al iniciar sesión (el Worker ya gestiona su propio temporizador). Evitar varios procesos simultáneos.
- `SqlTimeZoneId=UTC` es una suposición: validar con timestamps de producción; asegurar interpretación horaria para evitar ventanas incorrectas.
- La identidad SQL **también** requiere permisos: el uso de Outlook no resuelve permisos SQL.
- Requiere validar códigos `Status` y `ResponseStatus`; se clasifican como errores/pendientes potenciales.
- El detalle del correo omite columnas sensibles (como `AccountNumber`) y limita a 100 filas por defecto; la consulta puede leer 5.000.

## Restricciones de arquitectura

Outlook COM es una solución **de escritorio**, no un servicio desatendido confiable: no operar en Windows Service Session 0, Linux, contenedores o Azure Container Apps. Nuevo Outlook no ofrece soporte COM. Si se requiere operación de servidor 24/7 sin sesión activa, conservar el conector **Office 365 Outlook de Power Automate** u obtener un método de envío autorizado por TI (por ejemplo SMTP relay corporativo).

**No ejecutado**: compilación ni pruebas de integración reales; este entorno no tiene .NET SDK ni la sesión de Outlook.
