# 05. Casos de Uso y Flujos de Negocio de Punta a Punta

Este módulo documenta el recorrido transversal de las operaciones comerciales de la librería, conectando las interfaces visuales de mostrador con las reglas de dominio, transacciones de persistencia y hardware de impresión:

---

## 🧭 Catálogo de Flujos Documentados

| Flujo de Negocio | Requisitos ERS | Desafío y Responsabilidad Técnica | Enlace al Artículo |
| :--- | :--- | :--- | :--- |
| **Venta en Mostrador y Descuento de Stock** | `RF-09`, `RF-10` | Transacción ACID atómica, cobro multimedio (*Split Payments*), cálculo estricto de vuelto y emisión de ticket térmico en 40 columnas. | [Ver Artículo](flujo-venta-y-descuento-stock.md) |
| **Presupuestos y Conciliación Adaptativa** | `RF-11`, `RF-12` | Congelamiento de precios por 15 días, no reserva de stock en góndola, detección de variaciones de costo y conversión a venta. | [Ver Artículo](flujo-conciliacion-presupuesto.md) |
| **Apertura de Caja y Arqueo Ciego** | `RF-13`, `RF-14`, `RF-15` | Custodia de gaveta de efectivo, retiros y gastos justificados, balance teórico y arqueo ciego anti-fraude sin sesgos. | [Ver Artículo](flujo-caja-y-arqueo-ciego.md) |
| **Importación Masiva de Listas de Precios** | `RF-05`, `RF-07` | Procesamiento en segundo plano (`Task.Run`) de planillas XLSX de distribuidores con MiniExcel en streaming ($\le 25\text{ MB}$ RAM) y recálculo por markup. | [Ver Artículo](flujo-importador-excel.md) |

---

## 📐 Estructura de Estudio
Cada artículo cuenta con:
1. **Fundamento Teórico:** La lógica de negocio del comercio minorista y normativas operativas.
2. **Código Real:** Enlaces directos a entidades de dominio, DTOs, validadores y servicios de `Retail.sln`.
3. **Diagramas Mermaid:** Diagramas de secuencia y máquinas de estados que recorren el flujo de punta a punta.
4. **Decisiones y Trade-offs:** Análisis de escenarios extremos (caídas de red, falta de stock, inflación).
5. **Preguntas de Examen:** Cuestionarios de autoevaluación para la mesa evaluadora de cátedra.
