# Suites de Pruebas Automatizadas (Tests)

El directorio `tests/` agrupa todos los proyectos de pruebas unitarias, de integración y de interfaz de usuario del sistema **Retail**, estructurados para garantizar la máxima cobertura, prevenir regresiones y validar el estricto cumplimiento de los requisitos funcionales (`RF-*`) y no funcionales (`RNF-*`).

---

## 🎯 Objetivos de la Estrategia de Pruebas

1. **Pruebas Unitarias de Dominio Puras (Sin I/O ni dependencias externas):**
   Verificar las reglas de negocio, fórmulas matemáticas (Markup %, arqueo ciego) e invariantes en microsegundos.
2. **Pruebas Unitarias de Aplicación (Aisladas con Mocks/Stubs):**
   Verificar la orquestación de casos de uso y la lógica de validación de `FluentValidation` simulando repositorios y servicios externos con `NSubstitute` o `Moq`.
3. **Pruebas de Integración de API (End-to-End en Backend):**
   Probar el pipeline completo de ASP.NET Core mediante `WebApplicationFactory`, verificando autenticación JWT, middlewares de sesión única, transacciones ACID en base de datos y códigos de estado HTTP.
4. **Pruebas Unitarias de ViewModels de Cliente (WPF):**
   Verificar comandos, cambios de propiedades observables, lógica de cobro y manejo de errores en el cliente de escritorio.

---

## 📁 Estructura del Directorio

```text
tests/
├── Retail.Domain.UnitTests/             # Pruebas de Entidades, Fórmulas e Invariantes
│   ├── ArticuloTests.cs                 # Descuento de stock, markup y servicios
│   ├── TurnoCajaTests.cs                # Arqueo ciego, faltante/sobrante y estados
│   ├── VentaTests.cs                    # Cobro multimedio, validación de total
│   ├── PresupuestoTests.cs              # Caducidad de 15 días sin reserva de stock
│   └── SesionActivaTests.cs             # Reemplazo de sesión y token hash
├── Retail.Application.UnitTests/        # Pruebas de Servicios de Aplicación y Validadores
│   ├── VentaServiceTests.cs             # Orquestación de venta y descuento atómico
│   ├── AuthServiceTests.cs              # Flujo de login, kick-out y verificación de roles
│   ├── CajaServiceTests.cs              # Cálculo de balance teórico vs. declarado
│   ├── FiscalServiceTests.cs            # Encolado en Channel FIFO y manejo de contingencia
│   └── Validators/                      # Pruebas de reglas de FluentValidation
│       ├── CrearVentaValidatorTests.cs
│       └── AperturaTurnoValidatorTests.cs
├── Retail.Server.Api.IntegrationTests/  # Pruebas de Integración con WebApplicationFactory
│   ├── AuthEndpointsTests.cs            # /api/auth (Login, Token JWT, Kick-out 401)
│   ├── VentasEndpointsTests.cs          # /api/ventas (Transacciones ACID, concurrencia)
│   ├── CajaEndpointsTests.cs            # /api/caja (Apertura y Arqueo Ciego)
│   └── Middlewares/
│       └── SessionValidationMiddlewareTests.cs
└── Retail.Client.Wpf.UnitTests/         # Pruebas de ViewModels y Servicios de Cliente
    ├── PosViewModelTests.cs             # Agregado de ítems, lector de barras y total
    ├── LoginViewModelTests.cs           # Manejo de credenciales y diálogo de desalojo
    ├── PollyPoliciesTests.cs            # Verificación de reintentos ante fallos de red
    └── SessionManagerTests.cs           # Almacenamiento seguro de token en memoria
```

---

## 🧩 Ejemplos de Pruebas Representativas

### 1. Prueba Unitaria de Dominio (`Retail.Domain.UnitTests/ArticuloTests.cs`)
```csharp
[Fact]
public void DescontarStock_CuandoStockEsSuficiente_DebeDisminuirStockActual()
{
    // Arrange
    var articulo = new Articulo("779123456", "Cuaderno Rivadavia 100h", 1, 1, null, 1500m, 30m, 1950m, 10, 2, false);

    // Act
    articulo.DescontarStock(3);

    // Assert
    articulo.StockActual.Should().Be(7);
}

[Fact]
public void DescontarStock_CuandoStockEsInsuficiente_DebeLanzarStockInsuficienteException()
{
    // Arrange
    var articulo = new Articulo("779123456", "Cuaderno Rivadavia 100h", 1, 1, null, 1500m, 30m, 1950m, 2, 1, false);

    // Act & Assert
    var act = () => articulo.DescontarStock(5);
    act.Should().Throw<StockInsuficienteException>();
}
```

### 2. Prueba Unitaria de Aplicación (`Retail.Application.UnitTests/VentaServiceTests.cs`)
```csharp
[Fact]
public async Task RegistrarVenta_SinTurnoCajaAbierto_DebeLanzarCajaCerradaException()
{
    // Arrange
    var cajaRepo = Substitute.For<ICajaRepository>();
    cajaRepo.GetTurnoActivoPorUsuarioAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns((TurnoCaja?)null);

    var service = new VentaService(cajaRepo, ...);
    var dto = new CrearVentaDto { /* ... */ };

    // Act & Assert
    await Assert.ThrowsAsync<CajaCerradaException>(() => 
        service.RegistrarVentaAsync(dto, 1, CancellationToken.None));
}
```

### 3. Prueba de Integración (`Retail.Server.Api.IntegrationTests/AuthEndpointsTests.cs`)
```csharp
public class AuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ConCredencialesValidas_DebeRetornar200YTokenJwt()
    {
        // Arrange
        var request = new LoginRequestDto { NombreUsuario = "cajero1", Password = "Password123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        result.Should().NotBeNull();
        result!.TokenJwt.Should().NotBeNullOrWhiteSpace();
    }
}
```

---

## 🛠️ Herramientas y Librerías de Testing

| Herramienta | Rol | Justificación |
| :--- | :--- | :--- |
| **xUnit** | Framework de Testing | Estándar de la industria en .NET con paralelización nativa. |
| **FluentAssertions** | Aserciones Expresivas | Sintaxis clara y legible (`result.Should().Be(...)`). |
| **NSubstitute** | Mocking | Creación rápida de dobles de prueba con sintaxis concisa y tipada. |
| **Microsoft.AspNetCore.Mvc.Testing** | Integración de API | Host en memoria de ASP.NET Core (`WebApplicationFactory`) para pruebas reales del pipeline HTTP. |
