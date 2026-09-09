# Etapa 6: Épica 6 - Estabilización, Integración E2E y Empaquetado Release

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RNF-01`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L250), [`RNF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L251), [`RNF-03`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L252), [`RNF-07`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L256), [`RNF-08`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L257)

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

## Criterio de Aceptación Final
La solución compila en modo Release con cero advertencias (`TreatWarningsAsErrors`); la suite de pruebas unitarias e integración se ejecuta al 100% en verde; el sistema opera en mostrador con teclado y scanner a $< 15\text{ ms}$; y se genera el archivo ZIP de release auto-contenido listo para su defensa y entrega.
