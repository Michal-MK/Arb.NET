import * as path from 'path';
import * as vscode from 'vscode';

export interface ArbDirectoryGroup {
	arbDirectory: string;
	relativeWorkspacePath: string;
	arbFilePaths: string[];
}

export async function scanArbDirectories(): Promise<ArbDirectoryGroup[]> {
	const arbFiles = await vscode.workspace.findFiles('**/*.arb', '**/{bin,obj,node_modules}/**');

	const byDirectory = new Map<string, string[]>();
	for (const uri of arbFiles) {
		const dir = path.dirname(uri.fsPath);
		const existing = byDirectory.get(dir);
		if (existing) {
			existing.push(uri.fsPath);
		} else {
			byDirectory.set(dir, [uri.fsPath]);
		}
	}

	const groups: ArbDirectoryGroup[] = [];
	for (const [directory, arbFilePaths] of byDirectory) {
		const workspaceFolder = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(directory));
		const relativePath = workspaceFolder
			? path.relative(workspaceFolder.uri.fsPath, directory)
			: directory;

		groups.push({
			directory,
			relativePath: relativePath === '' ? '.' : relativePath,
			arbFilePaths: arbFilePaths.sort()
		});
	}

	groups.sort((a, b) => a.relativePath.localeCompare(b.relativePath));
	return groups;
}