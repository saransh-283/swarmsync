# SwarmSync — Project Overview

SwarmSync is an open-source, peer-to-peer synchronization platform for devices on a local network. Its core is a reusable .NET library (`SwarmSync.Core`) providing fundamental synchronization logic.

Core-first and implementation-agnostic: developers can build custom daemons, CLI tools, desktop apps, and mobile apps without being forced to use any official client.

## Architecture

- **Core (`SwarmSync.Core`)**: Reusable logic, protocol functions, public APIs. Main project.
- **Daemon (`SwarmSync.Daemon`)**: Optional persistent background service managing peer discovery and sync independently of UIs.
- **CLI (`SwarmSync.Cli`)**: Example command-line interface.
- **Desktop (`SwarmSync.Desktop`)**: Planned desktop GUI (Avalonia).
- **Android (`SwarmSync.Android`)**: Planned mobile app.

Recommendations for developers: do not have apps talk directly to `Core` for live sync; use a daemon (official or custom) so synchronization continues when the GUI/CLI is closed, and state is shared between CLI and GUI.

## Setup / Build

Requires .NET 9 SDK.

### Windows / macOS / Linux

```bash
dotnet restore
dotnet build
```

### Run

```bash
# Daemon
dotnet run --project src/Daemon/SwarmSync.Daemon

# CLI
- `--list` — One-off device list
- `--help` — Show usage
dotnet run --project src/Cli/SwarmSync.Cli
```

## Status

Implemented: `Core`, `Daemon`, `CLI`, `Desktop` (project scaffold), `Android` (project scaffold). Peer discovery and basic device listing are present; full synchronization protocol and security features are not complete unless verified in source.
