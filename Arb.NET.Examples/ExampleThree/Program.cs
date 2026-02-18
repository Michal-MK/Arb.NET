// See https://aka.ms/new-console-template for more information
using ExampleThree.Localizations;

Console.WriteLine("Hello, World!");

AppLocale l = new(System.Globalization.CultureInfo.GetCultureInfo("en"));
Console.WriteLine($"AppTitle       : {l.AppTitle}");