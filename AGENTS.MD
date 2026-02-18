## Genereal readme
@Arb.NET/README.MD

## Project structure

`Arb.NET` - the core project, contains models and logic for parsing and generating code from .arb files.
`Arb.NET.Tool` - a console application that can be installed as a dotnet tool, used to generate code manually from .arb files in projects that use Arb.NET.
`Arb.NET.Generators` - Roslyn source generators that can be used to generate code from .arb files at design time.
`Arb.NET.Test` - a test project
`Arb.NET.Examples.*` - common prefix for example projects, examples use the Roslyn source generators

## IDE Support
`Arb.NET.IDE.Jetbrains` - a plugin for Rider and Resharper
`Arb.NET.IDE.VisualStudio` - a plugin for Visual Studio

`.editorconfig` - is present in the project!
`global.json` - is present in the project!

## Hot files

- `ArbCodeGenerator.cs` - the main source generator that generates code from .arb files at design time.
- `ArbParser.cs` - the main parser that parses .arb files and returns a model that can be used to generate code from it.
- `ArbSerializer.cs` - the main serializer that can serialize the model back to .arb files.

### Jetbrains specifics
- You are dealing with Rider/Resharper which is using both C# code and Kotlin code. Jetbrains IDEs are using the shared JVM runtime.
- The C# specific things live in a separate process. Communication between the two is done via the Rider Protocol.
- Not much documentation exists online, but there are examples in the ai-ref/ folder. (ignored by git, if empty the user did not provide any)

- To run the protocol generator:
`cd "./Arb.NET/Arb.NET.IDE.Jetbrains" && ./gradlew :protocol:rdgen 2>&1`

## Shell environment detection
- Run `uname` to detect the shell environment:
  - **Success** → bash/zsh/sh
  - **Failure (not recognized)** → PowerShell
- Use only commands from the respective shell environment to ensure compatibility and avoid issues with command execution!

## Logging
~~~~
When adding logging code, determine if the log will be useful to keep or only used for debugging in the current session. Prefix such logs with "[AI-DEBUG]" so that they can easily removed after.

## Nuget Package
- This project produces two nuget packages: `Arb.NET` and `Arb.NET.Generators`.
- Any changes from now on must accept that we could break someone! An attempt to minimize making such changes must be made.

At the start of each session print:
~~~~```
I will follow the content of this file to my best ability.
Determining shell environment... ## Shell environment detection
```