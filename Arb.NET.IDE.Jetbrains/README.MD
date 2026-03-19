# Arb.NET Rider Plugin

Rider plugin for [Arb.NET](https://github.com/Michal-MK/Arb.NET) — ARB (Application Resource Bundle) localization support for .NET.

## Features

- **ARB Editor** — side-by-side multi-locale editing with filtering, CSV import/export, and AI/Google translation
- **XAML Integration** — code completion, hover documentation, syntax highlighting, and Go-to-Definition for ARB keys inside XAML markup extensions
- **Inspections** — warnings for missing or unused ARB keys with quick-fixes
- **Navigation** — Ctrl+Click / F12 from XAML markup to the ARB editor, context menu on `.arb` files and folders

## Requirements

- JetBrains Rider 2025.3+
- A project using [Arb.NET](https://github.com/Michal-MK/Arb.NET) with a `l10n.yaml` configuration file

## Development Setup

1. Place `nuget.exe` inside `Arb.NET.IDE.Jetbrains/tools/` (next to `vswhere.exe` on Windows)
2. `runIde` goes through `prepareSandbox`, so .NET backend binaries are copied before launch
3. If launch fails on Windows due to toolchain issues, verify Visual Studio/MSBuild are installed and discoverable by `vswhere.exe`
4. After protocol model changes, run: `cd "./Arb.NET.IDE.Jetbrains" && ./gradlew :protocol:rdgen 2>&1`

## License

MIT - See the [LICENSE](../LICENSE) file in the repository root.
