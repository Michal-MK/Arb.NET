# Changelog

All notable changes to the Arb.NET Rider plugin will be documented in this file.

## 0.0.6
- Fix ARB directory discovery when Rider is opened from a `.csproj` without a `.sln`
- Improve source navigation support for generated localization members
- Pick up generated XML documentation and locale-ordering fixes in Rider tooltips and editor views

## 0.0.5
- Add `l10n.yaml` templates and related Rider-side documentation updates
- Normalize generated file encoding to avoid BOM-related churn in edited files
- Refresh examples and release documentation

## 0.0.4
- Refresh Rider plugin branding, icon assets, and marketplace metadata
- Clean up plugin packaging and release documentation
- Bundle the first pass of plugin polish after the initial public release

## 0.0.3
- CSV import/export support in the ARB editor
- Generation fixes and quality-of-life improvements
- Fix warnings and simplify key removal when only one key is filtered
- Refactored code generation

## 0.0.2
- Prefilter the selected key when navigating via Go-to-Declaration
- Context menu action for `l10n.yaml` and single-instance ARB editor
- Key filtering in the ARB editor
- Improved XAML hover documentation tooltips
- XAML syntax highlighting, code completion, and Go-to-Definition for ARB keys
- Plugin build improvements

## 0.0.1
- Smart ARB editor with side-by-side multi-locale editing
- AI-powered and Google translation sources
- Add / remove keys and locales from the editor
- Column width and order persistence
- XAML integration for .NET MAUI projects
- Go to ARB editor from XAML markup extensions
- Initial release
