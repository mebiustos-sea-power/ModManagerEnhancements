using System;
using System.Collections.Generic;
using ModManagerEnhancements.Core;

internal static class LocalizationTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(Action<string, Action> test)
    {
        test("translated format arguments can be reordered and keep arbitrary mod names intact", () =>
        {
            var text = new ModText(key => key == "CompatibilityConfirmation" ? "{1}: {0} / {{example}}" : throw new KeyNotFoundException(key));
            string name = "Example {1} = [MOD]";
            Require(text.CompatibilityConfirmation(name, "0.9.3") == "0.9.3: " + name + " / {example}",
                "Translation formatting reordered the wrong values or interpreted braces inside a mod name.");
        });
        test("invalid translated placeholders surface instead of silently replacing translator text", () =>
        {
            var text = new ModText(_ => "{3}");
            try { text.Summary(2); throw new Exception("An invalid translator placeholder was silently accepted."); }
            catch (FormatException) { }
        });
    }
}
