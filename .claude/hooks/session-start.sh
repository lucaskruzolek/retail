#!/bin/bash
# Instala el SDK de .NET 8 en sesiones cloud de Claude Code para poder ejecutar
# `dotnet build` y `dotnet test`. Idempotente: no hace nada si el SDK 8 ya existe.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Se usa apt (archivo de Ubuntu) porque builds.dotnet.microsoft.com
# (dotnet-install.sh) puede estar bloqueado por la política de red del entorno.
if ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  SUDO=""
  if [ "$(id -u)" -ne 0 ]; then
    SUDO="sudo"
  fi
  $SUDO apt-get update -qq
  $SUDO env DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-8.0
fi

# Persistir el entorno para el resto de la sesión
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
    # Retail.App (WPF, net8.0-windows) debe poder restaurarse/compilarse desde Linux
    echo 'export EnableWindowsTargeting=true'
  } >> "$CLAUDE_ENV_FILE"
fi

# Restaurar paquetes NuGet (se cachean en el estado del contenedor)
cd "${CLAUDE_PROJECT_DIR:-.}"
EnableWindowsTargeting=true dotnet restore Retail.sln
