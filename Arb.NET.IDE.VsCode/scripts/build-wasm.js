// Publishes Arb.NET.Wasm and copies its AppBundle into ../wasm (see Arb.NET.Wasm.csproj),
// but skips the (slow) dotnet publish when the existing output is already newer than every
// input source file, so `npm run watch` / F5 stays fast when no C# changed.
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const wasmProjectDir = path.join(__dirname, '..', '..', 'Arb.NET.Wasm');
const arbNetDir = path.join(__dirname, '..', '..', 'Arb.NET');
const wasmOutputDir = path.join(__dirname, '..', 'wasm');
const buildStampFile = path.join(wasmOutputDir, '.build-stamp');

function newestMTime(dir, extensions) {
	let newest = 0;
	for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
		if (entry.name === 'bin' || entry.name === 'obj' || entry.name.startsWith('.')) continue;
		const fullPath = path.join(dir, entry.name);
		if (entry.isDirectory()) {
			newest = Math.max(newest, newestMTime(fullPath, extensions));
		} else if (extensions.some((ext) => entry.name.endsWith(ext))) {
			newest = Math.max(newest, fs.statSync(fullPath).mtimeMs);
		}
	}
	return newest;
}

const sourceExtensions = ['.cs', '.csproj', '.json'];
const newestSource = Math.max(
	newestMTime(wasmProjectDir, sourceExtensions),
	newestMTime(arbNetDir, sourceExtensions)
);

if (fs.existsSync(buildStampFile) && fs.statSync(buildStampFile).mtimeMs > newestSource) {
	console.log('[build-wasm] wasm/ is up to date, skipping dotnet publish.');
	process.exit(0);
}

console.log('[build-wasm] Publishing Arb.NET.Wasm...');
execFileSync('dotnet', ['publish', '-c', 'Release'], {
	cwd: wasmProjectDir,
	stdio: 'inherit'
});

fs.writeFileSync(buildStampFile, new Date().toISOString());