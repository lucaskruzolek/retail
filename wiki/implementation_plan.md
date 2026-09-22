# Plan de Implementación: Módulo 1.1 - Autenticación, Seguridad y Shell Base de Mostrador

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Responsable:** Pablo Fernandez  
**Épica:** Etapa 1 - Autenticación, RBAC y Shell de Navegación  
**Requisitos Vinculados:** [`RF-01`](file:///c:/Users/pablo/retail/docs/ERS%20-%20Libreria%20POS.md#L225) (Autenticación y Sesión), [`RF-02`](file:///c:/Users/pablo/retail/docs/ERS%20-%20Libreria%20POS.md#L226) (Gestión de Sesión Local sin reinicio), [`RNF-04`](file:///c:/Users/pablo/retail/docs/ERS%20-%20Libreria%20POS.md#L253) (Criptografía BCrypt), [`RNF-06`](file:///c:/Users/pablo/retail/docs/ERS%20-%20Libreria%20POS.md#L255) (RBAC en memoria y UI adaptativa).

---

## 🎯 Resumen Ejecutivo

El Módulo 1.1 establece la puerta de entrada segura y el marco de navegación unificado para todo el sistema de escritorio Retail POS. Permite que los operadores inicien sesión mediante credenciales protegidas con hash BCrypt, mantiene el contexto del usuario activo en memoria (`CurrentUserSession`), adapta las opciones de navegación según el rol asignado (Cajero, Encargado, Gerente) y provee un Shell ergonómico de mostrador (`MainWindow`) con cambio rápido de usuario en menos de 15 ms sin reiniciar la aplicación.

---

## ⚖️ Decisiones de Diseño y Reglas de Arquitectura

1. **Clean Architecture e Inversión de Dependencias (Ley 1 & Ley 10):**
   * La lógica de login reside en `Retail.Application` (`AuthService`), validada con `FluentValidation`.
   * El servicio de sesión `CurrentUserSession` y el de navegación `NavigationService` son servicios de presentación `Singleton` en `Retail.App`.
   * `MainWindow` permanece estrictamente como `Singleton`. Las vistas secundarias se instancian a demanda mediante el contenedor DI a través de `NavigationService`.
2. **Seguridad en Mostrador y PasswordBox:**
   * Por seguridad del runtime de WPF, `PasswordBox.Password` no admite data binding bidireccional estándar (evita retener credenciales en texto plano en la memoria del Garbage Collector). El `LoginViewModel` recibe la contraseña de forma segura mediante parámetro en el comando `[RelayCommand]`.
3. **Cambio Rápido de Operador (`RF-02`):**
   * `MainViewModel` permite cerrar la sesión activa y desplegar el diálogo modal de login. Al autenticar un nuevo operador, se actualiza `CurrentUserSession` y la UI del Shell se recompone reactivamente en $< 15$ ms sin matar el proceso.
4. **Sistema de Diseño Windows 11 Fluent (`docs/SISTEMA_DE_DISENO.md`):**
   * `LoginWindow` y `MainWindow` utilizan `ui:FluentWindow` con efecto Mica, `ui:TitleBar`, paleta Borravino (`#9D0F33`) y tipografía dual (`Segoe UI Variable` y `Cascadia Code`).

---

## 📂 Cambios Propuestos

### 1. Capa de Aplicación (`src/Retail.Application`)

#### [NEW] [`src/Retail.Application/Validators/Auth/LoginRequestValidator.cs`](file:///c:/Users/pablo/retail/src/Retail.Application/Validators/Auth/LoginRequestValidator.cs)
* Validador declarativo con FluentValidation para [`LoginRequestDto`](file:///c:/Users/pablo/retail/src/Retail.Application/DTOs/Auth/LoginRequestDto.cs).
* Reglas: `NombreUsuario` no vacío, longitud entre 3 y 50 caracteres; `Password` no vacío, longitud mínima 4 caracteres.

#### [NEW] [`src/Retail.Application/Services/AuthService.cs`](file:///c:/Users/pablo/retail/src/Retail.Application/Services/AuthService.cs)
* Implementa [`IAuthService`](file:///c:/Users/pablo/retail/src/Retail.Application/Interfaces/Services/IAuthService.cs).
* Inyecta `IRepository<Usuario>`, `IPasswordHasher` y `IValidator<LoginRequestDto>`.
* Ejecuta validación previa, búsqueda insensible a mayúsculas con `includeDeleted: true`.
* Si no existe el usuario: lanza [`CredencialesInvalidasException`](file:///c:/Users/pablo/retail/src/Retail.Domain/Exceptions/CredencialesInvalidasException.cs).
* Si el usuario está dado de baja (`IsDeleted`): lanza [`UsuarioInactivoException`](file:///c:/Users/pablo/retail/src/Retail.Domain/Exceptions/UsuarioInactivoException.cs).
* Valida el hash con `_passwordHasher.Verify()`. Si no coincide: lanza `CredencialesInvalidasException`.
* Retorna [`LoginResultDto`](file:///c:/Users/pablo/retail/src/Retail.Application/DTOs/Auth/LoginResultDto.cs).

#### [MODIFY] [`src/Retail.Application/DependencyInjection.cs`](file:///c:/Users/pablo/retail/src/Retail.Application/DependencyInjection.cs)
* Registrar `services.AddScoped<IAuthService, AuthService>();`.

---

### 2. Pruebas Unitarias de Aplicación (`tests/Retail.Application.UnitTests`)

#### [NEW] [`tests/Retail.Application.UnitTests/Validators/LoginRequestValidatorTests.cs`](file:///c:/Users/pablo/retail/tests/Retail.Application.UnitTests/Validators/LoginRequestValidatorTests.cs)
* Casos BDD: Nombre de usuario vacío/nulo, contraseña vacía/nula, request con formato válido.

#### [NEW] [`tests/Retail.Application.UnitTests/Services/AuthServiceTests.cs`](file:///c:/Users/pablo/retail/tests/Retail.Application.UnitTests/Services/AuthServiceTests.cs)
* Casos BDD con NSubstitute y FluentAssertions:
  * `LoginAsync_CredencialesValidas_RetornaLoginResultDto`
  * `LoginAsync_UsuarioInexistente_LanzaCredencialesInvalidasException`
  * `LoginAsync_PasswordInvalido_LanzaCredencialesInvalidasException`
  * `LoginAsync_UsuarioInactivo_LanzaUsuarioInactivoException`
  * `LoginAsync_RequestInvalida_LanzaValidationException`

---

### 3. Servicios de UI y Gestión de Sesión (`src/Retail.App/Services`)

#### [NEW] [`src/Retail.App/Services/ICurrentUserSession.cs`](file:///c:/Users/pablo/retail/src/Retail.App/Services/ICurrentUserSession.cs)
* Contrato para el estado de sesión en memoria: `UsuarioActual`, `EstaAutenticado`, `EsGerente`, `EsEncargado`, `EsCajero`, evento `SessionChanged`.

#### [NEW] [`src/Retail.App/Services/CurrentUserSession.cs`](file:///c:/Users/pablo/retail/src/Retail.App/Services/CurrentUserSession.cs)
* Implementación `Singleton` de `ICurrentUserSession`.
* Métodos `EstablecerSesion(LoginResultDto)` y `CerrarSesion()`.

#### [NEW] [`src/Retail.App/Services/INavigationService.cs`](file:///c:/Users/pablo/retail/src/Retail.App/Services/INavigationService.cs)
* Contrato de navegación desacoplada: `Initialize(Frame frame)`, `NavigateTo<TView>()`, `NavigateTo(Type viewType)`, `CanGoBack`, `GoBack()`.

#### [NEW] [`src/Retail.App/Services/NavigationService.cs`](file:///c:/Users/pablo/retail/src/Retail.App/Services/NavigationService.cs)
* Implementación `Singleton` que orquesta la carga de páginas dentro del `Frame` de `MainWindow` resolviendo vistas desde `IServiceProvider`.

---

### 4. Capa de Presentación: Pantalla de Login (`src/Retail.App`)

#### [NEW] [`src/Retail.App/ViewModels/Auth/LoginViewModel.cs`](file:///c:/Users/pablo/retail/src/Retail.App/ViewModels/Auth/LoginViewModel.cs)
* ViewModel basado en `CommunityToolkit.Mvvm`.
* Propiedades observables: `NombreUsuario`, `MensajeError`, `TieneError`, `EstaCargando`.
* Comando `[RelayCommand] IniciarSesionAsync(object? parameter)`:
  * Extrae la contraseña del `PasswordBox`.
  * Invoca `_authService.LoginAsync()`.
  * En éxito: establece `_session.EstablecerSesion(resultado)` y dispara evento `LoginExitoso`.
  * En error: exhibe mensajes amigables sin cerrar la ventana.

#### [NEW] [`src/Retail.App/Views/Auth/LoginWindow.xaml`](file:///c:/Users/pablo/retail/src/Retail.App/Views/Auth/LoginWindow.xaml) y [`.cs`](file:///c:/Users/pablo/retail/src/Retail.App/Views/Auth/LoginWindow.xaml.cs)
* Ventana `ui:FluentWindow` con Mica backdrop, `ui:TitleBar` y layout centrado.
* Tarjeta Fluent con marca Carmín (`PrimaryBrush`), campos de texto de alta legibilidad y botón de inicio `PrimaryActionButtonStyle`.
* Soporte de tecla `Enter` para inicio rápido y feedback visual de carga.

---

### 5. Capa de Presentación: Shell Principal de Mostrador (`src/Retail.App`)

#### [NEW] [`src/Retail.App/ViewModels/MainViewModel.cs`](file:///c:/Users/pablo/retail/src/Retail.App/ViewModels/MainViewModel.cs)
* Inyecta `ICurrentUserSession`, `INavigationService` y `IServiceProvider`.
* Propiedades observables que reaccionan a `SessionChanged`:
  * `OperadorNombre`, `OperadorRolBadge`, `EsGerente`, `EsEncargado`, `EsCajero`.
  * `TituloModuloActual`.
* Comandos:
  * `NavegarCommand(string destino)`: Conmuta hacia `ArticulosView`, `UsuariosView` (solo si es Gerente), o `StyleGalleryView`.
  * `CambiarUsuarioCommand`: Desconecta la sesión actual y solicita nuevo login modal sin reiniciar el ejecutable (`RF-02`).

#### [MODIFY] [`src/Retail.App/MainWindow.xaml`](file:///c:/Users/pablo/retail/src/Retail.App/MainWindow.xaml) y [`.cs`](file:///c:/Users/pablo/retail/src/Retail.App/MainWindow.xaml.cs)
* Transición de arnés temporal de la Etapa 0 a Shell formal de mostrador:
  * **Barra de Título (`ui:TitleBar`):** Muestra el nombre de la app, pastilla del operador activo con badge de rol semántico y botón de cambio rápido de operador `[🔁]`.
  * **Barra de Navegación de Mostrador:** Botonera con teclas de función (`F2` Artículos, `F10` Operadores [restringido por RBAC], Galería Dev).
  * **Contenedor Dinámico:** `<Frame x:Name="RootFrame" NavigationUIVisibility="Hidden" />` administrado por `NavigationService`.
  * **Barra de Estado Inferior:** Versión del sistema, estado del motor LocalDB y atajos disponibles.

---

### 6. Orquestación del Host y Ciclo de Vida (`src/Retail.App/App.xaml.cs`)

#### [MODIFY] [`src/Retail.App/App.xaml.cs`](file:///c:/Users/pablo/retail/src/Retail.App/App.xaml.cs)
* Registro de dependencias en `ConfigureServices`:
  * `ICurrentUserSession, CurrentUserSession` (Singleton).
  * `INavigationService, NavigationService` (Singleton).
  * `LoginViewModel`, `LoginWindow` (Transient).
  * `MainViewModel` (Singleton).
* En `OnStartup`:
  * Mantener inicialización de base de datos (`MigrateAsync`, `DbInitializer`).
  * Desplegar `LoginWindow` al iniciar.
  * Si la autenticación es exitosa, mostrar `MainWindow`, vincular `NavigationService` al `RootFrame` y navegar a la página inicial predeterminada (`ArticulosView`).
  * Si el usuario cancela o cierra el login sin autenticarse, cerrar la aplicación limpiamente.

---

### 7. Pruebas Unitarias de Presentación (`tests/Retail.App.UnitTests`)

#### [NEW] [`tests/Retail.App.UnitTests/Services/CurrentUserSessionTests.cs`](file:///c:/Users/pablo/retail/tests/Retail.App.UnitTests/Services/CurrentUserSessionTests.cs)
* Verificación de estado inicial, cambio de usuario, asignación de permisos RBAC y notificación de eventos.

#### [NEW] [`tests/Retail.App.UnitTests/ViewModels/LoginViewModelTests.cs`](file:///c:/Users/pablo/retail/tests/Retail.App.UnitTests/ViewModels/LoginViewModelTests.cs)
* Verificación de comandos, manejo de errores de credenciales y propagación de sesión exitosa.

---

## 🧪 Plan de Verificación

### Pruebas Automatizadas
1. **Compilación estricta en Release (cero advertencias):**
   ```powershell
   dotnet build Retail.sln --configuration Release
   ```
2. **Suite completa de pruebas unitarias e integración:**
   ```powershell
   dotnet test Retail.sln --configuration Release --no-build
   ```
3. **Alineación de formato Allman y .editorconfig:**
   ```powershell
   dotnet format Retail.sln --verify-no-changes
   ```

### Verificación Manual de Flujo de Mostrador
1. Ejecutar la aplicación con `dotnet run --project src/Retail.App`.
2. Verificar que se presenta la ventana `LoginWindow`.
3. Probar ingreso con credenciales inválidas (`admin` / `erronea`) -> verificar badge de error sin cierre de app.
4. Probar ingreso con credenciales correctas (`admin` / `Admin123!`) -> verificar apertura fluida de `MainWindow`.
5. Comprobar que en la barra superior figura `admin` con el badge `Gerente`.
6. Navegar entre `Artículos [F2]` y `Operadores [F10]`.
7. Probar el botón de "Cambiar Operador": debe permitir loguear otro usuario (o re-autenticar) en $< 15$ ms sin reiniciar el proceso.
