using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ModManagerEnhancements;
using ModManagerEnhancements.Core;
using SeaPower;

internal static class LocalizationUiTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(Action<string, Action> test)
    {
        test("native language files provide Japanese English multiline text and formatted values", () =>
        {
            var english = Load(TestText.FileFor("en"));
            var japanese = Load(TestText.FileFor("ja"));
            foreach (var language in new[] { english, japanese })
            {
                var handler = Handler(language, english);
                var text = GameText.Create(() => handler);
                Require(text.SyncConfirmation.Contains("\n\n") && !text.SyncConfirmation.Contains("\\n") &&
                    text.CompatibilityConfirmation("Example {1}", "0.9.3").Contains("Example {1}") &&
                    text.CompatibilityConfirmation("Example", "0.9.3").Contains("0.9.3"),
                    "Native parsing lost newlines or interpreted braces in a replacement argument.");
            }
            var en = Handler(english, english);
            Require(GameText.Create(() => en).Help.Contains("filter; click"), "Native parsing truncated a semicolon in the text.");
        });
        test("a separate translation mod supplies a third language with native per-key English fallback", () =>
        {
            using var f = new Translation();
            File.WriteAllText(f.File, @"{""ModManagerEnhancements"": {""SearchPlaceholder"": ""Rechercher des mods"", ""Sync"": ""Synchroniser"", ""CompatibilityConfirmation"": ""Version {1} : {0}""}}");
            var english = Load(TestText.FileFor("en"));
            var handler = Handler(Load(f.File), english);
            var text = GameText.Create(() => handler);
            Require(text.SearchPlaceholder == "Rechercher des mods" && text.Sync == "Synchroniser" &&
                text.UpdatedFilter == "Updated" && text.CompatibilityConfirmation("Nom {1}", "0.9.3") == "Version 0.9.3 : Nom {1}",
                "An external third-language file required binary changes, failed fallback, or could not reorder arguments.");
            NativeMessageBox? dialog = null;
            var open = new HashSet<NativeMessageBox>(); int executed = 0;
            using var confirmation = new SyncConfirmation(new DelegateCommand(_ => executed++), text,
                message => { dialog = message; open.Add(message); }, message => open.Remove(message), open.Contains, error => throw error);
            confirmation.Execute(null);
            Require(dialog!.AcknowledgeCommandLabel == "Synchroniser" && dialog.CancelCommandLabel == "Cancel" && executed == 0,
                "The actual confirmation did not combine the supplied translation with native fallback.");
            dialog.CancelCommand.Execute(null);
            Require(executed == 0 && open.Count == 0, "Localizing the confirmation changed cancellation behavior.");
        });
        test("existing text follows an entirely replaced native language handler after translation reload", () =>
        {
            using var f = new Translation();
            var english = Load(TestText.FileFor("en"));
            var current = Handler(english, english);
            var old = current;
            var text = GameText.Create(() => current);
            Require(text.Sync == "Sync", "The initial native context was not used.");
            File.WriteAllText(f.File, @"{""ModManagerEnhancements"": {""Sync"": ""Synchroniser""}}");
            current = Handler(Load(f.File), english);
            Require(!ReferenceEquals(old, current) && text.Sync == "Synchroniser", "The text adapter retained the old handler.");
            File.WriteAllText(f.File, @"{""ModManagerEnhancements"": {""Sync"": ""Nouvelle traduction""}}");
            current = Handler(Load(f.File), english);
            Require(text.Sync == "Nouvelle traduction", "Editing and reloading a translation required a new DLL or ModText instance.");
        });
    }

    private static Dictionary<string, string> Load(string path)
    {
        var data = LocalizationLoader.LoadFileAsData(path, Path.GetFileNameWithoutExtension(path));
        var result = new Dictionary<string, string>();
        foreach (var section in data)
        foreach (var key in section.Value) result.Add((section.Key + key.Key).ToLower(), key.Value);
        return result;
    }
    private static LanguageResourceHandler Handler(Dictionary<string, string> selected, Dictionary<string, string> english)
    {
#pragma warning disable SYSLIB0050
        var handler = (LanguageResourceHandler)FormatterServices.GetUninitializedObject(typeof(LanguageResourceHandler));
#pragma warning restore SYSLIB0050
        // Exercise the native managed localization methods without creating a Unity component or touching its singleton.
        GC.SuppressFinalize(handler);
        typeof(LanguageResourceHandler).GetField("_languageDictionary", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(handler, new DictionaryWithFallbacks<string, string>("MISSING TEXT", selected, english));
        typeof(LanguageResourceHandler).GetField("keyDictionary", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(handler, new ConcurrentDictionary<(string, string), string>());
        return handler;
    }
    private sealed class Translation : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "mme-translator-" + Guid.NewGuid().ToString("N"));
        internal string File { get; }
        internal Translation()
        {
            var folder = Path.Combine(root, "CommunityTranslation", "language_fr");
            Directory.CreateDirectory(folder); File = Path.Combine(folder, "mod_manager_enhancements.json");
        }
        public void Dispose() { Directory.Delete(root, true); }
    }
}
