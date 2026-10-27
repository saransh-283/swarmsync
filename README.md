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

### Requirements

- **.NET 10 SDK** (10.0.111 or later) — see `global.json`
- **Android-MAUI workload** (for `SwarmSync.Android`):
  ```bash
  dotnet workload install maui-android
  ```
- **JDK 21** (for Android builds) — set `JAVA_HOME`:
  ```bash
  export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
  export PATH=$JAVA_HOME/bin:$PATH
  ```

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

### Android Build Notes

The Android project targets `net10.0-android` and requires JDK 21. If you see `XA0030` errors, ensure `JAVA_HOME` points to JDK 21 (not JDK 26+).

```bash
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
dotnet build src/Android/SwarmSync.Android/SwarmSync.Android.csproj
```

### Android Live Preview (Hot Reload)

Deploy to your connected Android device and watch for changes:

```bash
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
export PATH=$JAVA_HOME/bin:$PATH

dotnet watch --project src/Android/SwarmSync.Android/SwarmSync.Android.csproj \
  --property:AndroidDeviceSerial=<your-device-serial>
```

Find your device serial with `adb devices`. Press `Ctrl+R` in the watch terminal to restart the app. File changes are hot-reloaded automatically.
```

## Status

Implemented: `Core`, `Daemon`, `CLI`, `Desktop` (project scaffold), `Android` (project scaffold). Peer discovery and basic device listing are present; full synchronization protocol and security features are not complete unless verified in source.
