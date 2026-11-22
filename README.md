# Órbita

Aplicación nativa de escritorio para Windows, escrita en C# / .NET 10 y WinUI 3. Un archivo seleccionado ocupa el centro de una rueda de acciones, con su tamaño visible. La rueda cambia según el formato.

## Ejecutar

Proyecto de código abierto bajo la [licencia MIT](LICENSE). Repositorio: [Silent343/Orbita](https://github.com/Silent343/Orbita).

Abre `publish/win-x64/Orbita.Desktop.exe` cuando exista una publicación compilada. Copia la carpeta completa para llevar la aplicación a otro equipo; el ejecutable necesita los archivos que lo acompañan.

Para desarrollo, instala el SDK de .NET 10 y abre `Orbita.slnx` en una versión de Visual Studio compatible con .NET 10 y WinUI. También se puede compilar desde PowerShell sin Visual Studio:

```powershell
.\scripts\setup.ps1                  # Solo si no hay SDK; descarga oficial local verificada con SHA512
.\scripts\build.ps1 -Test -Publish
```

Destino: Windows 10 2004 o posterior / Windows 11, x64. Mica y Acrylic dependen de la versión de Windows y de la configuración de transparencia; el sistema puede usar un fondo de reserva. La primera restauración necesita acceso a NuGet.

## Clonar y compilar

Clona el repositorio y compila desde PowerShell en Windows:

```powershell
git clone https://github.com/Silent343/Orbita.git
cd Orbita
.\scripts\setup.ps1
.\scripts\build.ps1 -Test -Publish
.\publish\win-x64\Orbita.Desktop.exe
```

El paso `setup.ps1` es opcional si ya tienes el SDK de .NET 10 instalado. Si lo ejecutas, descarga el SDK oficial, comprueba su SHA512 y lo prepara en `.tooling/dotnet`, dentro de la carpeta clonada. Las cachés y archivos de pruebas se guardan en `artifacts/`. Ambas carpetas, los binarios publicados y los diagnósticos locales están excluidos de Git. No necesitas Visual Studio para ejecutar estos scripts.

Clonar desde GitHub o compilar en tu equipo **no garantiza que Smart App Control permita ejecutar el resultado**. El código abierto facilita inspeccionar y reproducir la compilación, pero no sustituye la firma digital ni la confianza del sistema. En este equipo ya se bloquearon DLL compiladas localmente. Consulta la [documentación de Smart App Control](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/overview).

## Uso

1. Arrastra un archivo al espacio de trabajo o pulsa «Elegir archivo».
2. Elige un formato o cambia al grupo «Herramientas».
3. Usa Shift + la rueda del ratón para alternar entre ambos grupos.
4. Desde el Explorador o el escritorio, selecciona **un único archivo** y mantén Shift durante 350 ms. La ventana de Órbita debe seguir abierta, aunque puede estar minimizada. Suelta Shift o pulsa Esc para cerrar el menú flotante; haz clic en una acción mientras mantienes Shift para ejecutarla.
5. Los resultados se guardan en una subcarpeta `Orbita` junto al original, o en la carpeta configurada en Preferencias. Se reservan nombres nuevos para no sobrescribir archivos.

La pestaña Preferencias cambia español/inglés y tema claro/oscuro/sistema. La localización se inyecta en las vistas y modelos de presentación y se actualiza en caliente.

## Estado de verificación de esta entrega

Consulta [VERIFICATION.md](VERIFICATION.md). La compilación se genera sin firmar. En este equipo, una política de integridad de Windows exige una firma de confianza para `Orbita.Preferences.Application.dll` y `Orbita.Activity.Application.dll`, lo que bloquea parte de las pruebas y el arranque de la versión Release. No se ha cambiado ni desactivado esa política. La ejecución de esta entrega requiere autorización conforme a la política del equipo y, si corresponde, firma con un certificado de confianza. La inspección de la interfaz final está pendiente por ese bloqueo.

## Funciones incluidas

| Entrada | Acciones |
| --- | --- |
| PDF | Extraer texto a TXT / Markdown; renderizar todas las páginas a PNG / JPG |
| PNG, JPG, BMP, TIFF | Convertir entre estos formatos; recortar por coordenadas; redimensionar; girar 90°; guardar JPG a calidad 75 % |
| TXT / MD | Guardar copia como TXT / MD, conservando el contenido |
| Cualquier archivo | Crear un ZIP con el archivo |

Incluye cancelación, historial local de los últimos 200 resultados, preferencias persistentes y un registro de errores. Todo se procesa en el equipo; no se envían archivos a servidores.

## Límites de esta primera versión

- PDF → Word y PowerPoint aparecen deshabilitados. Necesitan un adaptador de conversión adicional que preserve la estructura editable. No se finge una conversión cambiando la extensión.
- La extracción PDF → TXT/MD conserva texto; no reconstruye tablas, formato o títulos Markdown. Los documentos escaneados necesitan OCR, aún no incluido.
- El recorte es por coordenadas; todavía no hay editor visual de imagen, eliminación de fondo ni vista previa del recorte.
- TXT ↔ MD conserva bytes y no interpreta ni elimina la sintaxis Markdown.
- JPG coloca la transparencia sobre blanco. La compresión a calidad 75 % puede generar un archivo mayor según la entrada.
- La edición trabaja sobre una imagen / primera página TIFF, no sobre secuencias animadas o TIFF multipágina.
- La ventana flotante usa Acrylic en una superficie rectangular de Windows. No es una superposición de transparencia por píxel fuera del círculo.
- El atajo usa la selección del Shell de Windows y no el portapapeles. Los gestores de archivos de terceros no están incluidos. No hay soporte para carpetas ni lotes.
- Cerrar la ventana principal cierra el programa. No se registra inicio automático, servicio ni icono de bandeja.
- La distribución es una carpeta portable sin firma digital ni instalador.

## Estructura

```text
src/
  BuildingBlocks/
    Orbita.SharedKernel/       Excepciones e infraestructura mínima de notificación
    Orbita.Localization/       Contrato ILocalizer y catálogos es/en
    Orbita.Persistence/        Almacenamiento JSON atómico y serialización concurrente
  Modules/
    Files/
      Orbita.Files.Domain/          Archivo, acciones y objetos de valor
      Orbita.Files.Application/     Casos de uso, catálogo y puertos
      Orbita.Files.Infrastructure/  Adaptadores WinRT, PdfPig, ZIP y disco
    Preferences/
      Orbita.Preferences.Domain/
      Orbita.Preferences.Application/
      Orbita.Preferences.Infrastructure/
    Activity/
      Orbita.Activity.Domain/
      Orbita.Activity.Application/
      Orbita.Activity.Infrastructure/
  Orbita.Desktop/              Host, composición DI, MVVM, WinUI e integración Shell
tests/Orbita.Tests/            Pruebas de dominio, arquitectura, i18n, persistencia y conversiones
```

Consulta [ARCHITECTURE.md](ARCHITECTURE.md) para las decisiones y los puntos de extensión. Las versiones de dependencias están fijadas en los proyectos.

Los datos de ejecución se guardan en `%LOCALAPPDATA%/Orbita`: `preferences.json`, `activity.json` y `diagnostics.log`. El historial contiene nombres y rutas locales. La carpeta se puede abrir desde Preferencias.

## Verificar la interfaz

```powershell
.\publish\win-x64\Orbita.Desktop.exe --verify-ui C:\ruta\de\verificacion
```

Este modo de diagnóstico abre la aplicación, inspecciona las tres vistas, cambia el idioma en caliente, captura cada vista y guarda `ui-verification.json`. Al terminar cierra la aplicación. No cambia las preferencias guardadas.

## Licencia

El código de Órbita se distribuye bajo la [licencia MIT](LICENSE). Las dependencias y herramientas de terceros conservan sus respectivas licencias.
