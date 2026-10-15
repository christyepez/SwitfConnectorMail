# Crear ejecutable Windows x64 — SwiftHourlyWorker .NET 10

Este paquete incluye el **código fuente** y las herramientas para publicar un EXE autónomo de Windows. El EXE aún no está compilado.

## Requisitos para compilar (solo una vez)
- Windows x64 con SDK .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0
- Acceso a NuGet para restaurar los paquetes.

## Generar el EXE
1. Descomprime este ZIP.
2. Abre `SwiftHourlyWorker\build\CREAR_EJECUTABLE.cmd`.
3. Al completar, abre `SwiftHourlyWorker\publicado\win-x64\SwiftHourlyWorker.exe` (también estarán los archivos de configuración y el script de Outlook).
4. No muevas solo el EXE: conserva `appsettings.json` y la carpeta `scripts` junto a él.

## Prueba sin correo
Inicia Outlook clásico y autentícate con tu cuenta corporativa. Verifica acceso a Azure SQL con tu identidad Microsoft Entra. Ejecuta `build\INICIAR_PRUEBA.cmd`. El modo de prueba NO envía mensajes, pero necesita acceso a SQL.

## Envío real
Solo después de probar y validar la ventana horaria, ejecuta `build\INICIAR_ENVIO.cmd`. El primer correo será el de la siguiente hora completa; los posteriores, cada hora mientras permanezca ejecutándose. El mecanismo usa automatización COM de Outlook clásico, en sesión Windows interactiva. No es compatible con el nuevo Outlook, servicio Session 0 o Docker.

## Seguridad y limitaciones
- Configura destinatarios y cuenta autorizada antes del envío.
- No permite envíos sin permisos corporativos de Outlook; usa el perfil Outlook ya configurado.
- El checkpoint queda en el directorio de ejecución; evita ejecutar varias instancias.
- No hay garantía de entrega final cuando Outlook acepta un correo en la bandeja de salida.
- El paquete no ha sido compilado ni validado contra SQL/Outlook reales desde este entorno.
