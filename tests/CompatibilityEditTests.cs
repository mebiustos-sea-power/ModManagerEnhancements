using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ModManagerEnhancements.Core;

internal static class CompatibilityEditTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Fails(Action action)
    {
        bool failed = false;
        try { action(); } catch (Exception error) when (error is ArgumentException || error is IOException || error is InvalidDataException) { failed = true; }
        Require(failed, "An unsafe or invalid compatibility operation succeeded.");
    }

    internal static void Run(Action<string, Action> test)
    {
        test("compatibility edit and restore preserve settings comments encodings and line endings", () =>
        {
            foreach (var encoding in new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true), new UnicodeEncoding(false, true), new UnicodeEncoding(true, true) })
                foreach (string newline in new[] { "\n", "\r\n" })
                {
                    using var f = new FileFixture();
                    string input = string.Join(newline, new[] { "[Language_ja]", "Name=日本語MOD", "ExactVersion=unrelated", "", "[cOmPaTiBiLiTy] // version settings", "  exactversion = 0.9.0  // author's comment", "LessThanVersion=0.9.1", "[Replaces]", "Folder1=ui" });
                    File.WriteAllText(f.Path, input, encoding);
                    byte[] original = File.ReadAllBytes(f.Path);
                    var edit = CompatibilityEdit.Prepare(f.Path, "0.9.3-261116");
                    Require(File.ReadAllBytes(f.Path).SequenceEqual(original), "Opening confirmation wrote the file.");
                    edit.Commit();
                    string changed = File.ReadAllText(f.Path);
                    Require(changed.Contains("  exactversion = 0.9.3-261116  // author's comment") &&
                        changed.Contains("Name=日本語MOD") && changed.Contains("ExactVersion=unrelated") &&
                        CompatibilityEdit.Inspect(f.Path) == CompatibilityOverrideState.Modified,
                        "The edited setting, other metadata or its marker was incorrect.");
                    CompatibilityEdit.PrepareRestore(f.Path).Commit();
                    Require(File.ReadAllBytes(f.Path).SequenceEqual(original) &&
                        CompatibilityEdit.Inspect(f.Path) == CompatibilityOverrideState.None,
                        "Restore lost original bytes or left a changed marker.");
                    Require(Directory.GetFiles(f.Root).Length == 1, "A temporary file or sidecar remained.");
                }
        });
        test("range-only declarations are restored by removing the added exact version", () =>
        {
            using var f = new FileFixture();
            const string input = "[Compatibility]\r\nApproximateVersion=0.8.0\r\nExcludeVersions=0.9.3\r\n[Language_en]\r\nName=Example\r\n";
            File.WriteAllText(f.Path, input, new UTF8Encoding(false));
            CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
            string changed = File.ReadAllText(f.Path);
            Require(changed.Contains("[Compatibility]\r\nExactVersion=0.9.3\r\nApproximateVersion=0.8.0\r\nExcludeVersions=0.9.3"),
                "The edit changed the author's other constraints.");
            CompatibilityEdit.PrepareRestore(f.Path).Commit();
            Require(File.ReadAllText(f.Path) == input, "An added ExactVersion survived restore.");
        });
        test("duplicate exact-version lines retain their individual original values", () =>
        {
            using var f = new FileFixture();
            const string input = "[Compatibility]\nExactVersion=0.8.7\nExactVersion=0.9.0\n[Language_en]\nExactVersion=keep\n[Compatibility]\nExactVersion=0.9.1";
            File.WriteAllText(f.Path, input);
            CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
            string changed = File.ReadAllText(f.Path);
            Require(Regex.Matches(changed, @"(?m)^ExactVersion=0\.9\.3(?:\r?$)").Count == 3 &&
                changed.Contains("[Language_en]\nExactVersion=keep"), "Duplicate entries or another section were edited incorrectly.");
            CompatibilityEdit.PrepareRestore(f.Path).Commit();
            Require(File.ReadAllText(f.Path) == input, "Duplicate values were not restored individually.");
        });
        test("a pending confirmation does not overwrite a concurrent Workshop update", () =>
        {
            using var f = new FileFixture();
            File.WriteAllText(f.Path, "[Compatibility]\nExactVersion=0.9.0\n");
            var edit = CompatibilityEdit.Prepare(f.Path, "0.9.3");
            const string updated = "[Compatibility]\nApproximateVersion=0.9.3\n[Language_en]\nName=Updated by author\n";
            File.WriteAllText(f.Path, updated);
            Fails(edit.Commit);
            Require(File.ReadAllText(f.Path) == updated, "The update was overwritten by stale confirmation.");
        });
        test("invalid versions and missing compatibility information fail without modifying a file", () =>
        {
            using var f = new FileFixture();
            const string input = "[Language_en]\nName=Example\n";
            File.WriteAllText(f.Path, input);
            foreach (string version in new[] { "0.9.3\nName=bad", "0.9.3" })
            {
                Fails(() => CompatibilityEdit.Prepare(f.Path, version).Commit());
                Require(File.ReadAllText(f.Path) == input, "Invalid metadata was modified.");
            }
        });
        test("later overrides keep the setting from before the first MME edit", () =>
        {
            foreach (string input in new[] { "[Compatibility]\nExactVersion=0.8.7\n", "[Compatibility]\nApproximateVersion=0.8.0\n" })
            {
                using var f = new FileFixture();
                File.WriteAllText(f.Path, input);
                CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
                CompatibilityEdit.Prepare(f.Path, "0.9.4").Commit();
                Require(CompatibilityEdit.Inspect(f.Path) == CompatibilityOverrideState.Modified &&
                    File.ReadAllText(f.Path).Contains("ExactVersion=0.9.4"), "A later override lost its valid record.");
                CompatibilityEdit.PrepareRestore(f.Path).Commit();
                Require(File.ReadAllText(f.Path) == input, "Restore returned to an earlier MME override rather than the author setting.");
            }
        });
        test("restore keeps unrelated edits and other MME section keys", () =>
        {
            using var f = new FileFixture();
            const string input = "[Language_en]\nName=Before\n[Compatibility]\nExactVersion=0.8.7\nLessThanVersion=0.10.0\n[ModManagerEnhancements]\nOtherSetting=keep";
            File.WriteAllText(f.Path, input);
            CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
            File.WriteAllText(f.Path, File.ReadAllText(f.Path).Replace("Name=Before", "Name=After").Replace("LessThanVersion=0.10.0", "LessThanVersion=0.11.0"));
            CompatibilityEdit.PrepareRestore(f.Path).Commit();
            Require(File.ReadAllText(f.Path) == input.Replace("Name=Before", "Name=After").Replace("LessThanVersion=0.10.0", "LessThanVersion=0.11.0"),
                "Restore overwrote changes outside the MME-controlled setting.");
        });
        test("restore accepts manual value and comment edits while keeping unrelated data", () =>
        {
            using var f = new FileFixture();
            File.WriteAllText(f.Path, "[Compatibility]\nExactVersion=0.8.7\n");
            CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
            File.WriteAllText(f.Path, File.ReadAllText(f.Path).Replace("ExactVersion=0.9.3", "ExactVersion = 0.9.4 ; my note"));
            Require(CompatibilityEdit.Inspect(f.Path) == CompatibilityOverrideState.Modified, "Manual edits hid the saved original setting.");
            CompatibilityEdit.PrepareRestore(f.Path).Commit();
            Require(File.ReadAllText(f.Path) == "[Compatibility]\nExactVersion = 0.8.7 ; my note\n", "Restore rejected manual edits or replaced their formatting and comment.");
        });
        test("restore handles removed keys and preserves comments when the original key was absent", () =>
        {
            using var f = new FileFixture();
            File.WriteAllText(f.Path, "[Compatibility]\nExactVersion=0.8.7\n");
            CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
            File.WriteAllText(f.Path, File.ReadAllText(f.Path).Replace("ExactVersion=0.9.3\n", ""));
            CompatibilityEdit.PrepareRestore(f.Path).Commit();
            Require(File.ReadAllText(f.Path).Contains("ExactVersion=0.8.7"), "Restore failed to reinsert a manually removed key.");
            File.WriteAllText(f.Path, "[Compatibility]\nApproximateVersion=0.8.0\n");
            CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
            File.WriteAllText(f.Path, File.ReadAllText(f.Path).Replace("ExactVersion=0.9.3", "ExactVersion=0.9.4 ; keep this note"));
            CompatibilityEdit.PrepareRestore(f.Path).Commit();
            Require(File.ReadAllText(f.Path) == "[Compatibility]\n; keep this note\nApproximateVersion=0.8.0\n", "Removing an added key also removed the user's comment.");
        });
        test("invalid saved records fail explicitly without fabricating an original setting", () =>
        {
            using var f = new FileFixture();
            File.WriteAllText(f.Path, "[Compatibility]\nExactVersion=0.8.7\n");
            Fails(() => CompatibilityEdit.PrepareRestore(f.Path).Commit());
            CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
            File.WriteAllText(f.Path, Regex.Replace(File.ReadAllText(f.Path), @"(?m)^OriginalExactVersionData=.*$", "OriginalExactVersionData=invalid!"));
            byte[] broken = File.ReadAllBytes(f.Path);
            Fails(() => CompatibilityEdit.Inspect(f.Path));
            Fails(() => CompatibilityEdit.PrepareRestore(f.Path).Commit());
            Require(File.ReadAllBytes(f.Path).SequenceEqual(broken), "A broken record was silently repaired or used for restoration.");
        });
        test("restore preserves final-line boundaries and pre-existing empty metadata sections", () =>
        {
            foreach (string input in new[]
            {
                "[Compatibility]", "[Compatibility]\nExactVersion=",
                "[Compatibility]\nApproximateVersion=0.8.0\n[ModManagerEnhancements]",
                "[ModManagerEnhancements]\nOther=keep\n[Compatibility]\r\nExactVersion=0.8.7"
            })
            {
                using var f = new FileFixture();
                File.WriteAllText(f.Path, input);
                byte[] original = File.ReadAllBytes(f.Path);
                CompatibilityEdit.Prepare(f.Path, "0.9.3").Commit();
                CompatibilityEdit.PrepareRestore(f.Path).Commit();
                Require(File.ReadAllBytes(f.Path).SequenceEqual(original), "Round-trip changed an original final line or section boundary.");
            }
        });
        test("metadata refresh clears relative and full-path caches before a later menu reads the file", () =>
        {
            foreach (string relative in new[] { @"Example\_info.ini", @"..\..\..\workshop\content\1286220\3755769170\_info.ini" })
            {
                const string physical = @"N:\Steam\MOD\_info.ini";
                const string unrelated = @"AnotherMOD\_info.ini";
                var cache = new Dictionary<string, string>
                {
                    [relative] = "old range", [physical] = "old range", [unrelated] = "other mod"
                };
                CachedIni Open(string key)
                {
                    if (!cache.TryGetValue(key, out string? value)) cache[key] = value = "new exact version";
                    return new CachedIni { Key = key, Value = value };
                }
                void Invalidate(string path)
                {
                    string needle = path.Replace('\\', '/');
                    foreach (string key in cache.Keys.ToArray())
                        if (key.Replace('\\', '/').IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) cache.Remove(key);
                }
                var refreshed = IniRefresh.Read(physical, _ => Open(relative), ini => ini.Key, Invalidate, ini =>
                {
                    ini.Key = physical; ini.Value = "new exact version"; cache[physical] = ini.Value;
                });
                Require(refreshed.Value == "new exact version" && Open(relative).Value == "new exact version" &&
                    Open(physical).Value == "new exact version" && cache[unrelated] == "other mod",
                    "A menu reopened after the edit read stale metadata, or another mod's cache was cleared.");
            }
        });
    }

    private sealed class CachedIni
    {
        internal string Key { get; set; } = "";
        internal string Value { get; set; } = "";
    }
    private sealed class FileFixture : IDisposable
    {
        internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mme-compatibility-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Root, "_info.ini");
        internal FileFixture() { Directory.CreateDirectory(Root); }
        public void Dispose() { Directory.Delete(Root, true); }
    }
}
