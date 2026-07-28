(function () {
	const vscode = acquireVsCodeApi();

	const errorBanner = document.getElementById('error-banner');
	const noKeyBanner = document.getElementById('no-key-banner');
	const sourceLocaleSelect = document.getElementById('source-locale');
	const targetLocalesContainer = document.getElementById('target-locales');
	const includeSubculturesCheckbox = document.getElementById('include-subcultures');
	const translateButton = document.getElementById('translate-button');
	const cancelButton = document.getElementById('cancel-button');
	const progressRow = document.getElementById('progress-row');
	const progressMessage = document.getElementById('progress-message');
	const statusMessage = document.getElementById('status-message');
	const resultsBody = document.getElementById('results-body');
	const applyRow = document.getElementById('apply-row');
	const applyButton = document.getElementById('apply-button');

	/** @type {string[]} */
	let locales = [];
	/** @type {{key: string, targetLocale: string, sourceText: string, existingValue: string, translation: string, filePath: string}[]} */
	let queuedRows = [];

	function showError(message) {
		errorBanner.textContent = message;
		errorBanner.style.display = 'block';
	}

	function clearError() {
		errorBanner.style.display = 'none';
		errorBanner.textContent = '';
	}

	// A locale is a "subculture" if it has a region/script suffix (en_US, en-US, ...) -
	// mirrors StringHelper.IsSubcultureLocale, hidden by default like JetBrains/VS.
	function isSubcultureLocale(locale) {
		return locale.replace(/-/g, '_').includes('_');
	}

	function visibleTargetLocales(source) {
		return locales.filter((locale) => locale !== source && (includeSubculturesCheckbox.checked || !isSubcultureLocale(locale)));
	}

	function renderLocalePickers() {
		sourceLocaleSelect.innerHTML = '';
		for (const locale of locales) {
			const option = document.createElement('option');
			option.value = locale;
			option.textContent = locale;
			sourceLocaleSelect.appendChild(option);
		}

		rebuildTargetCheckboxes();
	}

	function rebuildTargetCheckboxes() {
		const source = sourceLocaleSelect.value;
		const previousSelections = new Map(
			Array.from(targetLocalesContainer.querySelectorAll('input[type="checkbox"]')).map((cb) => [cb.value, cb.checked])
		);

		targetLocalesContainer.innerHTML = '';
		for (const locale of visibleTargetLocales(source)) {
			const label = document.createElement('label');
			const checkbox = document.createElement('input');
			checkbox.type = 'checkbox';
			checkbox.value = locale;
			checkbox.checked = previousSelections.has(locale) ? previousSelections.get(locale) : true;
			checkbox.addEventListener('change', requestPreview);
			label.appendChild(checkbox);
			label.appendChild(document.createTextNode(' ' + locale));
			targetLocalesContainer.appendChild(label);
		}
	}

	function getSelectedTargetLocales() {
		return Array.from(targetLocalesContainer.querySelectorAll('input[type="checkbox"]:checked')).map((cb) => cb.value);
	}

	function getSelectedMode() {
		const checked = document.querySelector('input[name="mode"]:checked');
		return checked ? checked.value : 'emptyOnly';
	}

	function getSelectedProvider() {
		const checked = document.querySelector('input[name="provider"]:checked');
		return checked ? checked.value : 'google';
	}

	function requestPreview() {
		vscode.postMessage({
			type: 'requestPreview',
			sourceLocale: sourceLocaleSelect.value,
			targetLocales: getSelectedTargetLocales(),
			mode: getSelectedMode()
		});
	}

	sourceLocaleSelect.addEventListener('change', () => {
		rebuildTargetCheckboxes();
		requestPreview();
	});
	includeSubculturesCheckbox.addEventListener('change', () => {
		rebuildTargetCheckboxes();
		requestPreview();
	});
	document.querySelectorAll('input[name="mode"]').forEach((radio) => radio.addEventListener('change', requestPreview));

	translateButton.addEventListener('click', () => {
		if (queuedRows.length === 0) {
			showError('Nothing queued - adjust source locale, targets, or mode.');
			return;
		}
		clearError();
		applyRow.style.display = 'none';
		progressRow.style.display = 'block';
		translateButton.disabled = true;

		vscode.postMessage({
			type: 'translate',
			sourceLocale: sourceLocaleSelect.value,
			rows: queuedRows,
			provider: getSelectedProvider()
		});
	});

	cancelButton.addEventListener('click', () => {
		vscode.postMessage({ type: 'cancel' });
	});

	applyButton.addEventListener('click', () => {
		const selectedRows = [];
		const rowEls = resultsBody.querySelectorAll('tr');
		rowEls.forEach((tr, index) => {
			const checkbox = tr.querySelector('input[type="checkbox"]');
			const translationInput = tr.querySelector('.translation-cell input');
			if (checkbox.checked) {
				selectedRows.push({ ...queuedRows[index], translation: translationInput.value });
			}
		});
		if (selectedRows.length === 0) {
			showError('No rows selected to apply.');
			return;
		}
		vscode.postMessage({ type: 'applySelected', rows: selectedRows });
	});

	function renderRows() {
		resultsBody.innerHTML = '';

		if (queuedRows.length === 0) {
			statusMessage.textContent = getSelectedMode() === 'emptyOnly'
				? 'Nothing queued - all targets already have values. Switch to "All cells" to re-translate.'
				: 'Nothing queued - no translatable items found.';
			applyRow.style.display = 'none';
			return;
		}

		statusMessage.textContent = `${queuedRows.length} item(s) queued for translation.`;
		applyRow.style.display = 'flex';

		for (const row of queuedRows) {
			const tr = document.createElement('tr');

			const checkTd = document.createElement('td');
			const checkbox = document.createElement('input');
			checkbox.type = 'checkbox';
			checkbox.checked = !!row.translation.trim();
			checkTd.appendChild(checkbox);
			tr.appendChild(checkTd);

			const keyTd = document.createElement('td');
			keyTd.textContent = row.key;
			tr.appendChild(keyTd);

			const localeTd = document.createElement('td');
			localeTd.textContent = row.targetLocale;
			tr.appendChild(localeTd);

			const sourceTd = document.createElement('td');
			sourceTd.className = 'source-cell';
			sourceTd.textContent = row.sourceText;
			sourceTd.title = row.sourceText;
			tr.appendChild(sourceTd);

			const existingTd = document.createElement('td');
			existingTd.className = 'existing-cell';
			existingTd.textContent = row.existingValue;
			existingTd.title = row.existingValue;
			tr.appendChild(existingTd);

			const translationTd = document.createElement('td');
			translationTd.className = 'translation-cell';
			const translationInput = document.createElement('input');
			translationInput.type = 'text';
			translationInput.value = row.translation;
			translationTd.appendChild(translationInput);
			tr.appendChild(translationTd);

			resultsBody.appendChild(tr);
		}
	}

	window.addEventListener('message', (event) => {
		const message = event.data;
		switch (message.type) {
			case 'init':
				locales = message.locales;
				renderLocalePickers();
				if (message.defaultSourceLocale) {
					sourceLocaleSelect.value = message.defaultSourceLocale;
					rebuildTargetCheckboxes();
				}
				if (message.defaultProvider) {
					const providerRadio = document.querySelector(`input[name="provider"][value="${message.defaultProvider}"]`);
					if (providerRadio) providerRadio.checked = true;
				}
				noKeyBanner.style.display = (!message.hasApiKey && message.defaultProvider === 'azureOpenAI') ? 'block' : 'none';
				requestPreview();
				break;
			case 'preview':
				queuedRows = message.rows;
				renderRows();
				break;
			case 'progress':
				progressMessage.textContent = `${message.message} (${message.completed}/${message.total})`;
				if (message.completed >= message.total) {
					progressRow.style.display = 'none';
					translateButton.disabled = false;
				}
				break;
			case 'translated':
				queuedRows = message.rows;
				renderRows();
				break;
			case 'error':
				showError(message.message);
				translateButton.disabled = false;
				break;
		}
	});

	document.querySelectorAll('input[name="provider"]').forEach((radio) => {
		radio.addEventListener('change', () => {
			noKeyBanner.style.display = 'none';
		});
	});

	vscode.postMessage({ type: 'ready' });
})();
