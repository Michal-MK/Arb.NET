(function () {
	const vscode = acquireVsCodeApi();

	const directorySelect = document.getElementById('directory-select');
	const filterInput = document.getElementById('filter-input');
	const emptyState = document.getElementById('empty-state');
	const errorBanner = document.getElementById('error-banner');
	const tableHeadRow = document.getElementById('table-head-row');
	const tableBody = document.getElementById('table-body');
	const tableEmptyState = document.getElementById('table-empty-state');

	let directories = [];
	/** @type {{locales: {locale: string, filePath: string}[], rows: {key: string, values: Record<string,string>, hasPlaceholders: boolean}[]} | null} */
	let tableData = null;
	let filterText = '';
	let sortColumn = 'key';
	let sortDirection = 'asc';
	/** @type {string | null} */
	let selectedKey = null;
	/** column name ("key" or a locale) -> width in px. Always fully populated once a
	 * table renders - every column gets an explicit width (DEFAULT_COLUMN_WIDTH if never
	 * resized), otherwise table-layout: fixed redistributes space from resized columns
	 * into any still-unset neighbor whenever one column's width changes. */
	let columnWidths = {};
	const DEFAULT_COLUMN_WIDTH = 200;

	const removeKeyButton = document.querySelector('button[data-action="removeKey"]');
	const aiSettingsButton = document.querySelector('button[data-action="aiSettings"]');
	// Every other action button operates on "the currently selected directory" and is
	// disabled until one is chosen; removeKey is gated on row selection instead (below),
	// and aiSettings is a global action with no directory dependency at all.
	const directoryScopedButtons = Array.from(document.querySelectorAll('button[data-action]'))
		.filter((button) => button !== removeKeyButton && button !== aiSettingsButton);

	function updateActionButtonState() {
		if (removeKeyButton) {
			removeKeyButton.disabled = selectedKey === null;
		}
		const hasDirectory = !!directorySelect.value;
		for (const button of directoryScopedButtons) {
			button.disabled = !hasDirectory;
		}
	}
	updateActionButtonState();

	function renderDirectories() {
		directorySelect.innerHTML = '';
		if (directories.length === 0) {
			emptyState.style.display = 'block';
			directorySelect.disabled = true;
			updateActionButtonState();
			return;
		}
		emptyState.style.display = 'none';
		directorySelect.disabled = false;
		for (const group of directories) {
			const option = document.createElement('option');
			option.value = group.directory;
			option.textContent = group.relativePath;
			directorySelect.appendChild(option);
		}
		updateActionButtonState();
	}

	/** en_US -> en, pt_BR -> pt. Mirrors the fallback-preview parent lookup in the other IDEs. */
	function parentLocale(locale) {
		const idx = Math.max(locale.lastIndexOf('_'), locale.lastIndexOf('-'));
		return idx > 0 ? locale.substring(0, idx) : null;
	}

	function resolveFallback(row, locale) {
		let current = parentLocale(locale);
		while (current) {
			// Emptiness check (not presence) - intentionally matches the JetBrains/VS grids,
			// which both show a fallback preview for an explicit empty string too. This
			// diverges from ArbCodeGenerator.cs's ContainsKey-based fallback (codegen won't
			// fall back for an explicit ""), but consistency with the other editors' UX wins.
			if (row.values[current]) {
				return { locale: current, value: row.values[current] };
			}
			current = parentLocale(current);
		}
		return null;
	}

	function matchesFilter(row) {
		if (!filterText) return true;
		const needle = filterText.toLowerCase();
		if (row.key.toLowerCase().includes(needle)) return true;
		return Object.values(row.values).some((v) => v.toLowerCase().includes(needle));
	}

	function sortedFilteredRows() {
		if (!tableData) return [];
		const rows = tableData.rows.filter(matchesFilter);
		rows.sort((a, b) => {
			let av, bv;
			if (sortColumn === 'key') {
				av = a.key;
				bv = b.key;
			} else {
				av = a.values[sortColumn] || '';
				bv = b.values[sortColumn] || '';
			}
			const cmp = av.localeCompare(bv, undefined, { sensitivity: 'base' });
			return sortDirection === 'asc' ? cmp : -cmp;
		});
		return rows;
	}

	function renderTable() {
		tableHeadRow.innerHTML = '';
		tableBody.innerHTML = '';

		if (!tableData || tableData.locales.length === 0) {
			tableEmptyState.style.display = 'block';
			return;
		}
		tableEmptyState.style.display = 'none';

		const keyTh = document.createElement('th');
		keyTh.textContent = 'Key';
		keyTh.dataset.column = 'key';
		tableHeadRow.appendChild(keyTh);

		for (const { locale } of tableData.locales) {
			const th = document.createElement('th');
			th.textContent = locale;
			th.dataset.column = locale;
			tableHeadRow.appendChild(th);
		}

		for (const th of tableHeadRow.querySelectorAll('th')) {
			if (th.dataset.column === sortColumn) {
				th.classList.add(sortDirection === 'asc' ? 'sorted-asc' : 'sorted-desc');
			}
			// Every column always gets an explicit width - see columnWidths comment above.
			if (!columnWidths[th.dataset.column]) {
				columnWidths[th.dataset.column] = DEFAULT_COLUMN_WIDTH;
			}
			th.style.width = columnWidths[th.dataset.column] + 'px';
			th.addEventListener('click', () => {
				if (sortColumn === th.dataset.column) {
					sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
				} else {
					sortColumn = th.dataset.column;
					sortDirection = 'asc';
				}
				renderTable();
			});

			const handle = document.createElement('div');
			handle.className = 'resize-handle';
			th.appendChild(handle);
		}

		wireColumnDragAndDrop();
		wireColumnResize();

		const rows = sortedFilteredRows();
		if (rows.length === 0) {
			tableEmptyState.style.display = 'block';
			tableEmptyState.textContent = filterText ? 'No keys match the current filter.' : 'No keys in this directory yet.';
		}

		for (const row of rows) {
			const tr = document.createElement('tr');
			tr.dataset.key = row.key;
			if (row.key === selectedKey) {
				tr.classList.add('selected-row');
			}
			tr.addEventListener('click', () => selectRow(row.key));

			const keyTd = document.createElement('td');
			keyTd.className = 'key-cell';
			keyTd.textContent = row.key;
			if (row.hasPlaceholders) {
				const badge = document.createElement('span');
				badge.className = 'placeholder-badge';
				badge.title = 'Has placeholders';
				badge.textContent = '{}';
				keyTd.appendChild(badge);
			}
			tr.appendChild(keyTd);

			for (const { locale, filePath } of tableData.locales) {
				const td = document.createElement('td');
				td.className = 'value-cell';
				const value = row.values[locale];
				renderValueCell(td, row, locale, filePath, value);
				tr.appendChild(td);
			}

			tableBody.appendChild(tr);
		}

		updateActionButtonState();
	}

	/**
	 * Drag-and-drop column reordering, mirroring the JetBrains/VS grids: any column
	 * (including Key) can be dragged, but only the locale order is persisted per
	 * directory - Key always renders first regardless of where it's dropped.
	 */
	function wireColumnDragAndDrop() {
		let draggedColumn = null;

		for (const th of tableHeadRow.querySelectorAll('th')) {
			th.draggable = true;

			th.addEventListener('dragstart', (e) => {
				draggedColumn = th.dataset.column;
				e.dataTransfer.effectAllowed = 'move';
				th.classList.add('dragging');
			});

			th.addEventListener('dragend', () => {
				th.classList.remove('dragging');
				for (const other of tableHeadRow.querySelectorAll('th')) {
					other.classList.remove('drag-over');
				}
			});

			th.addEventListener('dragover', (e) => {
				e.preventDefault();
				e.dataTransfer.dropEffect = 'move';
				if (th.dataset.column !== draggedColumn) {
					th.classList.add('drag-over');
				}
			});

			th.addEventListener('dragleave', () => {
				th.classList.remove('drag-over');
			});

			th.addEventListener('drop', (e) => {
				e.preventDefault();
				th.classList.remove('drag-over');
				const targetColumn = th.dataset.column;
				if (!draggedColumn || draggedColumn === targetColumn || !tableData) {
					return;
				}
				reorderLocaleColumn(draggedColumn, targetColumn);
			});
		}
	}

	function reorderLocaleColumn(draggedColumn, targetColumn) {
		// "key" is always column 0 and never part of the persisted/reorderable locale list -
		// dropping onto or dragging it just has no effect on locale order (matches both IDEs).
		if (draggedColumn === 'key' || targetColumn === 'key') {
			return;
		}

		const locales = tableData.locales.slice();
		const fromIndex = locales.findIndex((l) => l.locale === draggedColumn);
		const toIndex = locales.findIndex((l) => l.locale === targetColumn);
		if (fromIndex === -1 || toIndex === -1) {
			return;
		}

		const [moved] = locales.splice(fromIndex, 1);
		locales.splice(toIndex, 0, moved);
		tableData.locales = locales;
		renderTable();

		if (directorySelect.value) {
			vscode.postMessage({
				type: 'columnOrderChanged',
				directory: directorySelect.value,
				locales: locales.map((l) => l.locale)
			});
		}
	}

	/**
	 * Column width resize via the handle appended to each th. Uses document-level
	 * mousemove/mouseup (not events on the handle itself) so dragging past the handle's
	 * own bounds still tracks correctly, matching native resize-column UX.
	 */
	function wireColumnResize() {
		const MIN_WIDTH = 24;

		for (const th of tableHeadRow.querySelectorAll('th')) {
			const handle = th.querySelector('.resize-handle');
			if (!handle) continue;

			handle.addEventListener('mousedown', (e) => {
				e.preventDefault();
				e.stopPropagation(); // don't let this bubble into the th's sort-on-click handler
				th.draggable = false; // avoid the handle drag starting a column-reorder drag

				const startX = e.clientX;
				// offsetWidth (integer, border-box) matches what style.width targets under
				// box-sizing: border-box - using the same measurement to read and write
				// avoids any subpixel/box-model mismatch causing a jump on the first frame.
				const startWidth = th.offsetWidth;
				handle.classList.add('resizing');

				function onMouseMove(moveEvent) {
					const newWidth = Math.max(MIN_WIDTH, startWidth + (moveEvent.clientX - startX));
					th.style.width = newWidth + 'px';
				}

				function onMouseUp() {
					document.removeEventListener('mousemove', onMouseMove);
					document.removeEventListener('mouseup', onMouseUp);
					handle.classList.remove('resizing');
					th.draggable = true;

					const finalWidth = th.offsetWidth;
					columnWidths[th.dataset.column] = finalWidth;
					if (directorySelect.value) {
						vscode.postMessage({
							type: 'columnWidthChanged',
							directory: directorySelect.value,
							column: th.dataset.column,
							width: finalWidth
						});
					}
				}

				document.addEventListener('mousemove', onMouseMove);
				document.addEventListener('mouseup', onMouseUp);
			});

			// Also stop the handle's own click/drag from reaching the th (sort / column reorder).
			handle.addEventListener('click', (e) => e.stopPropagation());
			handle.addEventListener('dragstart', (e) => e.stopPropagation());
		}
	}

	function selectRow(key) {
		if (selectedKey === key) return;
		selectedKey = key;
		for (const tr of tableBody.querySelectorAll('tr')) {
			tr.classList.toggle('selected-row', tr.dataset.key === key);
		}
		updateActionButtonState();
	}

	function renderValueCell(td, row, locale, filePath, value) {
		td.innerHTML = '';
		if (value) {
			td.textContent = value;
		} else {
			const fallback = resolveFallback(row, locale);
			if (fallback) {
				const span = document.createElement('span');
				span.className = 'fallback-preview';
				span.textContent = '(' + fallback.locale + ') ' + fallback.value;
				td.appendChild(span);
			}
		}

		td.addEventListener('click', () => beginEdit(td, row, locale, filePath, value || ''));
	}

	function beginEdit(td, row, locale, filePath, currentValue) {
		if (td.querySelector('textarea')) return;

		// Freeze the cell's current content-box height before swapping content, then let
		// the textarea auto-grow from there - matches whatever height the wrapped text/
		// fallback preview was already using, so there's no snap on entering edit mode.
		// clientHeight includes padding but not border, which is exactly the box the
		// textarea now occupies (it has no padding/border/margin of its own).
		const style = getComputedStyle(td);
		const paddingY = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
		const restingContentHeight = td.clientHeight - paddingY;

		td.innerHTML = '';
		const textarea = document.createElement('textarea');
		textarea.value = currentValue;
		textarea.rows = 1;
		textarea.style.minHeight = restingContentHeight + 'px';
		td.appendChild(textarea);

		function autoGrow() {
			textarea.style.height = 'auto';
			textarea.style.height = Math.max(textarea.scrollHeight, restingContentHeight) + 'px';
		}
		autoGrow();
		textarea.addEventListener('input', autoGrow);

		textarea.focus();
		textarea.select();

		let committed = false;
		function commit() {
			if (committed) return;
			committed = true;
			const newValue = textarea.value;
			if (newValue !== currentValue) {
				row.values[locale] = newValue;
				vscode.postMessage({ type: 'cellEdited', filePath, key: row.key, value: newValue });
			}
			renderValueCell(td, row, locale, filePath, newValue);
		}

		textarea.addEventListener('blur', commit);
		textarea.addEventListener('keydown', (e) => {
			if (e.key === 'Enter' && !e.shiftKey) {
				e.preventDefault();
				textarea.blur();
			} else if (e.key === 'Escape') {
				committed = true;
				renderValueCell(td, row, locale, filePath, currentValue);
			}
		});
	}

	function showError(message) {
		errorBanner.textContent = message;
		errorBanner.style.display = 'block';
	}

	function clearError() {
		errorBanner.style.display = 'none';
		errorBanner.textContent = '';
	}

	directorySelect.addEventListener('change', () => {
		vscode.postMessage({ type: 'directorySelected', directory: directorySelect.value });
	});

	filterInput.addEventListener('input', () => {
		filterText = filterInput.value;
		vscode.postMessage({ type: 'filterChanged', filter: filterText });
		renderTable();
	});

	for (const button of document.querySelectorAll('button[data-action]')) {
		if (button === removeKeyButton) continue; // wired separately below - needs the selected key
		button.addEventListener('click', () => {
			if (!directorySelect.value) return;
			vscode.postMessage({ type: button.dataset.action, directory: directorySelect.value });
		});
	}

	if (removeKeyButton) {
		removeKeyButton.addEventListener('click', () => {
			if (!selectedKey || !directorySelect.value) return;
			// The extension shows a confirmation dialog before actually removing anything;
			// selection is cleared naturally on the next tableData refresh if it goes through.
			vscode.postMessage({ type: 'removeKey', directory: directorySelect.value, key: selectedKey });
		});
	}

	window.addEventListener('message', (event) => {
		const message = event.data;
		switch (message.type) {
			case 'directories':
				directories = message.directories;
				renderDirectories();
				if (message.selectDirectory) {
					directorySelect.value = message.selectDirectory;
				}
				break;
			case 'tableData':
				clearError();
				tableData = message.data;
				columnWidths = message.columnWidths || {};
				renderTable();
				break;
			case 'tableError':
				showError(message.message);
				break;
		}
	});

	vscode.postMessage({ type: 'ready' });
})();
