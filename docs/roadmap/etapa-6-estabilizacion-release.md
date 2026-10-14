# Etapa 6: Épica 6 - Estabilización, Integración E2E y Empaquetado Release

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RNF-01`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L250), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251), [`RNF-03`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L252), [`RNF-07`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L256), [`RNF-08`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L257), [`RF-22`, `RF-23`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#32-requisitos-funcionales-rf) (Track 6.3)

---

> **Objetivo:** Auditar la experiencia de usuario y ergonomía de mostrador, optimizar los tiempos de respuesta de base de datos, validar la resiliencia integral de flujos de negocio y generar el paquete de release auto-contenido para producción.

## Módulos y Responsabilidades Asignadas

### Track 6.1: Auditoría UX, Modo Mostrador Mouse-less y Flujos Integrados
**Responsable:** Lucas Kruzolek

* **Experiencia de Mostrador Mouse-less:** auditoría de accesibilidad y navegación 100% por teclado en POS y cajas (tabulación cíclica, foco garantizado y atajos F1-F12).
* **Manejo Global de Diálogos:** unificación visual de alertas amigables y registro de errores de interfaz con Serilog rotativo (`logs/retail-.log`).
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

**Mejoras futuras (fuera de alcance):** envío de correos de recuperación de contraseña (único mecanismo de recuperación previsto), copia cifrada a una carpeta sincronizada con OneDrive, migración de producción a SQL Server Express (coordinar con el Track 6.2) y **CRUD propio de Categorías y Marcas**, que hoy solo se cargan con `datos-demo`.

---

## Criterio de Aceptación Final
La solución compila en modo Release con cero advertencias (`TreatWarningsAsErrors`); la suite de pruebas unitarias e integración se ejecuta al 100% en verde; el sistema opera en mostrador con teclado y scanner a $< 15\text{ ms}$; y se genera el archivo ZIP de release auto-contenido listo para su defensa y entrega.
