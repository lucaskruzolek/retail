# Convenciones de Código, Disciplina y Calidad

### Módulo: 01. Fundamentos y Onboarding
**Audiencia:** Desarrolladores, evaluadores y revisores de código  
**Gobernanza:** [`Directory.Build.props`](file:///c:/Users/lucas/Proyectos/retail/Directory.Build.props) y [`.editorconfig`](file:///c:/Users/lucas/Proyectos/retail/.editorconfig)  

---

## 1. 📖 Filosofía de Ingeniería: Rigor y Cero Atajos

En el desarrollo de software profesional y universitario, la calidad del código no es un accesorio cosmético; es el cimiento de la mantenibilidad. En Retail aplicamos una política de **disciplina técnica forzada por herramientas automatizadas**:

1. **El compilador como juez supremo:** Ningún código que genere advertencias (*warnings*) o riesgos de referencia nula no controlada puede ser integrado a la rama principal.
2. **Claridad Pedagógica:** El código debe ser auto-documentado, legible y estructurado sin trucos oscuros ni abreviaturas crípticas.
3. **Simplicidad Pragmática (Ponytail Principle):** Reutilizar lo existente, resolver los problemas en su causa raíz y evitar la proliferación de abstracciones no solicitadas (*Boring code is good code*).

---

## 2. ⚖️ Las Reglas Técnicas Forzadas por Compilación

### 2.1 Cero Tolerancia a Advertencias (`TreatWarningsAsErrors`)
En la raíz de la solución, el archivo [`Directory.Build.props`](file:///c:/Users/lucas/Proyectos/retail/Directory.Build.props) establece directivas que afectan a todos los proyectos C#:

```xml
<PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
</PropertyGroup>
```

* `<Nullable>enable</Nullable>`: Obliga al desarrollador a declarar explícitamente qué variables o propiedades pueden ser nulas (`string?`) y cuáles son obligatorias (`string`). Elimina la plaga del `NullReferenceException` en tiempo de ejecución.
* `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`: Cualquier variable no utilizada, directiva `using` redundante o posible desreferencia nula hace que el comando `dotnet build` falle inmediatamente con código de error.

### 2.2 Formato y Estilo de Código: Allman Style
A través de [`.editorconfig`](file:///c:/Users/lucas/Proyectos/retail/.editorconfig), el repositorio impone:
* **Estilo Allman:** Las llaves de apertura y cierre `{ }` siempre se ubican en una **nueva línea**.
* **Indentación:** 4 espacios para C# y XAML; 2 espacios para JSON y YAML.
* **Prohibición de Tabulaciones:** Solo espacios.

```csharp
// CORRECTO: Estilo Allman, llaves en nueva línea
public void RegistrarVenta(Venta venta)
{
    if (venta == null)
    {
        throw new ArgumentNullException(nameof(venta));
    }
}

// PROHIBIDO: Estilo K&R (llave en la misma línea)
public void RegistrarVenta(Venta venta) {
    if (venta == null) {
        throw new ArgumentNullException(nameof(venta));
    }
}
```

---

## 3. 🧪 Estándar de Pruebas Unitarias: Convención BDD

Las pruebas en `tests/` siguen la convención formal de nomenclatura **BDD (Behavior-Driven Development)**:

$$\text{MetodoProbado}\_\text{CondicionOCenario}\_\text{ResultadoEsperado}$$

### Ejemplo en [`Retail.Domain.UnitTests`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Domain.UnitTests/):
```csharp
[Fact]
public void AgregarItem_CuandoStockEsInsuficiente_LanzaStockInsuficienteException()
{
    // 1. Arrange (Preparar)
    var articulo = new Articulo("Cuaderno", precio: 100, stock: 2);
    var venta = new Venta();

    // 2. Act (Actuar)
    Action accion = () => venta.AgregarItem(articulo, cantidad: 5, precioUnitario: 100);

    // 3. Assert (Verificar con FluentAssertions)
    accion.Should().Throw<StockInsuficienteException>()
        .WithMessage("*Stock insuficiente*");
}
```

---

## 4. 👥 Gobernanza de Pareja y Peer Review (Lucas & Pablo)

Para garantizar la autoría compartida del 100% de la base de código y la preparación paritaria ante la mesa de examen:

1. **Modelo Full-Stack Desktop:** Lucas y Pablo dominan el flujo completo desde la vista XAML hasta la base de datos SQL.
2. **Revisión Cruzada Obligatoria (100% Peer Review):** Ningún Pull Request se fusiona en `main` sin la aprobación formal del otro compañero.
3. **Aduana de CI Automática:** Antes de que un PR pueda ser revisado por un humano, el workflow [`.github/workflows/ci.yml`](file:///c:/Users/lucas/Proyectos/retail/.github/workflows/ci.yml) compila en Release, corre todos los tests y valida el formato.
