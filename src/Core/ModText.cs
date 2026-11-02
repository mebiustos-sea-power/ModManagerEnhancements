using System;

namespace ModManagerEnhancements.Core;

internal sealed class ModText
{
    private readonly bool ja;
    internal ModText(string language) { ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase); }
    internal string Header => ja ? "上書き詳細" : "File overrides";
    internal string Help => ja ? "↑ 優先されるファイル　↓ 上位MODと内容が異なるファイル。\n左の↑／↓でMODを絞り込み、選択中の枠付きマークを再クリックで解除します。" : "↑ Files with priority   ↓ Files differing from a higher-priority mod.\nClick a mod’s ↑/↓ to filter; click the selected outlined badge again to clear.";
    internal string IniNote => ja ? "INIは読み込み方法によりキー単位で統合されます。この表示はファイル候補の優先順位です。" : "INI files may be merged by key depending on how they are loaded. This shows file-provider priority.";
    internal string Scanning => ja ? "ファイルを確認しています…" : "Scanning files…";
    internal string ShowAll => ja ? "全件表示に戻す" : "Show all overrides";
    internal string FilteredSelection(string name) => ja ? "表示対象：" + name : "Filtered by: " + name;
    internal string DllTooltip => ja ? "DLLファイルを含むMODです。" : "This mod contains DLL files.";
    internal string LocalTooltip => ja ? "ローカルMOD" : "Local mod";
    internal string WorkshopTooltip => ja ? "Steamワークショップ" : "Steam Workshop";
    internal string Refresh => ja ? "再スキャン" : "Rescan";
    internal string Local => ja ? "ローカル" : "Local";
    internal string Workshop => ja ? "ワークショップ" : "Workshop";
    internal string Preferred => ja ? "優先" : "Preferred";
    internal string Lower => ja ? "下位" : "Lower priority";
    internal string Identical => ja ? "優先ファイルと同じ内容" : "Same contents as preferred file";
    internal string Failed => ja ? "確認に失敗しました。原因を解消して再スキャンしてください。" : "Scan failed. Resolve the cause and rescan.";
    internal string Summary(int count) => ja ? $"内容が異なる重複ファイル: {count}件" : $"Overlapping files with different contents: {count}";
    internal string Empty => ja ? "この範囲に、内容が異なる重複ファイルはありません。" : "No files with differing contents overlap in this selection.";
    internal string Tooltip(ModImpact impact, bool selected) => ja
        ? $"優先されるファイル: {impact.Preferred}件\n上位MODと内容が異なるファイル: {impact.Overridden}件\n" +
            (selected ? "もう一度クリックして絞り込みを解除" : "クリックして、このMODの上書き詳細に絞り込み")
        : $"Files with priority: {impact.Preferred}\nFiles differing from a higher-priority mod: {impact.Overridden}\n" +
            (selected ? "Click again to show all overrides" : "Click to show only this mod’s file overrides");
}
