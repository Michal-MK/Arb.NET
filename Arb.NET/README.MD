# Arb.NET

This project adds .arb localization support to .NET applications.

RESX files suck:
1. Are IDE dependent - the generated code is inconsistent
    - uses xml which belongs to 1980s
    - each IDE generates the code differently
    - each IDE sorts the entries differently
    - ResXManager is trying to make it manageable but I still hated it.

2. Does not even support pluralization  

This is a work in progress! Proof of concept!

## Projects:

`Arb.NET` - shared lib for the lang code generation and the runtime support.

`Arb.NET.Tool` - a app to be installed as a tool and ran as a executable in projects that use Arb.NET. It will generate the lang code from the .arb files.

`Arb.NET.Test` - a test project to test the code generation and the runtime support. It will be used to test the generated code and the runtime support. It will also be used to test the integration with the MAUI app.

## Future:
`Arb.NET.Analyzers` - analyzer project that will aid in generation etc...

## License:
**I do not care licence.** You can credit me. You can support me. I am not liable for anything.