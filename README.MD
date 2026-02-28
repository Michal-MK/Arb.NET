# Arb.NET

This project adds .arb localization support to .NET applications.

RESX files suck:
1. Are IDE dependent - the generated code is inconsistent
    - uses xml which belongs to 1980s
    - each IDE generates the code differently
    - each IDE sorts the entries differently
    - ResXManager is trying to make it manageable but I still hated it.
    + Is the standard, but we are about to change that :D

2. Does not even support pluralization

## How to:

1. Install the packge of your choice. (Arb.NET.Generator for automatic source generated approach or Arb.NET.Tool for manual generation)

2. Create a `l10n.yaml` file in the root of your project with the following content:
```yaml
arb-dir: arbs # [REQUIRED] relative path to the directory containing your .arb files
template-arb-file: en.arb # [REQUIRED] the template .arb file (primary locale) that contains all the keys
output-class: L # [REQUIRED] the name of the generated C# class that will contain the localization members
output-namespace: MyProject.Locale # [REQUIRED] the namespace of the generated C# class
```

3. Create your .arb files in the specified directory (e.g. `arbs/en.arb`, `arbs/cs.arb`, etc.) with the following content:
```json
{
  "@@locale": "en", # [REQUIRED] the locale of this .arb file
  "KeyName": "Localized Value" # entries in this format
  "ParametricKey": "Value with {param} placeholder" # for parametric keys, use {<name>} syntax
  "PluralKey": "{count, plural, one {One item} other {{count} items}}" # for pluralization, use ICU syntax
}
```

4. If using `Arb.NET.Generator`, the code will be generated automatically at build time. If using `Arb.NET.Tool`, run the tool to generate the code manually.

5. Profit!

## Projects:

`Arb.NET` - shared lib for the lang code generation and the runtime support.

`Arb.NET.Tool` - an app to be installed as a tool and ran as a executable in projects that use Arb.NET. It will generate the lang code from the .arb files.

`Arb.NET.Generator` - Roslyn source generator that will generate the lang code from the .arb files at design time.

`Arb.NET.Examples/*` - example projects to show how to use the library in different types of applications, currently a CLI example only.

`Arb.NET.Test` - a test project to test the code generation and the runtime support. It will be used to test the generated code and the runtime support. It will also be used to test the integration with the MAUI app.

### IDE support (WIP):

`Arb.NET.IDE.Jetbrains` - a plugin for Rider and Resharper

`Arb.NET.IDE.VisualStudio` - a plugin for Visual Studio


## Future:
`Arb.NET.Analyzers` - analyzer project that will aid users in some way etc...

## License:
**I do not care licence.** (MIT) You can credit me. You can support me. I am not liable for anything.