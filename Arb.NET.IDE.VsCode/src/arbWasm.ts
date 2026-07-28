import * as path from 'path';
import { pathToFileURL } from 'url';
import * as vscode from 'vscode';

interface ArbInteropExports {
	ParseContent(content: string): string;
	Serialize(documentJson: string): string;
	BuildCsvImportPreview(localeContentsJson: string, csvContent: string, defaultLocale: string | null): string;
	ApplyCsvImport(localeContentsJson: string, csvContent: string, mappingsJson: string, mode: string): string;
	ExportCsv(localeContentsJson: string): string;
	TranslateBatch(provider: string, settingsJson: string, sourceLocale: string, targetLocale: string, itemsJson: string): Promise<string>;
	ValidateTranslationSettings(provider: string, settingsJson: string): string;
}

let cachedExports: Promise<ArbInteropExports> | undefined;

// dotnet.js ships as ESM ("type": "module"), but this extension compiles to CommonJS,
// and TypeScript's commonjs target rewrites `import()` into `require()`, which cannot
// load an ES module. Going through `new Function` forces a real dynamic import at runtime,
// bypassing tsc's transform - the standard workaround for CJS code that needs an ESM dependency.
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- dynamic import's return type is genuinely unknown here
const dynamicImport = new Function('specifier', 'return import(specifier)') as (specifier: string) => Promise<any>;

function loadArbInterop(context: vscode.ExtensionContext): Promise<ArbInteropExports> {
	if (!cachedExports) {
		const dotnetJsPath = path.join(context.extensionPath, 'wasm', '_framework', 'dotnet.js');
		cachedExports = (async () => {
			const dotnetJsUrl = pathToFileURL(dotnetJsPath).toString();
			const { dotnet } = await dynamicImport(dotnetJsUrl);
			// dotnet.js resolves sibling framework assets relative to its own module URL,
			// so no explicit resource loader is needed regardless of the extension host's cwd.
			const { getAssemblyExports, getConfig } = await dotnet
				.withDiagnosticTracing(false)
				.create();

			const config = getConfig();
			const exports = await getAssemblyExports(config.mainAssemblyName);
			return exports.Arb.NET.Wasm.ArbInterop as ArbInteropExports;
		})();
	}
	return cachedExports;
}

export interface ArbParseResult {
	validationResults: {
		isValid: boolean;
		errors: { keyword: string; message: string; instanceLocation: string; schemaPath: string }[];
	};
	document?: ArbDocument;
}

export interface ArbDocument {
	locale: string;
	context: string;
	entries: Record<string, ArbEntry>;
}

export interface ArbEntry {
	key: string;
	value: string;
	metadata?: {
		description?: string;
		placeholders?: Record<string, { type: string }>;
	};
}

export async function parseArbContent(context: vscode.ExtensionContext, content: string): Promise<ArbParseResult> {
	const interop = await loadArbInterop(context);
	return JSON.parse(interop.ParseContent(content)) as ArbParseResult;
}

export async function serializeArbDocument(context: vscode.ExtensionContext, document: ArbDocument): Promise<string> {
	const interop = await loadArbInterop(context);
	return interop.Serialize(JSON.stringify(document));
}

export type CsvImportMode = 'merge' | 'replaceAll';

export interface CsvImportPreview {
	headers: string[];
	rows: { cells: string[] }[];
	suggestedMappings: string[];
	availableLocaleMappings: string[];
	defaultLocale: string | null;
}

export interface CsvImportApplyResult {
	importedKeyCount: number;
	affectedLocaleCount: number;
	createdLocaleCount: number;
}

export interface CsvApplyImportResponse {
	result: CsvImportApplyResult;
	updatedLocaleContents: Record<string, string>;
}

export async function buildCsvImportPreview(
	context: vscode.ExtensionContext,
	localeContents: Record<string, string>,
	csvContent: string,
	defaultLocale: string | null
): Promise<CsvImportPreview> {
	const interop = await loadArbInterop(context);
	const json = interop.BuildCsvImportPreview(JSON.stringify(localeContents), csvContent, defaultLocale);
	return JSON.parse(json) as CsvImportPreview;
}

export async function applyCsvImport(
	context: vscode.ExtensionContext,
	localeContents: Record<string, string>,
	csvContent: string,
	mappings: string[],
	mode: CsvImportMode
): Promise<CsvApplyImportResponse> {
	const interop = await loadArbInterop(context);
	const json = interop.ApplyCsvImport(JSON.stringify(localeContents), csvContent, JSON.stringify(mappings), mode);
	return JSON.parse(json) as CsvApplyImportResponse;
}

export async function exportCsv(context: vscode.ExtensionContext, localeContents: Record<string, string>): Promise<string> {
	const interop = await loadArbInterop(context);
	return interop.ExportCsv(JSON.stringify(localeContents));
}

export type TranslationProvider = 'google' | 'azureOpenAI';

export interface AzureTranslationSettings {
	endpoint: string;
	deploymentName: string;
	apiKey: string;
	customPrompt: string;
	temperature: number;
}

export interface TranslationItem {
	key: string;
	sourceText: string;
	description?: string | null;
}

export interface ValidateSettingsResponse {
	valid: boolean;
	error?: string | null;
}

const EMPTY_AZURE_SETTINGS: AzureTranslationSettings = {
	endpoint: '',
	deploymentName: '',
	apiKey: '',
	customPrompt: '',
	temperature: 0.2
};

export async function validateTranslationSettings(
	context: vscode.ExtensionContext,
	provider: TranslationProvider,
	settings: AzureTranslationSettings
): Promise<ValidateSettingsResponse> {
	const interop = await loadArbInterop(context);
	const json = interop.ValidateTranslationSettings(provider, JSON.stringify(provider === 'google' ? EMPTY_AZURE_SETTINGS : settings));
	return JSON.parse(json) as ValidateSettingsResponse;
}

export async function translateBatch(
	context: vscode.ExtensionContext,
	provider: TranslationProvider,
	settings: AzureTranslationSettings,
	sourceLocale: string,
	targetLocale: string,
	items: TranslationItem[]
): Promise<string[]> {
	const interop = await loadArbInterop(context);
	const settingsJson = JSON.stringify(provider === 'google' ? EMPTY_AZURE_SETTINGS : settings);
	const json = await interop.TranslateBatch(provider, settingsJson, sourceLocale, targetLocale, JSON.stringify(items));
	return JSON.parse(json) as string[];
}