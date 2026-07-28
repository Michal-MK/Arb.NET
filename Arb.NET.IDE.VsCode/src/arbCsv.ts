import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { buildCsvImportPreview, applyCsvImport, exportCsv as exportCsvWasm, CsvImportPreview, CsvImportMode, CsvApplyImportResponse } from './arbWasm';

async function loadLocaleContents(arbFilePaths: string[]): Promise<Record<string, string>> {
	const contents: Record<string, string> = {};
	for (const filePath of arbFilePaths) {
		const localeHint = path.basename(filePath, '.arb');
		contents[localeHint] = await fs.promises.readFile(filePath, 'utf8');
	}
	return contents;
}

async function resolveDefaultLocale(arbDirectory: string): Promise<string | null> {
	let dir = arbDirectory;
	for (let i = 0; i < 6; i++) {
		const yamlPath = path.join(dir, 'l10n.yaml');
		if (fs.existsSync(yamlPath)) {
			const yaml = await fs.promises.readFile(yamlPath, 'utf8');
			const match = yaml.match(/^\s*template-arb-file\s*:\s*['"]?([^'"#\n]+)/im);
			const templateFile = match?.[1]?.trim();
			if (templateFile) {
				const templatePath = path.join(arbDirectory, templateFile);
				if (fs.existsSync(templatePath)) {
					const content = await fs.promises.readFile(templatePath, 'utf8');
					const localeMatch = content.match(/"@@locale"\s*:\s*"([^"]+)"/);
					if (localeMatch) {
						return localeMatch[1];
					}
				}
			}
			return null;
		}
		const parent = path.dirname(dir);
		if (parent === dir) {
			break;
		}
		dir = parent;
	}
	return null;
}

export async function loadCsvImportPreview(context: vscode.ExtensionContext, arbDirectory: string, arbFilePaths: string[], csvContent: string): Promise<CsvImportPreview> {
	const localeContents = await loadLocaleContents(arbFilePaths);
	const defaultLocale = await resolveDefaultLocale(arbDirectory);
	return buildCsvImportPreview(context, localeContents, csvContent, defaultLocale);
}

export async function performCsvImport(
	context: vscode.ExtensionContext,
	arbDirectory: string,
	arbFilePaths: string[],
	csvContent: string,
	mappings: string[],
	mode: CsvImportMode
): Promise<CsvApplyImportResponse> {
	const localeContents = await loadLocaleContents(arbFilePaths);
	const response = await applyCsvImport(context, localeContents, csvContent, mappings, mode);

	for (const [locale, content] of Object.entries(response.updatedLocaleContents)) {
		const filePath = path.join(arbDirectory, `${locale}.arb`);
		await fs.promises.mkdir(path.dirname(filePath), { recursive: true });
		await fs.promises.writeFile(filePath, content, 'utf8');
	}

	return response;
}

export async function buildCsvExport(context: vscode.ExtensionContext, arbFilePaths: string[]): Promise<string> {
	const localeContents = await loadLocaleContents(arbFilePaths);
	return exportCsvWasm(context, localeContents);
}