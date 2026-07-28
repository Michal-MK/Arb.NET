import * as vscode from 'vscode';
import { AzureTranslationSettings, TranslationProvider } from './arbWasm';

const SECRET_KEY_API_KEY = 'arbNet.translation.apiKey';

export async function getTranslationSettings(context: vscode.ExtensionContext): Promise<{ provider: TranslationProvider; settings: AzureTranslationSettings }> {
	const config = vscode.workspace.getConfiguration('arbNet.translation');
	const provider = config.get<TranslationProvider>('provider', 'google');
	const apiKey = (await context.secrets.get(SECRET_KEY_API_KEY)) ?? '';

	return {
		provider,
		settings: {
			endpoint: config.get<string>('azureEndpoint', ''),
			deploymentName: config.get<string>('azureDeploymentName', ''),
			apiKey,
			customPrompt: config.get<string>('customPrompt', ''),
			temperature: config.get<number>('temperature', 0.2)
		}
	};
}

export async function setApiKey(context: vscode.ExtensionContext, apiKey: string): Promise<void> {
	if (apiKey) {
		await context.secrets.store(SECRET_KEY_API_KEY, apiKey);
	} else {
		await context.secrets.delete(SECRET_KEY_API_KEY);
	}
}

export async function hasApiKey(context: vscode.ExtensionContext): Promise<boolean> {
	return !!(await context.secrets.get(SECRET_KEY_API_KEY));
}

export async function openAiSettings(context: vscode.ExtensionContext): Promise<void> {
	await vscode.commands.executeCommand('workbench.action.openSettings', 'arbNet.translation');

	const currentKey = await context.secrets.get(SECRET_KEY_API_KEY);
	const newKey = await vscode.window.showInputBox({
		title: 'Azure OpenAI API Key',
		prompt: 'Stored securely via VS Code SecretStorage. Leave blank to clear, Escape to leave unchanged.',
		password: true,
		value: '',
		placeHolder: currentKey ? '(unchanged - a key is already stored)' : 'Enter API key'
	});

	if (newKey === undefined) {
		return;
	}

	await setApiKey(context, newKey);
	vscode.window.showInformationMessage(newKey ? 'Azure OpenAI API key saved.' : 'Azure OpenAI API key cleared.');
}