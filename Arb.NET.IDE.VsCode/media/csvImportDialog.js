(function () {
	const vscode = acquireVsCodeApi();

	const sourceLabel = document.getElementById('source-label');
	const errorBanner = document.getElementById('error-banner');
	const mappingBody = document.getElementById('mapping-body');
	const previewHeadRow = document.getElementById('preview-head-row');
	const previewBody = document.getElementById('preview-body');
	const importButton = document.getElementById('import-button');
	const cancelButton = document.getElementById('cancel-button');
	const modeRadios = document.querySelectorAll('input[name="mode"]');

	/** @type {{headers: string[], rows: {cells: string[]}[], suggestedMappings: string[], availableLocaleMappings: string[], defaultLocale: string | null} | null} */
	let preview = null;
	/** @type {string[]} */
	let currentMappings = [];

	function showError(message) {
		errorBanner.textContent = message;
		errorBanner.style.display = 'block';
	}

	function clearError() {
		errorBanner.style.display = 'none';
		errorBanner.textContent = '';
	}

	function validateMappings() {
		const keyCount = currentMappings.filter((m) => m === 'key').length;
		if (keyCount === 0) {
			return 'Exactly one column must be mapped to "Key".';
		}
		if (keyCount > 1) {
			return 'Only one column can be mapped to "Key".';
		}
		const localeMappings = currentMappings.filter((m) => m && m !== 'key');
		const seen = new Set();
		for (const locale of localeMappings) {
			const lower = locale.toLowerCase();
			if (seen.has(lower)) {
				return `Locale "${locale}" is mapped more than once.`;
			}
			seen.add(lower);
		}
		return null;
	}

	function updateImportButtonState() {
		const error = validateMappings();
		importButton.disabled = !!error;
		if (error) {
			showError(error);
		} else {
			clearError();
		}
	}

	function renderMappingTable() {
		mappingBody.innerHTML = '';
		if (!preview) return;

		preview.headers.forEach((header, index) => {
			const tr = document.createElement('tr');

			const headerTd = document.createElement('td');
			headerTd.textContent = header;
			tr.appendChild(headerTd);

			const selectTd = document.createElement('td');
			const select = document.createElement('select');

			const ignoreOption = document.createElement('option');
			ignoreOption.value = '';
			ignoreOption.textContent = '(Ignore)';
			select.appendChild(ignoreOption);

			const keyOption = document.createElement('option');
			keyOption.value = 'key';
			keyOption.textContent = 'Key';
			select.appendChild(keyOption);

			for (const locale of preview.availableLocaleMappings) {
				const option = document.createElement('option');
				option.value = locale;
				option.textContent = locale;
				select.appendChild(option);
			}

			select.value = currentMappings[index] || '';
			select.addEventListener('change', () => {
				currentMappings[index] = select.value;
				updateImportButtonState();
			});

			selectTd.appendChild(select);
			tr.appendChild(selectTd);
			mappingBody.appendChild(tr);
		});
	}

	function renderPreviewTable() {
		previewHeadRow.innerHTML = '';
		previewBody.innerHTML = '';
		if (!preview) return;

		for (const header of preview.headers) {
			const th = document.createElement('th');
			th.textContent = header;
			previewHeadRow.appendChild(th);
		}

		for (const row of preview.rows) {
			const tr = document.createElement('tr');
			for (const cell of row.cells) {
				const td = document.createElement('td');
				td.textContent = cell;
				td.title = cell;
				tr.appendChild(td);
			}
			previewBody.appendChild(tr);
		}
	}

	function getSelectedMode() {
		for (const radio of modeRadios) {
			if (radio.checked) return radio.value;
		}
		return 'merge';
	}

	importButton.addEventListener('click', () => {
		const error = validateMappings();
		if (error) {
			showError(error);
			return;
		}
		vscode.postMessage({ type: 'confirmImport', mappings: currentMappings, mode: getSelectedMode() });
	});

	cancelButton.addEventListener('click', () => {
		vscode.postMessage({ type: 'cancel' });
	});

	window.addEventListener('message', (event) => {
		const message = event.data;
		switch (message.type) {
			case 'preview':
				clearError();
				preview = message.preview;
				currentMappings = [...preview.suggestedMappings];
				sourceLabel.textContent = message.sourceName ? `Source: ${message.sourceName}` : '';
				renderMappingTable();
				renderPreviewTable();
				updateImportButtonState();
				break;
			case 'error':
				showError(message.message);
				break;
		}
	});

	vscode.postMessage({ type: 'ready' });
})();
