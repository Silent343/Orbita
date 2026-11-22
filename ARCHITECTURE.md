# Arquitectura de Órbita

## Monolito modular y límites

Un proceso WinUI aloja tres contextos delimitados: **Files**, **Preferences** y **Activity**. Cada módulo dispone de sus propios contratos, modelo de dominio y adaptadores. Los módulos no se referencian entre sí. La composición del host conecta sus contratos de aplicación mediante `OperationCoordinator`.

La dependencia interna de cada módulo es:

```text
Desktop (composición y presentación)
  → Infrastructure → Application → Domain → SharedKernel
  → Application
```

Domain no conoce WinUI, APIs WinRT, disco, JSON ni inyección de dependencias. Application define los puertos y usa el dominio; Infrastructure implementa esos puertos. Las referencias del host a infraestructura se usan en el punto de composición, no para ejecutar conversiones desde las vistas.

## DDD y POO

El diseño aplica DDD de forma acotada: contextos delimitados, lenguaje común de archivo/acción/preferencia/operación, reglas en el modelo y contratos explícitos. `FileAsset` es un objeto inmutable que garantiza ruta absoluta, tamaño válido y clasificación coherente. `UserPreferences` contiene la validación del idioma, tema y gesto. `OperationRecord` identifica un resultado completado con un ID estable. No se añaden agregados o eventos distribuidos donde el dominio actual no los necesita.

El catálogo determina qué acciones corresponden al archivo y su disponibilidad. El caso de uso vuelve a comprobar esa política antes de despachar una acción; una llamada desde otra interfaz no puede saltarse la restricción de la UI.

La POO usa encapsulación e inmutabilidad en el modelo, composición de servicios, contratos pequeños y polimorfismo en los adaptadores `IFileActionHandler`. Las operaciones de disco y los formatos no se implementan dentro del dominio.

## SOLID

- Responsabilidad única: inspeccionar archivos, aplicar reglas, convertir, persistir preferencias y dibujar la rueda son componentes separados.
- Abierto/cerrado: un motor nuevo implementa `IFileActionHandler` y se registra en composición; el despachador no necesita nuevas ramas.
- Sustitución: cada adaptador cumple el mismo contrato de resultado, cancelación y salida en una copia nueva.
- Segregación: `IFileInspector`, `IActionCatalog`, `IFileActions`, repositorios y `ILocalizer` cubren necesidades concretas.
- Inversión: casos de uso y vistas reciben sus dependencias por constructor; no consultan un contenedor global. Solo el host y su raíz de composición resuelven servicios.

## Presentación y traducciones

WinUI y los ViewModels forman la capa de presentación. El host utiliza `Microsoft.Extensions.DependencyInjection`; cada ViewModel recibe `ILocalizer`, y cada vista recibe su ViewModel. El mismo localizador alimenta cabecera, navegación, todas las pestañas, controles de acciones, diálogos y ventana flotante.

Los catálogos JSON están embebidos en `Orbita.Localization`. Sus claves son estables y la infraestructura de dominio almacena claves o códigos, nunca texto ya traducido. `LanguageChanged` e `INotifyPropertyChanged` actualizan las vinculaciones existentes sin reinicio. El historial traduce el nombre de las acciones al idioma actual, no al idioma del día en que se ejecutaron.

Para incorporar otro idioma, añade el catálogo embebido y el idioma en el localizador, la validación y el selector. Las pruebas comprueban paridad de claves.

## Integridad de archivos y persistencia

`OutputFile` reserva un nombre con `FileMode.CreateNew`; si existe, prueba un sufijo. Los originales nunca se abren para escritura. Las salidas parciales se eliminan cuando una operación falla o se cancela; una conversión PDF multipágina confirma el conjunto solo al terminar. El contenido se procesa de forma secuencial por página.

Preferencias e historial usan archivos JSON en esta primera versión. `JsonFileStore` serializa cambios con `SemaphoreSlim`, escribe un temporal y lo mueve sobre el destino. Un error al registrar actividad después de completar una conversión se informa como advertencia; no se informa falsamente que la conversión falló. Para reemplazar JSON por SQLite solo hacen falta implementaciones nuevas de los repositorios.

## Integración con Windows

`ExplorerShortcutService` comprueba el estado de Shift en el hilo de UI cada 35 ms. Solo activa el gesto si el primer plano pertenece al Explorador o al escritorio; no bloquea ni consume teclas globales. Ratón pulsado, Ctrl o Alt impiden iniciar el gesto para respetar selección y arrastre del sistema. El lector Shell usa COM y libera sus referencias; no altera el portapapeles.

WinUI controla la ventana de escritorio y una segunda ventana Acrylic superior. La rueda se puede usar con botones y foco de teclado en la aplicación principal, además del gesto con ratón. La composición visual respeta el tema del usuario; Windows decide la disponibilidad real de los materiales.

## Añadir PDF → DOCX/PPTX

1. Elegir un motor con la fidelidad y licencia adecuadas.
2. Implementar un adaptador `IFileActionHandler` en infraestructura o un módulo de motores.
3. Registrar ese adaptador en `ServiceRegistration`.
4. Marcar como disponibles las acciones del catálogo solo cuando el motor esté instalado.
5. Añadir pruebas con PDFs de texto, tablas, imágenes y escaneos.

Un conversor externo se debe ejecutar como proceso separado con argumentos estructurados, cancelación y salida capturada. El contrato admite esa implementación sin acoplar la UI a un proveedor.

## Validación

La solución incluye pruebas de invariantes, límites de dependencias, actualización de i18n, guardado concurrente, retención del historial, nombres sin colisiones, cancelación, imágenes decodificables y extracción/renderizado de un PDF real. `--verify-ui` comprueba que las tres pestañas actualizan el idioma en un árbol visual WinUI real y genera capturas para inspeccionar el diseño.
