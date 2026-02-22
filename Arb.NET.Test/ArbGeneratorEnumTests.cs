namespace Arb.NET.Test;

[TestFixture]
public class ArbGeneratorEnumTests {
    private static ArbDocument CreateDocument(string locale, params string[] keys) {
        Dictionary<string, ArbEntry> entries = new();
        foreach (string key in keys) {
            entries[key] = new ArbEntry {
                Key = key,
                Value = $"Localized {key}"
            };
        }
        return new ArbDocument {
            Locale = locale,
            Entries = entries
        };
    }

    private static string GenerateDispatcher(ArbDocument defaultDoc, params EnumLocalizationInfo[] enums) {
        (ArbDocument, string)[] locales = [(defaultDoc, "L_en")];
        IReadOnlyList<EnumLocalizationInfo>? enumLocalizations = enums.Length == 0 ? null : enums.ToList();
        return new ArbCodeGenerator().GenerateDispatcherClass(locales, defaultDoc, "L", "Test", enumLocalizations);
    }

    [Test]
    public void Dispatcher_WithoutEnumInfo_DoesNotContainLocalizeMethod() {
        ArbDocument doc = CreateDocument("en", "appTitle");

        string source = GenerateDispatcher(doc);

        Console.WriteLine(source);
        Assert.That(source, Does.Not.Contain("public string Localize("));
    }

    [Test]
    public void Dispatcher_WithEnumInfo_ContainsLocalizeOverload() {
        ArbDocument doc = CreateDocument("en", "myStatusActive", "myStatusInactive");

        string source = GenerateDispatcher(doc, new EnumLocalizationInfo {
            FullName = "MyNamespace.MyStatus",
            SimpleName = "MyStatus",
            Members = ["Active", "Inactive"]
        });

        Console.WriteLine(source);
        Assert.That(source, Does.Contain("public string Localize(MyNamespace.MyStatus value)"));
    }

    [Test]
    public void Dispatcher_LocalizeMethod_ArmsUseCorrectPascalCaseProperties() {
        ArbDocument doc = CreateDocument("en", "myStatusActive", "myStatusInactive");

        string source = GenerateDispatcher(doc, new EnumLocalizationInfo {
            FullName = "MyNamespace.MyStatus",
            SimpleName = "MyStatus",
            Members = ["Active", "Inactive"]
        });

        // Each arm must reference the dispatcher property derived from the ARB key:
        // "myStatusActive"   → ToPascalCase → MyStatusActive
        // "myStatusInactive" → ToPascalCase → MyStatusInactive
        Assert.That(source, Does.Contain("MyNamespace.MyStatus.Active => MyStatusActive,"));
        Assert.That(source, Does.Contain("MyNamespace.MyStatus.Inactive => MyStatusInactive,"));
    }

    [Test]
    public void Dispatcher_LocalizeMethod_ContainsFallbackToStringCall() {
        ArbDocument doc = CreateDocument("en", "myStatusActive");

        string source = GenerateDispatcher(doc, new EnumLocalizationInfo {
            FullName = "MyStatus",
            SimpleName = "MyStatus",
            Members = ["Active"]
        });

        Assert.That(source, Does.Contain("_ => value.ToString()"));
    }

    [Test]
    public void Dispatcher_MultipleEnums_EmitsOneOverloadPerType() {
        ArbDocument doc = CreateDocument("en",
                                         "myAgeYoung", "myAgeOld",
                                         "myColorRed", "myColorBlue");

        string source = GenerateDispatcher(
            doc,
            new EnumLocalizationInfo {
                FullName = "MyAge",
                SimpleName = "MyAge",
                Members = ["Young", "Old"]
            },
            new EnumLocalizationInfo {
                FullName = "MyColor",
                SimpleName = "MyColor",
                Members = ["Red", "Blue"]
            }
        );

        Assert.That(source, Does.Contain("public string Localize(MyAge value)"));
        Assert.That(source, Does.Contain("public string Localize(MyColor value)"));
    }

    [Test]
    public void Dispatcher_LocalizeMethod_CamelCasesEnumName() {
        ArbDocument doc = CreateDocument("en", "myLongEnumNameYoung");

        string source = GenerateDispatcher(doc, new EnumLocalizationInfo {
            FullName = "MyLongEnumName",
            SimpleName = "MyLongEnumName",
            Members = ["Young"]
        });

        Assert.That(source, Does.Contain("MyLongEnumName.Young => MyLongEnumNameYoung,"));
    }
}