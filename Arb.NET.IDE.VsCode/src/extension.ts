import * as vscode from 'vscode';
import { parseArbContent, serializeArbDocument } from './arbWasm';
import { openArbToolWindow } from './arbToolWindow';

const SAMPLE_ARB = JSON.stringify({
	'@@locale': 'en',
	'greeting': 'Hello {name}!',
	'@greeting': {
		'description': 'A friendly greeting',
		'placeholders': { 'name': { 'type': 'String' } }
	}
});

export function activate(context: vscode.ExtensionContext) {
	console.log('Arb.NET extension is now active');

	const disposable = vscode.commands.registerCommand('arb-net.testWasm', async () => {
		try {
			const result = await parseArbContent(context, SAMPLE_ARB);
			if (!result.document) {
				vscode.window.showErrorMessage('Arb.NET WASM: parse failed - ' + JSON.stringify(result.validationResults.errors));
				return;
			}

			const roundTrip = await serializeArbDocument(context, result.document);
			vscode.window.showInformationMessage(
				`Arb.NET WASM round-trip OK. Locale: ${result.document.locale}, entries: ${Object.keys(result.document.entries).length}`
			);
			console.log('Round-tripped ARB content:\n' + roundTrip);
		} catch (err) {
			vscode.window.showErrorMessage('Arb.NET WASM call failed: ' + (err instanceof Error ? err.message : String(err)));
			console.error(err);
		}
	});

	context.subscriptions.push(disposable);

	const openEditorDisposable = vscode.commands.registerCommand('arb-net.openEditor', (uri?: vscode.Uri) => {
		openArbToolWindow(context, uri);
	});

	context.subscriptions.push(openEditorDisposable);
}
