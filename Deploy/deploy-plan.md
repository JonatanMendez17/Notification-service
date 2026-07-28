# Plan de deploy y actualización — Notification.Api / Notification.Engine

> Documento de diseño, todavía no implementado (2026-07-24). Sirve como referencia para cuando se decida encarar esto.

## Contexto

`Notification.Api` y `Notification.Engine` hoy solo se corren a mano (`dotnet run`) desde el dev machine. Hace falta:
1. Dejarlos corriendo como **servicios de Windows** en el server donde corre TGN (IIS + SQL Server 172.18.131.46).
2. Un mecanismo de **actualización** calcado del que ya usaba el usuario en otro trabajo: publicar desde Visual Studio → empaquetar como `.buf` (zip renombrado) → copiar al server → correr una herramienta que compara versión instalada vs. nueva y, al confirmar, para el servicio, reemplaza los archivos y lo reinicia.

`Notification.Engine` ya tiene el paquete `Microsoft.Extensions.Hosting.WindowsServices` y `AddWindowsService()` en `Program.cs` — ya está preparado para correr como servicio. `Notification.Api` (Kestrel puro) todavía no.

No existe (a la fecha de este documento) ninguna herramienta de actualización ni convención de despliegue en este repo — se construye de cero (confirmado con el usuario). Servicios corren en el **mismo server que TGN** (confirmado). Empaquetado será **automático al publicar desde Visual Studio** (confirmado) y `Notification.Api` escuchará en el **puerto 5080** (confirmado).

## Parte A — Preparar los dos proyectos para correr como servicio

**`Notification.Api`** (`Notification.Api.csproj`, `Program.cs`):
- Agregar `PackageReference Microsoft.Extensions.Hosting.WindowsServices` (misma versión que usa Engine: 8.0.1).
- En `Program.cs`: `builder.Host.UseWindowsService();` antes de `Build()`.
- Fijar el puerto Kestrel explícito en `appsettings.json`: `"Urls": "http://localhost:5080"` (corriendo como servicio no hay `launchSettings.json` ni consola que lo infiera).

**Ambos csproj**: agregar `<Version>1.0.0</Version>` explícito en el `PropertyGroup` (punto de partida; se bumpea a mano antes de cada publish/release). Este valor es la única fuente de verdad de versión — se lee luego vía `FileVersionInfo` tanto para nombrar el paquete como para que el Updater compare versiones, sin inventar un archivo de versión aparte.

**Instalación inicial (una sola vez, no forma parte del updater)**: script `Deploy/install-services.ps1` en la raíz del repo que, corrido en el server con permisos de administrador:
- Verifica que esté instalado el runtime/hosting de .NET 8 (`dotnet --list-runtimes`).
- Crea las rutas fijas de despliegue: `D:\APP\TGN\Notification-service\Deployed\Notification.Api\` y `...\Deployed\Notification.Engine\`.
- Crea los dos servicios de Windows con `sc.exe create` (o `New-Service`), arranque `Automatic`, apuntando a los `.exe` en esas rutas.
- Los deja arrancados (`Start-Service`).

Este script asume que la primera copia de archivos a esas rutas (incluido un `appsettings.json` con credenciales reales ya completadas) se hace a mano una única vez antes de correrlo — el Updater (Parte C) nunca hace instalaciones nuevas, solo actualiza servicios que ya existen y ya están corriendo.

## Parte B — Empaquetado versionado (.buf) automático al publicar

Se agrega un `Target` de MSBuild con `AfterTargets="Publish"` a `Notification.Api.csproj` y `Notification.Engine.csproj` (misma lógica en los dos, puede vivir en un `.targets` común importado por ambos, ej. `Deploy/Package.targets`, para no duplicar):

1. Copia `$(PublishDir)` a una carpeta de staging temporal.
2. Elimina de esa copia `appsettings.json` y `appsettings.Development.json` — **nunca deben viajar en el paquete**, así el Updater jamás tiene la tentación (ni la posibilidad) de pisar credenciales ya configuradas en el server.
3. Comprime la carpeta de staging a `.zip` (usando `System.IO.Compression` vía una `Task` inline de MSBuild, o invocando `powershell Compress-Archive` desde el target — más simple de mantener) y renombra el resultado a `.buf`.
4. Deja el archivo final en `Deploy\Packages\{NombreProyecto}_{Version}.buf` en la raíz del repo (ej. `Notification.Api_1.0.0.buf`).

Ese `.buf` es lo que se copia manualmente al server (RDP/carpeta compartida — igual que antes), a una carpeta fija `Deploy\Incoming\` en el server.

## Parte C — `Notification.Updater` (proyecto nuevo, WinForms)

Nuevo proyecto agregado a `Notification.sln`: `Notification.Updater` (`net8.0-windows`, `UseWindowsForms=true`, `app.manifest` con `requireAdministrator` — controlar servicios de Windows requiere elevación).

Config fija en el propio Updater (no hace falta que sea editable): tabla de 2 entradas, una por servicio —

| Clave (prefijo de archivo) | Nombre del servicio de Windows | Ruta de despliegue | Exe principal |
|---|---|---|---|
| `Notification.Api` | `NotificationApi` | `D:\APP\TGN\Notification-service\Deployed\Notification.Api\` | `Notification.Api.exe` |
| `Notification.Engine` | `NotificationEngine` | `D:\APP\TGN\Notification-service\Deployed\Notification.Engine\` | `Notification.Engine.exe` |

**Flujo de la pantalla principal (Form único, se abre y escanea):**
1. Lista `Deploy\Incoming\*.buf`, matchea cada archivo contra las claves conocidas por prefijo de nombre.
2. Para cada match: extrae el `.buf` (copiado como `.zip`) a una carpeta temp y lee `FileVersionInfo.GetVersionInfo(exeExtraido).FileVersion` (versión nueva) y `FileVersionInfo.GetVersionInfo(exeDesplegado).FileVersion` (versión instalada, si el servicio ya existe).
3. Muestra una grilla: `Servicio | Versión actual | Versión nueva | Estado | [Actualizar]`.

**Al presionar "Actualizar" (por fila):**
1. `ServiceController.Stop()` + `WaitForStatus(Stopped, timeout: 30s)`.
2. Backup: mueve la carpeta desplegada actual a `Deploy\Backups\{Servicio}\{versionVieja}_{timestamp}\` (sin límite de limpieza automática por ahora — son carpetas chicas, se puede podar a mano si hace falta).
3. Copia los archivos extraídos del paquete a la ruta de despliegue, **sin pisar `appsettings.json`/`appsettings.*.json` si ya existen ahí** (chequeo simple: si el destino existe, se salta ese archivo puntual; el resto se sobreescribe).
4. `ServiceController.Start()` + `WaitForStatus(Running, timeout: 30s)`.
5. Actualiza el estado en pantalla (OK / Error con motivo — ej. "no arrancó: revisar logs").
6. Mueve el `.buf` consumido a `Deploy\Processed\` (no se borra, sirve de historial).

Sin rollback automatizado: si algo sale mal, el backup del paso 2 permite restaurar a mano (parar servicio, restaurar carpeta, arrancar) — no se pidió automatizar esto.

## Archivos a tocar/crear (cuando se implemente)

- `Notification.Api/Notification.Api.csproj` — agregar paquete + `<Version>`.
- `Notification.Api/Program.cs` — `UseWindowsService()`.
- `Notification.Api/appsettings.json` — `"Urls": "http://localhost:5080"`.
- `Notification.Engine/Notification.Engine.csproj` — agregar `<Version>`.
- `Deploy/Package.targets` (nuevo) — importado desde ambos csproj, lógica de empaquetado post-publish.
- `Deploy/install-services.ps1` (nuevo) — instalación inicial one-shot.
- `Notification.Updater/` (proyecto nuevo completo) — WinForms, `Form1.cs` (grilla + lógica de update), `ServiceUpdateEntry.cs` (config fija de las 2 entradas), `app.manifest`.
- `Notification.sln` — agregar el proyecto nuevo.

## Verificación (cuando se implemente)

1. `dotnet build` de la solución completa (los 3 proyectos) sin errores.
2. Publicar `Notification.Api` y `Notification.Engine` en Release desde VS (o `dotnet publish`) y confirmar que aparece el `.buf` en `Deploy\Packages\` con el nombre y versión esperados.
3. En una máquina de prueba (o la misma, con cuidado): correr `install-services.ps1`, confirmar que los 2 servicios quedan `Running` (`Get-Service NotificationApi, NotificationEngine`), que el Api responde `GET http://localhost:5080/health`, y que el Engine escribe su log en `log\Notification.Engine\engine_*.txt`.
4. Bumpear `<Version>` a `1.0.1` en uno de los dos proyectos, publicar de nuevo (genera un segundo `.buf`), copiarlo a `Deploy\Incoming\`, correr `Notification.Updater.exe` como administrador y confirmar: detecta la versión nueva, para el servicio, hace backup, reemplaza archivos, reinicia, y que `appsettings.json` con las credenciales reales **no** se pisó.

## Nota fuera de alcance

El comentario en `PollingReceiver.cs` dice que "en server se reemplaza por WebhookReceiver" — esa clase no existe todavía en el repo. Este plan despliega el Engine tal cual está hoy (con polling cada 5s), lo cual funciona pero no es lo que el comentario anticipa como diseño final de producción. Si se quiere implementar el webhook receiver, es un tema aparte que no toca este plan de deploy/actualización.
