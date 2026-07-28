import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { scanArbDirectories, ArbDirectoryGroup } from './arbDirectoryScanner';
import { loadArbTableData, saveArbEntryValue, removeArbKey, addArbKey, addArbLocale, ArbTableData } from './arbTableData';
import { getSavedColumnOrder, saveColumnOrder, applyColumnOrder, getSavedColumnWidths, saveColumnWidth } from './arbColumnOrder';
import { buildCsvExport } from './arbCsv';
import { openCsvImportDialog } from './arbCsvImportDialog';
import { openAiSettings } from './arbTranslationSettings';
import { openTranslateDialog } from './arbTranslateDialog';

const VIEW_TYPE = 'arbNet.toolWindow';
const TITLE = 'Arb.NET Editor';

let currentPanel: vscode.WebviewPanel | undefined;
let currentDirectories: ArbDirectoryGroup[] = [];

type ExtensionMessage =
	| { type: 'directories'; directories: ArbDirectoryGroup[]; selectDirectory?: string }
	| { type: 'tableData'; data: ArbTableData; columnWidths: Record<string, number> }
	| { type: 'tableError'; message: string };

type WebviewMessage =
	| { type: 'ready' }
	| { type: 'refreshDirectories' }
	| { type: 'directorySelected'; directory: string }
	| { type: 'filterChanged'; filter: string }
	| { type: 'cellEdited'; filePath: string; key: string; value: string }
	| { type: 'removeKey'; directory: string; key: string }
	| { type: 'addKey'; directory: string }
	| { type: 'addLocale'; directory: string }
	| { type: 'exportCsv'; directory: string }
	| { type: 'importCsv'; directory: string }
	| { type: 'translate'; directory: string }
	| { type: 'aiSettings' }
	| { type: 'columnOrderChanged'; directory: string; locales: string[] }
	| { type: 'columnWidthChanged'; directory: string; column: string; width: number };

export function openArbToolWindow(context: vscode.ExtensionContext, arbDirectory?: vscode.Uri): void {
	if (currentPanel) {
		currentPanel.reveal(vscode.ViewColumn.Active);
		postDirectories(context, currentPanel, arbDirectory);
		return;
	}

	currentPanel = vscode.window.createWebviewPanel(
		VIEW_TYPE,
		TITLE,
		vscode.ViewColumn.Active,
		{
			enableScripts: true,
			retainContextWhenHidden: true,
			localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'media')]
		}
	);

	currentPanel.webview.html = getHtml(context, currentPanel.webview);

	const panel = currentPanel;
	panel.webview.onDidReceiveMessage((message: WebviewMessage) => {
		switch (message.type) {
			case 'ready':
			case 'refreshDirectories':
				postDirectories(context, panel, arbDirectory);
				break;
			case 'directorySelected':
				loadAndPostTableData(context, panel, message.directory);
				break;
			case 'filterChanged':
				// No-op - filtering happens entirely client-side in the webview.
				break;
			case 'cellEdited':
				handleCellEdited(context, panel, message.filePath, message.key, message.value);
				break;
			case 'removeKey':
				handleRemoveKey(context, panel, message.directory, message.key);
				break;
			case 'addKey':
				handleAddKey(context, panel, message.directory);
				break;
			case 'addLocale':
				handleAddLocale(context, panel, message.directory);
				break;
			case 'exportCsv':
				handleExportCsv(context, panel, message.directory);
				break;
			case 'importCsv':
				handleImportCsv(context, panel, message.directory);
				break;
			case 'translate':
				handleTranslate(context, panel, message.directory);
				break;
			case 'aiSettings':
				openAiSettings(context);
				break;
			case 'columnOrderChanged':
				saveColumnOrder(context, message.directory, message.locales);
				break;
			case 'columnWidthChanged':
				saveColumnWidth(context, message.directory, message.column, message.width);
				break;
		}
	}, null, context.subscriptions);

	currentPanel.onDidDispose(() => {
		currentPanel = undefined;
	}, null, context.subscriptions);
}

async function postDirectories(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, preferredTarget?: vscode.Uri): Promise<void> {
	const directories = await scanArbDirectories();
	currentDirectories = directories;
	const selectDirectory = preferredTarget ? resolveArbDirectory(preferredTarget) : undefined;
	const message: ExtensionMessage = { type: 'directories', directories, selectDirectory };
	panel.webview.postMessage(message);

	const toLoad = directories.find((d) => d.directory === selectDirectory) ?? directories[0];
	if (toLoad) {
		await loadAndPostTableData(context, panel, toLoad.directory);
	}
}

async function loadAndPostTableData(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, directory: string): Promise<void> {
	const group = currentDirectories.find((d) => d.directory === directory);
	if (!group) {
		return;
	}
	try {
		const data = await loadArbTableData(context, group.arbFilePaths);
		const savedOrder = getSavedColumnOrder(context, directory);
		data.locales = applyColumnOrder(savedOrder, data.locales);
		const columnWidths = getSavedColumnWidths(context, directory);
		panel.webview.postMessage({ type: 'tableData', data, columnWidths } satisfies ExtensionMessage);
	} catch (err) {
		panel.webview.postMessage({
			type: 'tableError',
			message: err instanceof Error ? err.message : String(err)
		} satisfies ExtensionMessage);
	}
}

async function handleCellEdited(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, filePath: string, key: string, value: string): Promise<void> {
	const group = currentDirectories.find((d) => d.arbFilePaths.includes(filePath));
	try {
		await saveArbEntryValue(context, filePath, key, value);
		if (group) {
			await loadAndPostTableData(context, panel, group.directory);
		}
	} catch (err) {
		panel.webview.postMessage({
			type: 'tableError',
			message: err instanceof Error ? err.message : String(err)
		} satisfies ExtensionMessage);
	}
}

async function handleRemoveKey(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, directory: string, key: string): Promise<void> {
	const group = currentDirectories.find((d) => d.directory === directory);
	if (!group) {
		return;
	}

	const confirmation = await vscode.window.showWarningMessage(
		`Remove key "${key}" from all locales in this directory?`,
		{ modal: true },
		'Remove'
	);
	if (confirmation !== 'Remove') {
		return;
	}

	try {
		await removeArbKey(context, group.arbFilePaths, key);
		await loadAndPostTableData(context, panel, group.directory);
	} catch (err) {
		panel.webview.postMessage({
			type: 'tableError',
			message: err instanceof Error ? err.message : String(err)
		} satisfies ExtensionMessage);
	}
}

async function handleAddKey(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, directory: string): Promise<void> {
	const group = currentDirectories.find((d) => d.directory === directory);
	if (!group) {
		return;
	}

	const key = await vscode.window.showInputBox({
		title: 'Add ARB Key',
		prompt: 'Enter the new key name:'
	});
	if (!key || !key.trim()) {
		return;
	}

	try {
		await addArbKey(context, group.arbFilePaths, key.trim());
		await loadAndPostTableData(context, panel, group.directory);
	} catch (err) {
		panel.webview.postMessage({
			type: 'tableError',
			message: err instanceof Error ? err.message : String(err)
		} satisfies ExtensionMessage);
	}
}

async function handleAddLocale(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, directory: string): Promise<void> {
	const group = currentDirectories.find((d) => d.directory === directory);
	if (!group) {
		return;
	}

	const locale = await vscode.window.showInputBox({
		title: 'Add ARB Locale',
		prompt: 'Enter locale code (e.g. de, fr):'
	});
	if (!locale || !locale.trim()) {
		return;
	}

	try {
		const newFilePath = await addArbLocale(context, group.directory, group.arbFilePaths, locale.trim());
		if (!newFilePath) {
			vscode.window.showWarningMessage(`Locale file "${locale.trim()}.arb" already exists.`);
			return;
		}

		// The new file changes this directory's file set, so re-scan before reloading the table.
		const directories = await scanArbDirectories();
		currentDirectories = directories;
		const refreshedGroup = directories.find((d) => d.directory === directory);
		if (refreshedGroup) {
			await loadAndPostTableData(context, panel, refreshedGroup.directory);
		}
	} catch (err) {
		panel.webview.postMessage({
			type: 'tableError',
			message: err instanceof Error ? err.message : String(err)
		} satisfies ExtensionMessage);
	}
}

async function handleExportCsv(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, directory: string): Promise<void> {
	const group = currentDirectories.find((d) => d.directory === directory);
	if (!group) {
		return;
	}

	const defaultFileName = path.basename(directory) + '.csv';
	const targetUri = await vscode.window.showSaveDialog({
		title: 'Export CSV',
		defaultUri: vscode.Uri.file(path.join(path.dirname(directory), defaultFileName)),
		filters: { 'CSV files': ['csv'] }
	});
	if (!targetUri) {
		return;
	}

	try {
		const csv = await buildCsvExport(context, group.arbFilePaths);
		await fs.promises.writeFile(targetUri.fsPath, csv, 'utf8');
		vscode.window.showInformationMessage(`Exported CSV to ${targetUri.fsPath}`);
	} catch (err) {
		panel.webview.postMessage({
			type: 'tableError',
			message: err instanceof Error ? err.message : String(err)
		} satisfies ExtensionMessage);
	}
}

async function handleImportCsv(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, directory: string): Promise<void> {
	const group = currentDirectories.find((d) => d.directory === directory);
	if (!group) {
		return;
	}

	await openCsvImportDialog(context, group.directory, group.arbFilePaths, () => {
		loadAndPostTableData(context, panel, group.directory);
	});
}

async function handleTranslate(context: vscode.ExtensionContext, panel: vscode.WebviewPanel, directory: string): Promise<void> {
	const group = currentDirectories.find((d) => d.directory === directory);
	if (!group) {
		return;
	}

	await openTranslateDialog(context, group.directory, group.arbFilePaths, () => {
		loadAndPostTableData(context, panel, group.directory);
	});
}

function resolveArbDirectory(target: vscode.Uri): string {
	try {
		return fs.statSync(target.fsPath).isDirectory() ? target.fsPath : path.dirname(target.fsPath);
	} catch {
		return path.dirname(target.fsPath);
	}
}

function getHtml(context: vscode.ExtensionContext, webview: vscode.Webview): string {
	const mediaDir = vscode.Uri.joinPath(context.extensionUri, 'media');
	const styleUri = webview.asWebviewUri(vscode.Uri.joinPath(mediaDir, 'toolWindow.css'));
	const scriptUri = webview.asWebviewUri(vscode.Uri.joinPath(mediaDir, 'toolWindow.js'));

	const templatePath = vscode.Uri.joinPath(mediaDir, 'toolWindow.html');
	const template = fs.readFileSync(templatePath.fsPath, 'utf8');

	return template
		.replaceAll('{{cspSource}}', webview.cspSource)
		.replaceAll('{{styleUri}}', styleUri.toString())
		.replaceAll('{{scriptUri}}', scriptUri.toString());
}