namespace Arb.NET.Test;

public static class TestHelpers {
    public static ArbParseResult ParseValid(string input, bool print = false) {
        try {
            ArbParseResult result = new ArbParser().ParseContent(input);

            if (print) {
                string resultString = result.ToString();
                Console.WriteLine(resultString);
            }

            Assert.That(result.ValidationResults.IsValid, Is.True);

            return result;
        }
        catch (Exception ex) {
            Assert.Fail(ex.Message);
            throw; // This will never be reached, but it satisfies the compiler
        }
    }
    
    public static ArbParseResult ParseInvalid(string input, bool print = false) {
        try {
            ArbParseResult result = new ArbParser().ParseContent(input);

            if (print) {
                string resultString = result.ToString();
                Console.WriteLine(resultString);
            }

            Assert.That(result.ValidationResults.IsValid, Is.False);

            return result;
        }
        catch (Exception ex) {
            Assert.Fail(ex.Message);
            throw; // This will never be reached, but it satisfies the compiler
        }
    }
}