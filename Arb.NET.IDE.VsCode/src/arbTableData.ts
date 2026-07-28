import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { parseArbContent, serializeArbDocument, ArbDocument, ArbEntry } from './arbWasm';

export interface ArbTableLocale {
	locale: string;
	filePath: string;
}

export interface ArbTableRow {
	key: string;
	values: Record<string, string>;
	hasPlaceholders: boolean;
}

export interface ArbTableData {
	locales: ArbTableLocale[];
	rows: ArbTableRow[];
}

interface LoadedLocale {
	locale: string;
	filePath: string;
	document: ArbDocument;
}

async function loadLocale(context: vscode.ExtensionContext, filePath: string): Promise<LoadedLocale | undefined> {
	const content = await fs.promises.readFile(filePath, 'utf8');
	const result = await parseArbContent(context, content);
	if (!result.document) {
		return undefined;
	}
	const locale = result.document.locale || path.basename(filePath, '.arb');
	return { locale, filePath, document: result.document };
}

export async function loadArbTableData(context: vscode.ExtensionContext, arbFilePaths: string[]): Promise<ArbTableData> {
	const loaded = (await Promise.all(arbFilePaths.map((filePath) => loadLocale(context, filePath))))
		.filter((l): l is LoadedLocale => l !== undefined)
		.sort((a, b) => a.locale.localeCompare(b.locale));

	const rowsByKey = new Map<string, ArbTableRow>();
	for (const { locale, document } of loaded) {
		for (const entry of Object.values(document.entries)) {
			let row = rowsByKey.get(entry.key);
			if (!row) {
				row = { key: entry.key, values: {}, hasPlaceholders: false };
				rowsByKey.set(entry.key, row);
			}
			row.values[locale] = entry.value;
			if (entry.metadata?.placeholders && Object.keys(entry.metadata.placeholders).length > 0) {
				row.hasPlaceholders = true;
			}
		}
	}

	const rows = Array.from(rowsByKey.values()).sort((a, b) => a.key.localeCompare(b.key, undefined, { sensitivity: 'base' }));

	return {
		locales: loaded.map(({ locale, filePath }) => ({ locale, filePath })),
		rows
	};
}

export async function saveArbEntryValue(context: vscode.ExtensionContext, filePath: string, key: string, value: string): Promise<void> {
	const content = await fs.promises.readFile(filePath, 'utf8');
	const result = await parseArbContent(context, content);
	if (!result.document) {
		throw new Error(`Cannot save: ${filePath} failed to parse.`);
	}

	const existing: ArbEntry | undefined = result.document.entries[key];
	result.document.entries[key] = {
		key,
		value,
		metadata: existing?.metadata
	};

	const serialized = await serializeArbDocument(context, result.document);
	await fs.promises.writeFile(filePath, serialized, 'utf8');
}

export async function removeArbKey(context: vscode.ExtensionContext, arbFilePaths: string[], key: string): Promise<void> {
	await Promise.all(arbFilePaths.map(async (filePath) => {
		const content = await fs.promises.readFile(filePath, 'utf8');
		const result = await parseArbContent(context, content);
		if (!result.document || !(key in result.document.entries)) {
			return;
		}

		delete result.document.entries[key];
		const serialized = await serializeArbDocument(context, result.document);
		await fs.promises.writeFile(filePath, serialized, 'utf8');
	}));
}

export async function addArbKey(context: vscode.ExtensionContext, arbFilePaths: string[], key: string): Promise<void> {
	await Promise.all(arbFilePaths.map(async (filePath) => {
		const content = await fs.promises.readFile(filePath, 'utf8');
		const result = await parseArbContent(context, content);
		if (!result.document || key in result.document.entries) {
			return;
		}

		result.document.entries[key] = { key, value: '' };
		const serialized = await serializeArbDocument(context, result.document);
		await fs.promises.writeFile(filePath, serialized, 'utf8');
	}));
}

export async function addArbLocale(context: vscode.ExtensionContext, directory: string, arbFilePaths: string[], locale: string): Promise<string | undefined> {
	const filePath = path.join(directory, `${locale}.arb`);
	if (fs.existsSync(filePath)) {
		return undefined;
	}

	const existingKeys = new Set<string>();
	for (const existingPath of arbFilePaths) {
		const content = await fs.promises.readFile(existingPath, 'utf8');
		const result = await parseArbContent(context, content);
		if (result.document) {
			for (const key of Object.keys(result.document.entries)) {
				existingKeys.add(key);
			}
		}
	}

	const document: ArbDocument = {
		locale,
		context: '',
		entries: Object.fromEntries(
			Array.from(existingKeys).sort().map((key) => [key, { key, value: '' }])
		)
	};

	const serialized = await serializeArbDocument(context, document);
	await fs.promises.writeFile(filePath, serialized, 'utf8');
	return filePath;
}