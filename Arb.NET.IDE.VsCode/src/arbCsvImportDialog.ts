import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { CsvImportPreview, CsvImportMode } from './arbWasm';
import { loadCsvImportPreview, performCsvImport } from './arbCsv';

const VIEW_TYPE = 'arbNet.csvImportDialog';

type DialogMessage =
	| { type: 'preview'; preview: CsvImportPreview; sourceName: string }
	| { type: 'error'; message: string };

type WebviewMessage =
	| { type: 'ready' }
	| { type: 'confirmImport'; mappings: string[]; mode: CsvImportMode }
	| { type: 'cancel' };

export async function openCsvImportDialog(
	context: vscode.ExtensionContext,
	directory: string,
	arbFilePaths: string[],
	onImported: () => void
): Promise<void> {
	const fileUris = await vscode.window.showOpenDialog({
		title: 'Import CSV',
		canSelectMany: false,
		filters: { 'CSV files': ['csv'] }
	});
	if (!fileUris || fileUris.length === 0) {
		return;
	}
	const csvPath = fileUris[0].fsPath;
	const csvContent = await fs.promises.readFile(csvPath, 'utf8');

	let preview: CsvImportPreview;
	try {
		preview = await loadCsvImportPreview(context, directory, arbFilePaths, csvContent);
	} catch (err) {
		vscode.window.showErrorMessage(`Could not read CSV: ${err instanceof Error ? err.message : String(err)}`);
		return;
	}

	const panel = vscode.window.createWebviewPanel(
		VIEW_TYPE,
		'Import CSV',
		vscode.ViewColumn.Active,
		{
			enableScripts: true,
			localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'media')]
		}
	);

	panel.webview.html = getHtml(context, panel.webview);

	panel.webview.onDidReceiveMessage(async (message: WebviewMessage) => {
		switch (message.type) {
			case 'ready': {
				const dialogMessage: DialogMessage = {
					type: 'preview',
					preview,
					sourceName: path.basename(csvPath)
				};
				panel.webview.postMessage(dialogMessage);
				break;
			}
			case 'confirmImport':
				try {
					const response = await performCsvImport(context, directory, arbFilePaths, csvContent, message.mappings, message.mode);
					vscode.window.showInformationMessage(
						`Imported ${response.result.importedKeyCount} keys across ${response.result.affectedLocaleCount} locale(s)` +
						(response.result.createdLocaleCount > 0 ? ` (${response.result.createdLocaleCount} new)` : '') + '.'
					);
					panel.dispose();
					onImported();
				} catch (err) {
					panel.webview.postMessage({
						type: 'error',
						message: err instanceof Error ? err.message : String(err)
					} satisfies DialogMessage);
				}
				break;
			case 'cancel':
				panel.dispose();
				break;
		}
	}, null, context.subscriptions);
}

function getHtml(context: vscode.ExtensionContext, webview: vscode.Webview): string {
	const mediaDir = vscode.Uri.joinPath(context.extensionUri, 'media');
	const styleUri = webview.asWebviewUri(vscode.Uri.joinPath(mediaDir, 'csvImportDialog.css'));
	const scriptUri = webview.asWebviewUri(vscode.Uri.joinPath(mediaDir, 'csvImportDialog.js'));

	const templatePath = vscode.Uri.joinPath(mediaDir, 'csvImportDialog.html');
	const template = fs.readFileSync(templatePath.fsPath, 'utf8');

	return template
		.replaceAll('{{cspSource}}', webview.cspSource)
		.replaceAll('{{styleUri}}', styleUri.toString())
		.replaceAll('{{scriptUri}}', scriptUri.toString());
}