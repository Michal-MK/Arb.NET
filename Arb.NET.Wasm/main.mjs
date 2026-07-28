import { dotnet } from './_framework/dotnet.js';

const { getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

export const ArbInterop = exports.Arb.NET.Wasm.ArbInterop;

await dotnet.run();
