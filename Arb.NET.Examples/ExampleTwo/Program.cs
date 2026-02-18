
using ExampleTwo.Localizations;

namespace Arb.NET.ExampleTwo;

internal static class Program {
    private static void Main() {
        L l = new(System.Globalization.CultureInfo.GetCultureInfo("en"));
        Console.WriteLine($"AppTitle       : {l.AppTitle}");

        // Enum localization — [ArbLocalize] on the enum type localizes all its members
        Console.WriteLine($"MyEnumOne.Young   : {l.Localize(MyEnumOne.Young)}");
        Console.WriteLine($"MyEnumOne.GrownUp : {l.Localize(MyEnumOne.GrownUp)}");
        Console.WriteLine($"MyEnumOne.Old     : {l.Localize(MyEnumOne.Old)}");

        try {
            Console.WriteLine($"MyEnumOne.Old     : {(l as dynamic).Localize(MyEnumTwo.Happy)}");
            Console.WriteLine("This line should not be reached as this enum does not participate in the generation, expected an error above.");
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex) {
            Console.WriteLine($"Expected error: {ex.Message}");
        }
    }
}