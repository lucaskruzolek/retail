# Etapa 1: Épica 1 - Autenticación, RBAC y Shell de Navegación

### Proyecto: Retail (Sistema ERP & Punto de Venta para Librería)
**Documento Maestro:** [`Roadmap de Implementacion.md`](file:///c:/Users/lucas/Proyectos/retail/docs/Roadmap%20de%20Implementacion.md) | **Mapa Semántico:** [`MAPA_DEL_PROYECTO.md`](file:///c:/Users/lucas/Proyectos/retail/docs/MAPA_DEL_PROYECTO.md)  
**Requisitos Vinculados:** [`RF-01`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L225), [`RF-02`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L226), [`RF-03`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L227), [`RNF-04`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L253), [`RNF-06`](file:///c:/Users/lucas/Proyectos/retail/docs/ERS%20-%20Libreria%20POS.md#L255)

---

> **Objetivo:** Garantizar el acceso seguro de operadores mediante credenciales protegidas con hash, control de acceso basado en roles (RBAC) y un shell de escritorio ergonómico con sesión activa y cambio rápido de usuario.

## Módulos e Interfaces Asignados

### Módulo 1.1: Autenticación, Seguridad y Shell Base de Mostrador
**Responsable:** Pablo Fernandez

* **Interfaz Visual:** `LoginWindow.xaml` (pantalla de inicio con `PasswordBox` protegido y feedback de credenciales) y `MainWindow.xaml` (Shell principal con contenedor de páginas `Frame`, barra superior con operador activo, turno y botón de cambio de sesión).
* **ViewModels y UI Services:** `LoginViewModel.cs`, `MainViewModel.cs`, `CurrentUserSession.cs` y `NavigationService.cs` para cambio de usuario sin reiniciar el ejecutable (`RF-02`).
* **Lógica y Casos de Uso:** `IAuthService.LoginAsync`, DTOs de login y validadores FluentValidation.
* **Dominio y Seguridad:** Entidad `Usuario` (`IAggregateRoot`), `Rol`, `PasswordHasher` con BCrypt (`BCrypt.Net-Next`) para hashing seguro con salt (`RNF-04`).
* **Persistencia:** `UsuarioConfiguration.cs` y `RolConfiguration.cs` en EF Core.
* **Testing:** Pruebas unitarias de hashing y autenticación en `AuthServiceTests.cs`; pruebas unitarias de interfaz en `LoginViewModelTests.cs`.

---

### Módulo 1.2: Administración de Usuarios y Roles RBAC (Estado: ✅ 100% Completado)
**Responsable:** Lucas Kruzolek

* **Interfaz Visual:** [`UsuariosView.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Pages/UsuariosView.xaml) (panel gerencial con grilla de operadores, badges semánticos carmín/neutro), [`UsuarioFormDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/UsuarioFormDialog.xaml) (diálogo modal de alta/edición) y [`CambiarPasswordDialog.xaml`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Views/Dialogs/CambiarPasswordDialog.xaml) (restablecimiento seguro de contraseña).
* **ViewModels y Servicios UI:** [`UsuariosViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Usuarios/UsuariosViewModel.cs), [`UsuarioFormViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Usuarios/UsuarioFormViewModel.cs), [`CambiarPasswordViewModel.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/ViewModels/Usuarios/CambiarPasswordViewModel.cs), [`IUsuarioDialogService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Services/IUsuarioDialogService.cs) y [`UsuarioDialogService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/Services/UsuarioDialogService.cs).
* **Lógica y Casos de Uso:** [`IUsuarioService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Interfaces/Services/IUsuarioService.cs), [`UsuarioService.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Services/UsuarioService.cs). Validadores FluentValidation: [`CrearUsuarioValidator.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Usuarios/CrearUsuarioValidator.cs), [`ModificarUsuarioValidator.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Usuarios/ModificarUsuarioValidator.cs), [`CambiarPasswordValidator.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Validators/Usuarios/CambiarPasswordValidator.cs).
* **Dominio:** `RolUsuarioEnum` (Cajero, Encargado, Gerente), reglas de baja lógica (`MarkAsDeleted`), métodos [`ActualizarDatos`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Usuario.cs), [`ActualizarPassword`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Entities/Usuario.cs) e invariante bidireccional que impide eliminar o degradar de rol al último Gerente activo del sistema ([`UltimoGerenteException.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Domain/Exceptions/UltimoGerenteException.cs)).
* **Persistencia:** Consultas filtradas de usuarios activos en EF Core, Filtered Unique Index ([`[deleted_at] IS NULL`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Infrastructure/Persistence/Configurations/UsuarioConfiguration.cs)) para aislamiento de identidad contable en re-registros, y soporte de filtros globales en [`IRepository.cs`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.Application/Interfaces/Persistence/IRepository.cs).
* **Testing:** Pruebas de reglas de negocio de entidad en [`UsuarioTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Domain.UnitTests/Entities/UsuarioTests.cs) (6 tests); orquestación y validadores en [`UsuarioServiceTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Application.UnitTests/Services/UsuarioServiceTests.cs) (14 tests); comandos, filtrado y diálogos MVVM en [`UsuariosViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/UsuariosViewModelTests.cs) (12 tests); formulario y sanitización en [`UsuarioFormViewModelTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.App.UnitTests/ViewModels/UsuarioFormViewModelTests.cs) (5 tests) e integración de persistencia real en [`RetailDbContextTests.cs`](file:///c:/Users/lucas/Proyectos/retail/tests/Retail.Infrastructure.IntegrationTests/RetailDbContextTests.cs).


---

## Prevención de Sobreescritura y Criterio de Aceptación
* **Prevención de Sobreescritura:** Pablo define el Shell `MainWindow` y el contenedor de navegación. Lucas desarrolla `UsuariosView.xaml` de forma completamente autónoma y la conecta como página de destino en el `NavigationService`.
* **Criterio de Aceptación Integrado:** El operador se autentica desde `LoginWindow`; el shell principal adapta sus opciones según el rol (`RNF-06`), y el Gerente puede administrar usuarios y roles desde `UsuariosView`. El cambio de usuario se efectúa en $< 15\text{ ms}$ sin reiniciar la aplicación.
