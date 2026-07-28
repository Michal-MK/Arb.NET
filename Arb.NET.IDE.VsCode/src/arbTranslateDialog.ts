import * as fs from 'fs';
import * as vscode from 'vscode';
import { loadArbTableData, saveArbEntryValue } from './arbTableData';
import { translateBatch, TranslationItem, TranslationProvider } from './arbWasm';
import { getTranslationSettings, hasApiKey } from './arbTranslationSettings';

const VIEW_TYPE = 'arbNet.translateDialog';

interface QueuedRow {
	key: string;
	targetLocale: string;
	sourceText: string;
	existingValue: string;
	translation: string;
	filePath: string;
}

type DialogMessage =
	| { type: 'init'; locales: string[]; defaultSourceLocale: string; defaultProvider: TranslationProvider; hasApiKey: boolean }
	| { type: 'preview'; rows: QueuedRow[] }
	| { type: 'progress'; completed: number; total: number; message: string }
	| { type: 'translated'; rows: QueuedRow[] }
	| { type: 'error'; message: string };

type WebviewMessage =
	| { type: 'ready' }
	| { type: 'requestPreview'; sourceLocale: string; targetLocales: string[]; mode: 'emptyOnly' | 'all' }
	| { type: 'translate'; sourceLocale: string; rows: QueuedRow[]; provider: TranslationProvider }
	| { type: 'applySelected'; rows: QueuedRow[] }
	| { type: 'cancel' };

export async function openTranslateDialog(
	context: vscode.ExtensionContext,
	directory: string,
	arbFilePaths: string[],
	onApplied: () => void
): Promise<void> {
	const tableData = await loadArbTableData(context, arbFilePaths);
	if (tableData.locales.length === 0) {
		vscode.window.showWarningMessage('No .arb files found in this directory.');
		return;
	}

	const panel = vscode.window.createWebviewPanel(
		VIEW_TYPE,
		'Translate',
		vscode.ViewColumn.Active,
		{
			enableScripts: true,
			localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'media')]
		}
	);

	panel.webview.html = getHtml(context, panel.webview);

	const locales = tableData.locales.map((l) => l.locale);
	const filePathByLocale = new Map(tableData.locales.map((l) => [l.locale, l.filePath]));

	panel.webview.onDidReceiveMessage(async (message: WebviewMessage) => {
		switch (message.type) {
			case 'ready': {
				const { provider } = await getTranslationSettings(context);
				const defaultSourceLocale = locales.find((l) => l === 'en')
					?? locales.find((l) => l.toLowerCase().startsWith('en'))
					?? locales[0];
				const initMessage: DialogMessage = {
					type: 'init',
					locales,
					defaultSourceLocale,
					defaultProvider: provider,
					hasApiKey: await hasApiKey(context)
				};
				panel.webview.postMessage(initMessage);
				break;
			}
			case 'requestPreview': {
				const rows = buildPreviewRows(tableData, message.sourceLocale, message.targetLocales, message.mode, filePathByLocale);
				panel.webview.postMessage({ type: 'preview', rows } satisfies DialogMessage);
				break;
			}
			case 'translate':
				await runTranslation(context, panel, message.sourceLocale, message.rows, message.provider);
				break;
			case 'applySelected':
				try {
					for (const row of message.rows) {
						if (!row.translation.trim()) {
							continue;
						}
						await saveArbEntryValue(context, row.filePath, row.key, row.translation);
					}
					vscode.window.showInformationMessage(`Applied ${message.rows.length} translation(s).`);
					panel.dispose();
					onApplied();
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

function buildPreviewRows(
	tableData: Awaited<ReturnType<typeof loadArbTableData>>,
	sourceLocale: string,
	targetLocales: string[],
	mode: 'emptyOnly' | 'all',
	filePathByLocale: Map<string, string>
): QueuedRow[] {
	const rows: QueuedRow[] = [];
	for (const targetLocale of targetLocales) {
		for (const row of tableData.rows) {
			const existing = row.values[targetLocale] ?? '';
			if (mode === 'emptyOnly' && existing) {
				continue;
			}

			// Prefer the selected source locale; fall back to any locale that has a value.
			let sourceText = row.values[sourceLocale] ?? '';
			if (!sourceText) {
				sourceText = Object.values(row.values).find((v) => !!v) ?? '';
			}
			if (!sourceText) {
				continue;
			}

			rows.push({
				key: row.key,
				targetLocale,
				sourceText,
				existingValue: existing,
				translation: '',
				filePath: filePathByLocale.get(targetLocale) ?? ''
			});
		}
	}
	return rows;
}

async function runTranslation(
	context: vscode.ExtensionContext,
	panel: vscode.WebviewPanel,
	sourceLocale: string,
	queuedRows: QueuedRow[],
	provider: TranslationProvider
): Promise<void> {
	const { settings } = await getTranslationSettings(context);

	const rowsByLocale = new Map<string, QueuedRow[]>();
	for (const row of queuedRows) {
		const list = rowsByLocale.get(row.targetLocale);
		if (list) {
			list.push(row);
		} else {
			rowsByLocale.set(row.targetLocale, [row]);
		}
	}

	let completed = 0;
	const totalWork = queuedRows.length;

	for (const [targetLocale, localeRows] of rowsByLocale) {
		panel.webview.postMessage({
			type: 'progress',
			completed,
			total: totalWork,
			message: `Translating to ${targetLocale}...`
		} satisfies DialogMessage);

		const items: TranslationItem[] = localeRows.map((row) => ({
			key: row.key,
			sourceText: row.sourceText
		}));

		try {
			const translations = await translateBatch(context, provider, settings, sourceLocale, targetLocale, items);
			localeRows.forEach((row, index) => {
				row.translation = translations[index] ?? '';
			});
		} catch (err) {
			panel.webview.postMessage({
				type: 'error',
				message: `Translating to ${targetLocale} failed: ${err instanceof Error ? err.message : String(err)}`
			} satisfies DialogMessage);
		}

		completed += localeRows.length;
	}

	panel.webview.postMessage({
		type: 'progress',
		completed: totalWork,
		total: totalWork,
		message: 'Done.'
	} satisfies DialogMessage);
	panel.webview.postMessage({ type: 'translated', rows: queuedRows } satisfies DialogMessage);
}

function getHtml(context: vscode.ExtensionContext, webview: vscode.Webview): string {
	const mediaDir = vscode.Uri.joinPath(context.extensionUri, 'media');
	const styleUri = webview.asWebviewUri(vscode.Uri.joinPath(mediaDir, 'translateDialog.css'));
	const scriptUri = webview.asWebviewUri(vscode.Uri.joinPath(mediaDir, 'translateDialog.js'));

	const templatePath = vscode.Uri.joinPath(mediaDir, 'translateDialog.html');
	const template = fs.readFileSync(templatePath.fsPath, 'utf8');

	return template
		.replaceAll('{{cspSource}}', webview.cspSource)
		.replaceAll('{{styleUri}}', styleUri.toString())
		.replaceAll('{{scriptUri}}', scriptUri.toString());
}