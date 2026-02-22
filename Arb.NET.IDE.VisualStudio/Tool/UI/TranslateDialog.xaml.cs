using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Arb.NET.IDE.Common.Models;
using Arb.NET.IDE.VisualStudio.Tool.Models;
using Arb.NET.IDE.VisualStudio.Tool.Services;
using Arb.NET.IDE.VisualStudio.Tool.Services.Persistence;

namespace Arb.NET.IDE.VisualStudio.Tool.UI;

public partial class TranslateDialog : DialogWindow {
    private readonly List<ArbRow> rows;
    private readonly List<string> langCodes;
    private readonly List<ArbFile> arbFiles;
    private readonly TranslationSettingsService settingsService;
    private readonly ArbParser parser;

    private readonly ObservableCollection<TranslationItem> resultItems = new();
    private CancellationTokenSource? cts;

    public bool AppliedChanges { get; private set; }

    public TranslateDialog(
        List<ArbRow> rows,
        List<string> langCodes,
        List<ArbFile> arbFiles,
        string directory,
        TranslationSettingsService settingsService,
        ArbParser parser
    ) {
        this.rows = rows;
        this.langCodes = langCodes;
        this.arbFiles = arbFiles;
        this.settingsService = settingsService;
        this.parser = parser;

        InitializeComponent();

        Title = "AI Translate";
        Width = 860;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        HasMaximizeButton = false;
        HasMinimizeButton = false;

        SourceLocaleCombo.ItemsSource = langCodes;
        SourceLocaleCombo.SelectedItem = langCodes.Contains("en") ? "en"
            : langCodes.FirstOrDefault(l => l.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            ?? langCodes.FirstOrDefault();

        AzureTranslationSettings settings = settingsService.Load();
        CustomPromptBox.Text = settings.CustomPrompt;

        ResultsGrid.ItemsSource = resultItems;

        SourceLocaleCombo.SelectionChanged += (_, _) => {
            PopulateTargetLocales();
            RebuildPreview();
        };

        PopulateTargetLocales();
        RebuildPreview();
    }

    private void PopulateTargetLocales() {
        string? source = SourceLocaleCombo.SelectedItem as string;
        List<LocaleSelection> targets = langCodes
            .Where(l => l != source)
            .Select(l => new LocaleSelection(l) {
                IsSelected = true
            })
            .ToList();

        // Rebuild preview when any target checkbox changes.
        foreach (LocaleSelection t in targets) {
            t.PropertyChanged += (_, _) => RebuildPreview();
        }

        TargetLocalesPanel.ItemsSource = targets;
    }

    private void ModeRadio_Changed(object sender, RoutedEventArgs e) => RebuildPreview();

    private void RebuildPreview() {
        string? sourceLocale = SourceLocaleCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(sourceLocale)) return;

        List<string> targetLocales = (TargetLocalesPanel.ItemsSource as List<LocaleSelection>)?
            .Where(t => t.IsSelected)
            .Select(t => t.Locale)
            .ToList() ?? [];

        bool emptyOnly = EmptyOnlyRadio.IsChecked == true;

        List<TranslationItem> newItems = [];
        foreach (string targetLocale in targetLocales) {
            foreach (ArbRow row in rows) {
                string sourceText = row.Values.TryGetValue(sourceLocale!, out string src) ? src : "";
                if (string.IsNullOrEmpty(sourceText)) continue;

                string existing = row.Values.TryGetValue(targetLocale, out string ex) ? ex : "";
                if (emptyOnly && !string.IsNullOrEmpty(existing)) continue;

                newItems.Add(new TranslationItem {
                    Key = row.Key,
                    SourceText = sourceText,
                    TargetLocale = targetLocale,
                    ExistingTranslation = existing,
                });
            }
        }

        resultItems.Clear();
        foreach (TranslationItem item in newItems) {
            resultItems.Add(item);
        }

        ApplyButton.IsEnabled = false;

        StatusText.Text = resultItems.Count == 0
            ? emptyOnly
                ? "Nothing queued — all targets already have values. Switch to \"All cells\" to re-translate."
                : "Nothing queued — no source texts found for the selected locale."
            : $"{resultItems.Count} item(s) queued for translation.";
    }

    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods")]
    private async void TranslateButton_OnClick(object sender, RoutedEventArgs e) {
        try {
            if (resultItems.Count == 0) {
                StatusText.Text = "Nothing queued — adjust source locale, targets, or mode.";
                return;
            }

            AzureTranslationSettings settings = settingsService.Load();
            if (!string.IsNullOrWhiteSpace(CustomPromptBox.Text)) {
                settings.CustomPrompt = CustomPromptBox.Text.Trim();
            }

            TranslationProvider provider = GoogleProviderRadio.IsChecked == true
                ? TranslationProvider.Google
                : TranslationProvider.AzureOpenAI;

            AzureOpenAITranslationService service = new(settings, provider);
            (bool valid, string error) = service.ValidateSettings();
            if (!valid) {
                MessageBox.Show(error, "Arb.NET - Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? sourceLocale = SourceLocaleCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(sourceLocale)) return;

            // Snapshot descriptions for the source locale.
            Dictionary<string, string?> descriptions = LoadDescriptions(sourceLocale!);
            foreach (TranslationItem item in resultItems) {
                if (descriptions.TryGetValue(item.Key, out string? desc)) {
                    item.Description = desc;
                }
            }

            TranslateButton.IsEnabled = false;
            CancelTranslationButton.Visibility = Visibility.Visible;
            ApplyButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            StatusText.Text = "Starting translation...";

            cts = new CancellationTokenSource();
            int totalItems = resultItems.Count;
            int completedItems = 0;

            List<IGrouping<string, TranslationItem>> groupedByLocale = resultItems
                .GroupBy(i => i.TargetLocale)
                .ToList();

            foreach (IGrouping<string, TranslationItem> group in groupedByLocale) {
                cts.Token.ThrowIfCancellationRequested();

                List<TranslationItem> localeItems = group.ToList();
                Progress<(int completed, int total, string message)> progress = new(p => {
                    int globalCompleted = completedItems + p.completed;
                    ProgressBar.Value = (double)globalCompleted / totalItems * 100;
                    StatusText.Text = p.message;
                });

                await service.TranslateAsync(sourceLocale!, group.Key, localeItems, cts.Token, progress);
                completedItems += localeItems.Count;
            }

            ProgressBar.Value = 100;
            int filled = resultItems.Count(i => !string.IsNullOrEmpty(i.ProposedTranslation));
            StatusText.Text = $"Done — {filled} item(s) translated.";
            ApplyButton.IsEnabled = filled > 0;
        }
        catch (OperationCanceledException) {
            int filled = resultItems.Count(i => !string.IsNullOrEmpty(i.ProposedTranslation));
            StatusText.Text = $"Translation cancelled ({filled} translated).";
            ApplyButton.IsEnabled = filled > 0;
        }
        catch (Exception ex) {
            StatusText.Text = $"Error: {ex.Message}";
            ApplyButton.IsEnabled = resultItems.Any(i => !string.IsNullOrEmpty(i.ProposedTranslation));
        }
        finally {
            TranslateButton.IsEnabled = true;
            CancelTranslationButton.Visibility = Visibility.Collapsed;
            cts?.Dispose();
            cts = null;
        }
    }

    private void CancelTranslationButton_OnClick(object sender, RoutedEventArgs e) {
        cts?.Cancel();
    }

    private void ApplyButton_OnClick(object sender, RoutedEventArgs e) {
        List<TranslationItem> accepted = resultItems
            .Where(i => i.Accepted && !string.IsNullOrEmpty(i.ProposedTranslation))
            .ToList();
        if (accepted.Count == 0) return;

        IEnumerable<IGrouping<string, TranslationItem>> byLocale = accepted.GroupBy(i => i.TargetLocale);
        int appliedCount = 0;

        foreach (IGrouping<string, TranslationItem> group in byLocale) {
            ArbFile? arb = arbFiles.FirstOrDefault(f => string.Equals(f.LangCode, group.Key, StringComparison.Ordinal));
            if (arb == null) continue;

            Dictionary<string, string> updates = group.ToDictionary(i => i.Key, i => i.ProposedTranslation);
            bool success = ModifyArbFile(arb, doc => {
                foreach (KeyValuePair<string, string> update in updates) {
                    if (doc.Entries.TryGetValue(update.Key, out ArbEntry entry)) {
                        entry.Value = update.Value;
                    }
                    else {
                        doc.Entries[update.Key] = new ArbEntry {
                            Key = update.Key,
                            Value = update.Value
                        };
                    }
                }
            });

            if (success) {
                appliedCount += group.Count();
            }
        }

        AppliedChanges = true;
        StatusText.Text = $"Applied {appliedCount} translation(s).";
        ApplyButton.IsEnabled = false;
    }

    private bool ModifyArbFile(ArbFile arb, Action<ArbDocument> mutate) {
        try {
            string content = File.ReadAllText(arb.FilePath);
            ArbParseResult parsed = parser.ParseContent(content);
            if (parsed.Document == null) return false;

            mutate(parsed.Document);
            File.WriteAllText(arb.FilePath, ArbSerializer.Serialize(parsed.Document));
            return true;
        }
        catch (Exception ex) {
            MessageBox.Show($"Failed to modify {arb.FilePath}: {ex.Message}", "Arb.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private Dictionary<string, string?> LoadDescriptions(string sourceLocale) {
        Dictionary<string, string?> descriptions = new(StringComparer.Ordinal);
        ArbFile? sourceFile = arbFiles.FirstOrDefault(f => string.Equals(f.LangCode, sourceLocale, StringComparison.Ordinal));
        if (sourceFile == null) return descriptions;

        try {
            string content = File.ReadAllText(sourceFile.FilePath);
            ArbParseResult result = parser.ParseContent(content);
            if (result.Document == null) return descriptions;

            foreach (KeyValuePair<string, ArbEntry> kvp in result.Document.Entries) {
                if (!string.IsNullOrEmpty(kvp.Value.Metadata?.Description)) {
                    descriptions[kvp.Key] = kvp.Value.Metadata?.Description;
                }
            }
        }
        catch {
            /* non-critical */
        }
        return descriptions;
    }
}