# Verificación de la primera entrega

## Actualización: 22 de noviembre de 2026

Después de que el propietario ajustara Smart App Control, las **20 pruebas automatizadas pasaron sin fallos ni omisiones**. Se verificó también el ejecutable portable con `--verify-ui`: las pestañas de trabajo, preferencias e historial cargan y actualizan sus textos al cambiar entre español e inglés.

El cierre del portable al cargar XAML se debía a la ausencia de `Orbita.Desktop.pri` en la publicación, no a un error del marcado. Se añadió un destino MSBuild que incluye el PRI generado en `dotnet publish`, y `scripts/verify-publish.ps1` comprueba los archivos esenciales. La comprobación falló por el PRI ausente antes del cambio y pasó después, junto con la prueba de arranque del portable. El gesto global del Explorador todavía no se ha verificado de extremo a extremo.

Los apartados siguientes conservan el estado histórico de la primera entrega; los bloqueos de aquella ejecución no representan el resultado de esta actualización.

## Compilación

La solución se ha compilado con el SDK oficial .NET 10.0.402 y Windows App SDK 2.5.1. El SDK se descargó en la carpeta de trabajo del proyecto y se verificó con el SHA512 publicado por Microsoft. Las dependencias NuGet tienen versiones explícitas.

## Pruebas automatizadas

En la última ejecución Release x64: **16 superadas, 4 bloqueadas, 0 omitidas**. Los cuatro resultados bloqueados se informan como fallos del ejecutor, pero su causa es `FileLoadException`, código `0x800711C7`, emitido por Control de aplicaciones de Windows al cargar dos ensamblados sin firma de confianza.

Las pruebas que pudieron ejecutarse comprueban clasificación de archivos, invariantes, rechazo de acciones incompatibles, validación de preferencias, paridad y notificación de traducciones, ZIP con conservación del original y nombres sin colisiones, cancelación con limpieza, recorte inválido, conversión/redimensión/recorte/giro de imágenes y extracción/renderizado de PDF. Los resultados de imagen se vuelven a decodificar, y el contenido de texto PDF y ZIP se lee para comprobarlo.

Las pruebas bloqueadas en esta ejecución son:

- Límites de dependencias de dominio y aplicación.
- Guardado y lectura de preferencias.
- Conservación de entradas de historial con escrituras concurrentes.
- Retención de las últimas 200 entradas de historial.

Esas cuatro pruebas sí pasaron en una compilación Debug anterior. Después se corrigieron dos problemas detectados durante la validación: vida útil de píxeles/calidad del codificador JPG y notificaciones de traducción para enlaces WinUI. La comprobación final de i18n en el árbol visual no ha podido completarse.

## Interfaz

La primera ejecución de desarrollo abrió la ventana real y detectó que WinUI no renovaba las vinculaciones directas al indexador de traducciones. Se sustituyeron por valores observables estables `LocalizedString.Value`, con pruebas de notificación. La nueva ejecución Release no pudo cargar `Orbita.Preferences.Application.dll` y `Orbita.Activity.Application.dll` por la política de firma de Windows.

Por tanto, **no se afirma que la interfaz final ni el gesto global de Explorer estén verificados de extremo a extremo**. El modo `--verify-ui` está incluido para continuar la comprobación en un entorno autorizado.

## Bloqueo confirmado

Los eventos de `Microsoft-Windows-CodeIntegrity/Operational`, IDs 3077 y 3033, identifican los ensamblados anteriores y el requisito de firma empresarial. Recompilar fuera del entorno restringido no eliminó el bloqueo. No se cambiaron políticas de Windows, no se añadieron excepciones y no se instalaron certificados.

Para ejecutar esta compilación en este equipo hace falta que el administrador o propietario autorice la aplicación por el mecanismo aprobado de su política, o que se use una distribución firmada con un certificado que el equipo ya considere de confianza.
