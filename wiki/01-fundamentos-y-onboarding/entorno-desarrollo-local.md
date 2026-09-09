# Configuración del Entorno de Desarrollo Local

### Módulo: 01. Fundamentos y Onboarding
**Audiencia:** Desarrolladores humanos, nuevos colaboradores y docentes evaluadores  
**Objetivo:** Levantar y verificar la solución `Retail.sln` desde cero en una máquina Windows limpia  

---

## 1. 📋 Requisitos Previos de la Estación de Trabajo

Para compilar, depurar y ejecutar las pruebas de Retail, tu máquina debe cumplir con:

* **Sistema Operativo:** Windows 10 (versión 21H2+) o Windows 11 (x64 recomendado).
* **SDK de .NET:** .NET 8.0 SDK (versión `8.0.x` LTS o superior). Verifica con `dotnet --version`.
* **Motor de Base de Datos Local:** SQL Server LocalDB (`(localdb)\mssqllocaldb`), instalado automáticamente con Visual Studio o descargable con SQL Server Express.
* **Entornos de Desarrollo Recomendados:**
  * Visual Studio 2022 (v17.8+ con la carga de trabajo *"Desarrollo de escritorio de .NET"*).
  * JetBrains Rider 2023.3+ (con plugins de .NET y XAML).
  * VS Code con las extensiones *"C# Dev Kit"* y *"C#"*.
* **Lector de Documentación:** [Obsidian](https://obsidian.md/) para navegar esta wiki de forma relacional.

---

## 2. 🚀 Paso a Paso: Del Clonado al Mostrador Funcionando

### Paso 1: Clonar el Repositorio
Abre PowerShell o tu terminal favorita y clona el código fuente:
```powershell
git clone https://github.com/lucaskruzolek/retail.git
cd retail
```

### Paso 2: Verificar la Cadena de Herramientas de .NET
Comprueba que el SDK de .NET 8 esté activo en el path:
```powershell
dotnet --info
```

### Paso 3: Restaurar Paquetes y Compilar la Solución
Compila en modo `Debug` para verificar que no existan advertencias ni dependencias faltantes:
```powershell
dotnet build Retail.sln
```
> [!NOTE]
> El archivo [`Directory.Build.props`](file:///c:/Users/lucas/Proyectos/retail/Directory.Build.props) impone `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Si la compilación termina con código `0`, el código cumple con el estándar de cero advertencias.

### Paso 4: Ejecutar la Suite Completa de Tests Automatizados
Antes de ejecutar la interfaz gráfica, corre las pruebas unitarias y de integración para asegurar que las reglas de negocio y los contratos de persistencia estén en verde:
```powershell
dotnet test Retail.sln
```

### Paso 5: Ejecutar la Aplicación de Escritorio
Inicia el proyecto de presentación WPF:
```powershell
dotnet run --project src/Retail.App
```
Al iniciarse por primera vez:
1. El inicializador de base de datos (`DbInitializer.cs`) detectará que la base LocalDB no existe y aplicará las migraciones de Entity Framework Core automáticamente.
2. Se poblará el catálogo de prueba (roles, usuario `admin`, categorías y artículos con precios y stock inicial).
3. Se abrirá la ventana principal de Retail lista para operar.

---

## 3. ⚙️ Variables de Configuración (`appsettings.json`)

El archivo de configuración local reside en [`src/Retail.App/appsettings.json`](file:///c:/Users/lucas/Proyectos/retail/src/Retail.App/appsettings.json):

```json
{
  "ConnectionStrings": {
    "RetailDbConnection": "Server=(localdb)\\mssqllocaldb;Database=RetailDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Fiscal": {
    "UseMockArca": true,
    "PuntoVenta": 1
  },
  "Hardware": {
    "UseMockPrinter": true,
    "TicketOutputDirectory": "tickets"
  }
}
```

* `"UseMockArca": true`: Permite operar sin certificados fiscales reales de AFIP/ARCA ni conexión a servidores de homologación. Emite un CAE simulado determinístico.
* `"UseMockPrinter": true`: Escribe la salida física del ticket de venta en archivos de texto dentro de la carpeta `tickets/` para inspeccionar su diseño visual sin requerir una impresora térmica física conectada a un puerto COM.

---

## 4. 🧰 Comandos de Rutina para el Desarrollador (Inner Loop)

```powershell
# Compilación estricta en Release (idéntica a la aduana de CI de GitHub Actions)
dotnet build Retail.sln --configuration Release

# Ejecutar pruebas con reporte de resultados
dotnet test Retail.sln --configuration Release --no-build

# Verificar que el código respete el formateo de .editorconfig (Allman style)
dotnet format Retail.sln --verify-no-changes
```
