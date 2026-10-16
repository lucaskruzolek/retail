# Defensa Técnica del Tablero Gerencial y los Reportes Exportables

### Módulo: 06. Vademécum para la Defensa Oral de Cátedra
**Audiencia:** Lucas Kruzolek, Pablo Fernandez y mesa evaluadora de cátedra  
**Requisitos Vinculados:** `RF-24`, `RF-25`, `RF-26`, `RNF-09`  
**Flujo de punta a punta:** [Flujo del Tablero Gerencial y Reportes](../05-casos-de-uso-y-flujos/flujo-tablero-gerencial-y-reportes.md)  
**Decisiones con su fundamento (D-1 a D-36) y riesgos:** [`etapa-6-track-6.4-dashboard-reportes.md`](file:///c:/Users/lucas/Proyectos/retail/docs/roadmap/etapa-6-track-6.4-dashboard-reportes.md)

---

## 1. ⚖️ Matrices de Decisión

### 1.1 ¿Dónde se calculan los reportes?
| Alternativa | Veredicto | Motivo |
| :--- | :---: | :--- |
| Procedimiento almacenado | ❌ | La Ley 6 de persistencia prohíbe calcular márgenes en la base. Además, EF Core no puede componer filtros sobre un procedimiento y no mejora el rendimiento. |
| Función escalar | ❌ | Prohibida por la misma ley, y en una consulta de reporte se ejecuta fila por fila |
| Vista SQL o función con valor de tabla | ⚠️ Posible, no se justifica | Una vista común **no guarda resultados**: el motor la expande dentro de la consulta igual que un LINQ. Saca la fórmula del compilador y obliga a repetir a mano el filtro de baja lógica. |
| Vista indexada (materializada) | ❌ | Es la única que acelera, pero el costo lo paga **cada venta del mostrador**, y SQL Server Express solo la usa con `NOEXPAND`, que EF no genera |
| **LINQ con push-down + "vista en C#" + índice** | ✅ | Una sola definición reutilizable, tipada, que respeta los filtros globales y se traduce a un único `SELECT`. El rendimiento lo da el índice. |

**Frase para la mesa:** *"En una base relacional, el rendimiento lo dan los índices y el plan de ejecución, no el lugar donde se escribe la consulta."*

### 1.2 ¿Cómo se muestra el medio de pago?
| Alternativa | Veredicto | Motivo |
| :--- | :---: | :--- |
| Filtro por medio de pago | ❌ | Ambiguo con pagos mixtos: una venta con efectivo y tarjeta, ¿"es" de efectivo? Se contaría dos veces. |
| Combo de un medio por vez | ❌ | No se pueden comparar los medios entre sí |
| Detalle al hacer clic en un período | ❌ | Esconde el dato y no se exporta (RF-26 exige exportar lo que se ve) |
| **Desglose de montos en columnas** | ✅ | Cada peso cae en una sola columna, la suma coincide con el total (`Venta.ValidarCierre`) y sirve para conciliar con la caja y con el cierre de lote |

### 1.3 Bibliotecas
| Necesidad | Elegida | Descartadas | Motivo |
| :--- | :--- | :--- | :--- |
| Gráficos | LiveCharts2 2.0.5 (MIT) | ScottPlot, OxyPlot, XAML puro | Binding MVVM nativo y dibujo fuera de pantalla para el PDF. Versión estable verificada en el índice de NuGet, no en buscadores. |
| PDF | QuestPDF 2026.9.1 (Community MIT) | PDFsharp/MigraDoc | API declarativa con paginación y tablas que continúan solas. Gratuita para uso educativo y para empresas con facturación anual menor a USD 1 M. |
| Excel | MiniExcel (ya instalado) | — | Peldaño 4 de la escalera de decisión: una dependencia existente lo resuelve |

---

## 2. 🔍 Tres Hallazgos que Solo Aparecieron Probando de Verdad

| Hallazgo | Cómo apareció | Lección |
| :--- | :--- | :--- |
| `IgnoreQueryFilters()` desactiva los filtros de **toda** la consulta | Un test de integración: un artículo dado de baja reaparecía en "menos vendidos" | Una excepción a una regla global debe tener su propio test |
| El test de rendimiento se colgaba 5 minutos | Diagnóstico con `sys.dm_exec_requests`: espera `RESOURCE_SEMAPHORE`. SQL Server solo tenía 6 MB para memoria de consultas porque la PC tenía 474 MB libres. | Un test que depende del estado de la máquina no es determinista y no va en la suite por defecto |
| Sin el archivo `QuestPDF.Fonts.Lato.br`, ningún PDF se generaba | Publicando el ejecutable único y ejecutándolo en una carpeta limpia | Desde la versión 2026.9.0, QuestPDF falla si falta cualquier fuente de la lista; ahora el PDF usa Segoe UI del sistema |

### 2.1 La tercera capa de verificación: mirar el resultado
Los tests automáticos estaban en verde y, aun así, **mirar** el PDF y la pantalla (renderizada fuera de pantalla con datos de estrés) encontró defectos que ningún test veía:

| Defecto | Por qué los tests no lo veían |
| :--- | :--- |
| Rentabilidad y Ranking mostraban **el gráfico de otro reporte** al cambiar de pestaña | El ViewModel tenía el estado correcto; el error estaba en cómo el control procesaba un paso intermedio vacío |
| Eje de barras empezando en 70: la barra de 100 parecía la cuarta parte de la de 250 | Los tests verificaban que la imagen fuera un PNG válido, no que fuera honesta |
| Líneas suavizadas que inventaban valores entre puntos | Ídem |
| La mitad de las barras sin nombre; nombres truncados idénticos ("…tomo 2", "…tomo 16" cortados al final) | Depende del espacio en píxeles y de datos realistas |
| Pestaña activa en ámbar, el color de "advertencia" | Es un error de significado, no de código |

**Respuesta modelo a "¿cómo validaron la interfaz?":** *"En tres capas: tests de ViewModel para la lógica, pruebas de humo STA para que el XAML resuelva todos sus recursos, y revisión visual con datos de estrés (importes de 9 cifras, 15 categorías, nombres largos) para la presentación. La tercera capa encontró un bug de correctitud, el gráfico de otro reporte, que las dos primeras no veían."*

---

## 3. 🎯 Preguntas Trampa con Respuesta Modelo

**1. "Si el artículo tiene 66 % de ganancia, ¿por qué el tablero dice 35 % de margen?"**  
Son bases distintas. El 66 % es *markup* sobre el costo; el margen del tablero es sobre la venta. Además, los descuentos prorrateados reducen el ingreso, y cada venta usa el costo **de su momento** (`CostoUnitario`), no el actual.

**2. "¿Por qué guardan el costo en cada ítem vendido? ¿No es redundante?"**  
No: es un dato distinto. El costo del artículo es el **vigente**; el del ítem es el **histórico**. Sin esa foto, el margen de marzo cambiaría cada vez que se importa una lista de precios. Es el mismo principio por el que la venta guarda su precio.

**3. "¿Por qué la exportación no vuelve a consultar la base?"**  
RF-26 pide exportar **exactamente lo que se ve**. Si se consultara de nuevo, una venta registrada en el medio haría que el archivo no coincidiera con la pantalla. Además, permite reintentar sin repetir la consulta si el archivo está abierto en Excel.

**4. "¿Por qué no paginaron la grilla de ventas por período?"**  
Porque son datos ya agregados: se acotó el **rango** (primera venta como límite inferior y un largo máximo por agrupación), así que la grilla nunca supera ~366 filas. Paginar complicaría el gráfico y la exportación, que necesitan todos los puntos. *Acotar el resultado es mejor que paginarlo cuando la consulta es una agregación.*

**5. "¿Qué pasa si el gerente consulta la semana del cambio de mes, o una venta de las 23:30?"**  
La semana se calcula con `DATEDIFF` desde un lunes ancla, independiente de la configuración del servidor. La venta de las 23:30 hora local, que en UTC es del día siguiente, se agrupa en su día local porque el rango se convierte a UTC antes de consultar y la agrupación suma el desfase en SQL. Ambos casos tienen tests de integración.

**6. "¿Cómo garantizan que un Cajero no vea la rentabilidad?"**  
El menú solo muestra el tablero al Gerente, pero eso **no es control de acceso**: el servicio de Aplicación verifica el rol del actor en cada consulta y en cada exportación, y lanza `OperacionNoAutorizadaException` (RNF-06). Hay tests que lo comprueban.

**7. "¿Por qué la variación de 'Mes anterior' no compara con la misma cantidad de días?"**  
Porque el gerente piensa en meses calendario: setiembre se compara con agosto completo (31 días), no con "los 30 días anteriores", que perderían el 1 de agosto. Si el rango empieza el 1 de enero, la comparación es interanual. Los rangos libres sí usan igual duración (RF-24).
