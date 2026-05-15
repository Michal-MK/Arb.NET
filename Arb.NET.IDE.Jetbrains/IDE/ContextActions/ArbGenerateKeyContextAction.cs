using System;
using System.Collections.Generic;
using Arb.NET.IDE.Common.Services;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Feature.Services.Intentions;
using JetBrains.ReSharper.Feature.Services.Protocol;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Rider.Model;
using JetBrains.TextControl;
using JetBrains.Util;

namespace Arb.NET.IDE.Jetbrains.Shared.ContextActions;

[ContextAction(
    GroupType = typeof(CSharpContextActions),
    Name = "Generate ARB key",
    Description = "Adds a new entry to all .arb files for this key name and opens the ARB editor.")]
public class ArbGenerateKeyContextAction(ICSharpContextActionDataProvider provider) : IContextAction {
    private const string ARB_LOCALE_INTERFACE = "Arb.NET.IArbLocale";

    private string keyName;
    private string arbDir;

    public bool IsAvailable(IUserDataHolder cache) {
        // GetSelectedElement<IReferenceExpression> returns the whole expression (e.g. `model.Something`),
        // not the identifier token the caret is on. Walk up from the identifier instead.
        var identifier = provider.GetSelectedElement<ICSharpIdentifier>();
        if (identifier is null) return false;

        var refExpr = ReferenceExpressionNavigator.GetByNameIdentifier(identifier);
        if (refExpr is null) return false;

        // Must be a member access — qualifier.Member
        var qualifier = refExpr.QualifierExpression;
        if (qualifier is null) return false;

        // Qualifier's type must implement IArbLocale
        if (qualifier.Type() is not IDeclaredType qualifierType) return false;
        if (!ImplementsArbLocale(qualifierType)) return false;

        // The reference must be unresolved — the key doesn't exist yet
        if (refExpr.Reference.Resolve().ResolveErrorType == ResolveErrorType.OK) return false;

        var keyName = refExpr.Reference.GetName();
        if (string.IsNullOrWhiteSpace(keyName)) return false;

        var filePath = provider.SourceFile?.GetLocation().FullPath;
        var projectDir = LocalizationYamlService.FindProjectDirectory(
            System.IO.Path.GetDirectoryName(filePath) ?? string.Empty);
        if (projectDir is null) return false;

        this.keyName = keyName;
        arbDir = LocalizationYamlService.ResolveArbDirectory(projectDir);
        return true;
    }

    public IEnumerable<IntentionAction> CreateBulbItems() {
        if (keyName is null || arbDir is null) yield break;

        yield return new GenerateArbKeyBulbAction(keyName, arbDir)
            .ToContextActionIntention(IntentionsAnchors.ContextActionsAnchor);
    }

    private static bool ImplementsArbLocale(IDeclaredType type) {
        var typeElement = type.GetTypeElement();
        if (typeElement is null) return false;

        foreach (var superType in typeElement.GetAllSuperTypes()) {
            if (superType.GetClrName().FullName == ARB_LOCALE_INTERFACE) return true;
        }
        return false;
    }

    private sealed class GenerateArbKeyBulbAction : BulbActionBase {
        private readonly string _keyName;
        private readonly string _arbDir;

        public GenerateArbKeyBulbAction(string keyName, string arbDir) {
            _keyName = keyName;
            _arbDir = arbDir;
        }

        public override string Text => $"Generate ARB key '{ArbKey}'";

        private string ArbKey => char.IsUpper(_keyName[0])
            ? char.ToLower(_keyName[0]) + _keyName.Substring(1)
            : _keyName;

        protected override Action<ITextControl>? ExecutePsiTransaction(ISolution solution, IProgressIndicator progress) {
            bool added = ArbKeyService.AddKey(_arbDir, ArbKey);
            if (added) {
                ArbModel model = solution.GetProtocolSolution().GetArbModel();
                model.OpenArbEditor.Fire(new ArbOpenEditor(_arbDir, ArbKey));
            }
            return null;
        }
    }
}