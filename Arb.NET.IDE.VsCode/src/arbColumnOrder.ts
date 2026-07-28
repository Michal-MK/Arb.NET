import * as vscode from 'vscode';

const ORDER_STATE_KEY_PREFIX = 'arbNet.columnOrder.';
const WIDTH_STATE_KEY_PREFIX = 'arbNet.columnWidths.';

export function getSavedColumnOrder(context: vscode.ExtensionContext, directory: string): string[] {
	return context.globalState.get<string[]>(ORDER_STATE_KEY_PREFIX + directory, []);
}

export async function saveColumnOrder(context: vscode.ExtensionContext, directory: string, locales: string[]): Promise<void> {
	await context.globalState.update(ORDER_STATE_KEY_PREFIX + directory, locales);
}

export function applyColumnOrder<T extends { locale: string }>(saved: string[], locales: T[]): T[] {
	if (saved.length === 0) {
		return locales;
	}
	const byLocale = new Map(locales.map((l) => [l.locale, l]));
	const ordered: T[] = [];
	for (const locale of saved) {
		const match = byLocale.get(locale);
		if (match) {
			ordered.push(match);
			byLocale.delete(locale);
		}
	}
	// Remaining locales (new since last save) keep their incoming order (already alphabetical).
	for (const locale of locales) {
		if (byLocale.has(locale.locale)) {
			ordered.push(locale);
		}
	}
	return ordered;
}

export function getSavedColumnWidths(context: vscode.ExtensionContext, directory: string): Record<string, number> {
	return context.globalState.get<Record<string, number>>(WIDTH_STATE_KEY_PREFIX + directory, {});
}

export async function saveColumnWidth(context: vscode.ExtensionContext, directory: string, column: string, width: number): Promise<void> {
	const widths = getSavedColumnWidths(context, directory);
	widths[column] = width;
	await context.globalState.update(WIDTH_STATE_KEY_PREFIX + directory, widths);
}