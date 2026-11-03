using System;
using System.IO;
using System.Threading;
using ModManagerEnhancements.Core;

internal static class SearchTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Match(string query, string name, string workshop = "") =>
        new ModListFilter(query, ModContent.None).Matches(name, workshop, ModContent.None);

    internal static void Run(Action<string, Action> test)
    {
        test("MOD search uses case-insensitive partial text and literal punctuation", () =>
        {
            Require(Match(" f-15 ", "JASDF F-15J") && Match("[beta].", "Test [Beta]. build") &&
                Match("航空", "航空部隊MOD") && !Match("F-15", "Other mod"), "Plain name search changed its scope or treated text as a pattern.");
            Require(Match("3832319500", "Unrelated display name", "3832319500") &&
                !Match("3832319500", "Unrelated display name"), "Workshop IDs were ignored or invented for local mods.");
        });
        test("wildcards match entire names while ordinary searches remain partial", () =>
        {
            Require(Match("*F-15*", "JASDF F-15J") && Match("Japan*", "Japanese Names") &&
                !Match("Japan*", "Other Japanese Names"), "Wildcard matching did not require the complete name.");
            Require(Match("F-?A", "F-2A") && !Match("F-?A", "F-15A") &&
                Match("艦?隊", "艦船隊") && Match("a**b*", "ab") && Match("*", "") && Match("*", "*decorated title"),
                "Question marks, empty stars, repeated stars or non-Latin names failed.");
            Require(Match("ship", "WarshipA Pack") && Match("ship?", "ShipA") && !Match("ship?", "WarshipA Pack") &&
                Match("ship*", "ShipA") && Match("ship*", "Ship Pack") && Match("ship*", "ship") &&
                !Match("ship*", "WarshipA Pack") && Match("*ship*", "WarshipA Pack") && !Match("ship?", "ship") &&
                Match("ship*gun", "Ship Main GUN") && !Match("?ship", "Aship Pack"),
                "Plain text, whole-name wildcards, zero-length stars or required question-mark characters differ from the agreed examples.");
            Require(!Match("*missing", "Long title without the requested suffix") &&
                Match("*9500", "Other", "3832319500"), "Wildcard suffix or Workshop ID matching failed.");
        });
        test("name search and content kind combine without requiring an exclusive type", () =>
        {
            var mixed = ModContent.Missions | ModContent.Dll;
            Require(new ModListFilter("*1985*", ModContent.Missions).Matches("Mission 1985", "", mixed) &&
                new ModListFilter("*1985*", ModContent.Dll).Matches("Mission 1985", "", mixed) &&
                !new ModListFilter("*1985*", ModContent.Units).Matches("Mission 1985", "", mixed) &&
                !new ModListFilter("1986", ModContent.Missions).Matches("Mission 1985", "", mixed),
                "Query and kind were combined with OR or a mixed-content MOD lost a matching kind.");
            Require(!new ModListFilter(" ", ModContent.None).IsActive && new ModListFilter("", ModContent.Dll).IsActive,
                "Filter activity did not follow the actual search and kind conditions.");
        });
        test("content inspection identifies mixed disabled MODs without parsing or locking files", () =>
        {
            using var f = new Program.Fixture(); var source = f.Source("Mixed", false);
            f.Write(source, "Missions/Example/Blue.INI", "invalid and locked INI");
            f.Write(source, "campaigns/Example.ini", "campaign"); f.Write(source, "aircraft/test.ini", "unit");
            f.Write(source, "plugins/Native.DLL", "not an assembly");
            using var locked = new FileStream(Path.Combine(source.Directory, "Missions", "Example", "Blue.INI"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var inventory = new ModContentScanner().Scan(new[] { source }, CancellationToken.None);
            Require(inventory.Kinds(source.Id) == (ModContent.Missions | ModContent.Campaigns | ModContent.Units | ModContent.Dll),
                "An inactive mixed MOD was misclassified or its contents were read.");
        });
        test("mission classification ignores metadata auxiliary folders and unrelated paths", () =>
        {
            using var f = new Program.Fixture(); var source = f.Source("Data");
            foreach (string path in new[] { "missions/Folder/_info.ini", "missions/Scenario_data/objects.ini",
                "missions/Scenario_briefing/map.ini", "missions/Scenario_temp/work.ini", "missions/.hidden/unused.ini",
                "backup/missions/example.ini", "campaigns/_info.ini", "preview.jpg" }) f.Write(source, path, "data");
            Require(new ModContentScanner().Scan(new[] { source }, CancellationToken.None).Kinds(source.Id) == ModContent.None,
                "Non-entry files manufactured a supported content kind.");
            f.Write(source, "missions/Scenario/Blue.ini", "mission");
            Require(new ModContentScanner().Scan(new[] { source }, CancellationToken.None).Kinds(source.Id) == ModContent.Missions,
                "A mission in a named subfolder was missed.");
        });
        test("content rescan observes kind additions and removals", () =>
        {
            using var f = new Program.Fixture(); var source = f.Source("Source"); var scanner = new ModContentScanner();
            f.Write(source, "vessels/vessel_names.ini", "names");
            Require(scanner.Scan(new[] { source }, CancellationToken.None).Kinds(source.Id) == ModContent.Units,
                "Unit data replacements were mistaken for requiring a newly added unit.");
            File.Delete(Path.Combine(source.Directory, "vessels", "vessel_names.ini"));
            f.Write(source, "campaigns/new.ini", "campaign");
            Require(scanner.Scan(new[] { source }, CancellationToken.None).Kinds(source.Id) == ModContent.Campaigns,
                "A rescan retained a removed kind or missed a new one.");
        });
    }
}
