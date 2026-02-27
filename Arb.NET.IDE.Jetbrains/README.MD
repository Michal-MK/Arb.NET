Inside Arb.NET\Arb.NET.IDE.Jetbrains\tools you need to put `nuget.exe`, next to `vswhere.exe` for windows.

- `runIde` goes through `prepareSandbox`, so .NET backend binaries are copied before launch.
- If launch fails on Windows due to toolchain issues, verify Visual Studio/MSBuild are installed and discoverable by `vswhere.exe`.
- Use `Regenerate Rider Protocol (rdgen)` after protocol model changes.