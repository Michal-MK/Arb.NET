using Arb.NET.Models;

namespace Arb.NET.Test;

[TestFixture]
public class L10nConfigTests {
    [Test]
    public void Parse_IgnoresFullLineAndInlineComments() {
        L10nConfig config = L10nConfig.Parse("""
            # Arb.NET localization settings
            arb-dir: arbs # Required template directory
            template-arb-file: en.arb # Primary locale file
            output-class: AppLocale # Optional dispatcher class
            output-namespace: My.Project.Localization # Optional namespace override
            """);

        Assert.That(config.ArbDir, Is.EqualTo("arbs"));
        Assert.That(config.TemplateArbFile, Is.EqualTo("en.arb"));
        Assert.That(config.OutputClass, Is.EqualTo("AppLocale"));
        Assert.That(config.OutputNamespace, Is.EqualTo("My.Project.Localization"));
    }

    [Test]
    public void Parse_PreservesHashesInsideQuotedValues() {
        L10nConfig config = L10nConfig.Parse("""
            template-arb-file: "en#dev.arb" # comment after quoted value
            output-class: 'App#Locale' # comment after quoted value
            """);

        Assert.That(config.TemplateArbFile, Is.EqualTo("en#dev.arb"));
        Assert.That(config.OutputClass, Is.EqualTo("App#Locale"));
    }
}