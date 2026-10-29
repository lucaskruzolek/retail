# Etapa 6: Épica 6 - Estabilización, Integración E2E y Empaquetado Release

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RNF-01`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L250), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251), [`RNF-03`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L252), [`RNF-07`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L256), [`RNF-08`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L257), [`RF-22`, `RF-23`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#32-requisitos-funcionales-rf) (Track 6.3 y Track 6.3.1), [`RF-24` a `RF-26`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#32-requisitos-funcionales-rf) (Track 6.4), [`RF-04`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#32-requisitos-funcionales-rf) (Track 6.5), [`RF-27`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#32-requisitos-funcionales-rf) (Track 6.6)

---

> **Objetivo:** Auditar la experiencia de usuario y ergonomía de mostrador, optimizar los tiempos de respuesta de base de datos, validar la resiliencia integral de flujos de negocio y generar el paquete de release auto-contenido para producción.

## Módulos y Responsabilidades Asignadas

### Track 6.1: Auditoría UX, Modo Mostrador Mouse-less y Flujos Integrados
**Responsable:** Lucas Kruzolek

* **Experiencia de Mostrador Mouse-less:** auditoría de accesibilidad y navegación 100% por teclado en POS y cajas (tabulación cíclica, foco garantizado y atajos F1-F12).
* **Manejo Global de Diálogos:** unificación visual de alertas amigables y registro de errores de interfaz con Serilog rotativo (`logs/retail-.log`).
* **Autorización por rol en servicios operativos (deuda registrada el 2026-10-29):** los servicios de ventas, caja, compras, pedidos y artículos todavía no verifican el rol del actor; la restricción es solo de navegación o de interfaz (A-14 del Track 4.3; nota de navegación del Track 6.3). Se aplica el patrón ya implementado en `ReporteService.AutorizarGerenteAsync`: el servicio relee al actor por su id desde la base y lanza `OperacionNoAutorizadaException`; la UI la traduce a un mensaje. Exige que los comandos que hoy no reciben el actor (por ejemplo, `EmitirAsync` y `CancelarAsync` de pedidos) pasen a recibirlo.
* **Pruebas de Flujos E2E:** validación de escenarios límite (venta sin stock, cliente con límite de crédito superado, presupuesto con precios desactualizados, corte fiscal).
* **Documentación:** manual de operación de mostrador y guía visual para defensa académica.

---

### Track 6.2: Rendimiento de Base de Datos y Empaquetado Release
**Responsable:** Pablo Fernandez

* **Optimización de Persistencia:** revisión de índices en SQL Server / LocalDB, auditoría de tiempos de respuesta en consultas de catálogo y venta ($< 15\text{ ms}$ en mostrador).
* **Pipeline CD en GitHub Actions:** validación del workflow `release.yml` para compilación de binario auto-contenido `Retail.App` (win-x64), compresión en ZIP y publicación de GitHub Release oficial.
* **Inner Loop de Calidad:** verificación estricta de compilación Release con cero advertencias, 100% de tests xUnit aprobados y formato conforme a `.editorconfig`.
* **Validación de Despliegue:** prueba de ejecución en equipo Windows limpio sin SDK .NET.

---

### Track 6.3: Continuidad Operativa — Backup, Restore y Administración de Base
**Responsable:** Lucas Kruzolek | **Rama:** `feat/backup-restore` | **Requisitos:** `RF-03`, `RF-22`, `RF-23`, `RNF-08`  
**Plan de diseño, decisiones y avance:** [`etapa-6-track-6.3-backup-restore.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.3-backup-restore.md)

> [!NOTE]
> **Implementación adelantada.** Este track se ejecuta antes que las Etapas 4–5 por pedido de la cátedra y **no depende de módulos pendientes**. Solo modifica zonas compartidas (`App.xaml.cs`, `MainViewModel.cs`, seed inicial) en las fases indicadas, coordinando con Pablo (Ley 10).

* **Fase 1 — Roles y cuentas:** rol `Administrador = 4` agregado por migración (opcional, sin invariante de "último administrador"); el Gerente asigna cualquier rol y el Administrador solo gestiona Cajeros y Encargados, con la autorización validada en Application; cambio de contraseña obligatorio (`DebeCambiarPassword`); email de recuperación obligatorio para Gerentes; reautenticación (`IAuthService.VerificarCredencialAsync`).
* **Fase 2 — Herramienta `Retail.Cli`:** comandos `migrar`, `inicializar` (solo el primer Gerente, con email), `datos-demo` (un proveedor, categorías y marcas) y `estado`. Sin recuperación de contraseñas por consola: es exclusivamente por correo (D-13). Se quitan `MigrateAsync` y el seed de `OnStartup`, que pasa a verificar el esquema y la existencia de un Gerente. Se eliminan la contraseña fija `Admin123!` y los artículos de demostración.
* **Fase 3 — Dominio de respaldos:** agregado `RegistroBackup`, enums de tipo y estado, excepciones y migración de `REGISTROS_BACKUP`.
* **Fase 4 — Backup:** `BACKUP DATABASE ... WITH CHECKSUM`, `RESTORE VERIFYONLY`, hash SHA-256, manifiesto `.json` por archivo, retención configurable, directorio dedicado `C:\RetailData\Backups` y backup `PreMigracion` dentro de `migrar`.
* **Fase 5 — Restore:** backup `PreRestauracion`, modo `SINGLE_USER`, rehidratación del historial de respaldos, bloqueo con turno de caja abierto, aviso de conciliación fiscal ARCA y comando `restaurar` para recuperación ante desastres.
* **Fase 6 — UI:** `BackupView` (IU-10) y `ConfirmarCredencialDialog`, visibles solo para Gerente (lectura para Administrador).
* **Fase 7 — Backup automático:** `BackupAutomaticoHostedService` con recuperación al arrancar, más un script PowerShell que registra `Retail.Cli backup --automatico` en el Programador de tareas.

**Mejoras futuras (fuera de alcance):** envío de correos de recuperación de contraseña (único mecanismo de recuperación previsto), copia cifrada a una carpeta sincronizada con OneDrive y migración de producción a SQL Server Express (coordinar con el Track 6.2). El CRUD de Categorías y Marcas, que hoy solo se cargan con `datos-demo`, pasó a ser el [Track 6.5](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.5-crud-categorias-marcas.md).

---

### Track 6.3.1: Control Dual de Restauración
**Responsable:** Lucas Kruzolek | **Rama:** `feat/control-dual-restore` | **Requisitos:** `RF-03`, `RF-22`, `RNF-08`  
**Plan de diseño, decisiones y avance:** [`etapa-6-track-6.3.1-control-dual-restore.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.3.1-control-dual-restore.md)

El Administrador ejecuta el backup manual con su propia contraseña y la restauración con el usuario y la contraseña de un Gerente que la autoriza (control dual, D-30 a D-32). Tras 3 autorizaciones fallidas consecutivas, su cuenta queda bloqueada hasta que un Gerente la desbloquee.

**Estado:** Diseño aprobado, sin implementar (2026-10-17).

---

### Track 6.4: Tablero Gerencial y Reportes Exportables
**Responsable:** Lucas Kruzolek | **Rama:** `feat/dashboard-reportes` | **Requisitos:** `RF-24`, `RF-25`, `RF-26`, `RNF-09`  
**Plan de diseño, decisiones y avance:** [`etapa-6-track-6.4-dashboard-reportes.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.4-dashboard-reportes.md)

* **Fase 1 — Dominio:** costo histórico `DetalleVenta.CostoUnitario`, tomado en `Venta.AgregarItem`.
* **Fase 2 — Aplicación:** `IReporteService` (Gerente, filtros validados, rango local → UTC), DTOs de reportes y puertos de lectura y exportación.
* **Fase 3 — Persistencia:** migración del costo e índice `IX_VENTAS_fecha_hora`; `ReporteQueryService` en LINQ con push-down (sin vistas ni procedimientos almacenados, ver sección 3 del plan).
* **Fase 4 — Exportación:** Excel con MiniExcel y PDF con QuestPDF, con gráficos renderizados fuera de pantalla.
* **Fase 5 — UI:** `DashboardView` (IU-11) con LiveCharts2, visible solo para el Gerente.

---

### Track 6.5: Rubros, Categorías y Marcas — CRUD de Clasificación
**Responsable:** Lucas Kruzolek | **Rama sugerida:** `feat/crud-categorias-marcas` | **Requisitos:** [`RF-04`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#32-requisitos-funcionales-rf)  
**Plan de diseño, decisiones y avance:** [`etapa-6-track-6.5-crud-categorias-marcas.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.5-crud-categorias-marcas.md)

> [!NOTE]
> **Diseño cerrado en lo conceptual, sin implementar (2026-10-17).** Este track reemplaza la mejora futura "CRUD propio de Categorías y Marcas" del Track 6.3 y se implementa **antes** que el Track 6.6. Se accede por un diálogo desde el catálogo de artículos, sin tocar `MainWindow` ni `MainViewModel` (Ley 10).

* **✅ Fase 0 — Alta y requisitos:** alta del track en este roadmap, ampliación de `RF-04` en la ERS y cierre de decisiones (hecho).
* **Fase 1 — Dominio:** nueva raíz de agregado `Rubro`; `Categoria` y `Marca` encapsuladas (`Crear`/`Renombrar`, `IdRubro` obligatorio en `Categoria`), regla única de texto y `ClasificacionEnUsoException`.
* **Fase 2 — Aplicación:** `IRubroService`, `ICategoriaService` e `IMarcaService` (sin abstracción genérica), DTOs con cantidad de artículos, validadores FluentValidation, autorización por rol validada en el servicio y baja bloqueada si hay artículos o categorías asignados.
* **Fase 3 — Infraestructura:** tabla `RUBROS`, índices únicos filtrados por `deleted_at IS NULL`, migración con detección previa de duplicados, conteos en SQL con push-down (Ley 8) y semillero con fábricas.
* **Fase 4 — UI:** `AdministrarClasificacionDialog` (pestañas Rubros y categorías, y Marcas) y `ClasificacionFormDialog`, lanzados desde `ArticulosView`.
* **Fase 5 — Cierre:** verificación Release, mapa semántico y propuesta de nota para `wiki/`.

---

### Track 6.6: Catalogación Inteligente — Sugerencia de Categoría y Marca
**Responsable:** Lucas Kruzolek | **Rama sugerida:** `feat/catalogacion-inteligente` | **Depende de:** Track 6.5 | **Requisitos:** [`RF-27`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#32-requisitos-funcionales-rf), `RNF-02`, `RNF-03`, `RNF-07`  
**Plan de diseño, decisiones y avance:** [`etapa-6-track-6.6-catalogacion-inteligente.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.6-catalogacion-inteligente.md)

> [!NOTE]
> **Diseño cerrado en lo conceptual, sin implementar (2026-10-17).** Al incorporar artículos del catálogo de un proveedor a la tienda, el sistema **sugiere** categoría y marca con un nivel de confianza; nada se asigna sin que una persona lo vea. El motor corre localmente, sin servicios externos. La Fase 1 (datos) puede solaparse con el Track 6.5 porque no toca `src/`.

* **✅ Fase 0 — Exploración y diseño:** scripts de exploración en `ml/` (fuera de `Retail.sln`), decisiones M-1 a M-16, alta del track en este roadmap y `RF-27` en la ERS (hecho).
* **Fase 1 — Arranque de datos:** diccionarios revisados por el estudiante y conjunto de oro.
* **Fase 2 — Experimentos E-1 a E-4:** selección del clasificador y de los umbrales, en Python.
* **Fase 3 — Dominio:** entidades hijas de `Categoria` y `Marca`, origen de la clasificación y regla de EAN.
* **Fase 4 — Aplicación:** puertos, servicio de clasificación por niveles y cambio del contrato de incorporación.
* **Fase 5 — Infraestructura:** vectorizador de n-gramas, clasificador, almacenamiento del modelo y migraciones.
* **Fase 6 — UI y `Retail.Cli`:** sugerencias por artículo, cola de revisión, carga de diccionarios y "Actualizar modelo".
* **Fase 7 — Cierre:** evaluación final sobre el conjunto de oro, verificación Release, mapa y wiki.

---

## Criterio de Aceptación Final
La solución compila en modo Release con cero advertencias (`TreatWarningsAsErrors`); la suite de pruebas unitarias e integración se ejecuta al 100% en verde; el sistema opera en mostrador con teclado y scanner a $< 15\text{ ms}$; y se genera el archivo ZIP de release auto-contenido listo para su defensa y entrega.
