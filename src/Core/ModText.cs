using System;
using System.Globalization;

namespace ModManagerEnhancements.Core;

internal sealed class ModText
{
    internal const string Section = "ModManagerEnhancements";
    private readonly Func<string, string> lookup;
    internal ModText(Func<string, string> lookup) { this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup)); }
    private string Get(string key) => lookup(key);
    private string Format(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, Get(key), values);
    internal string SearchPlaceholder => Get("SearchPlaceholder");
    internal string ClearSearch => Get("ClearSearch");
    internal string NoMatchingMods => Get("NoMatchingMods");
    internal string KindScanning => Get("KindScanning");
    internal string KindFailed => Get("KindFailed");
    internal string Header => Get("Header");
    internal string Help => Get("Help");
    internal string IniNote => Get("IniNote");
    internal string Scanning => Get("Scanning");
    internal string ShowAll => Get("ShowAll");
    internal string DllTooltip => Get("DllTooltip");
    internal string CompatibilityTooltip => Get("CompatibilityTooltip");
    internal string WorkshopUpdatedTooltip => Get("WorkshopUpdatedTooltip");
    internal string UpdatedFilter => Get("UpdatedFilter");
    internal string SetCompatibility => Get("SetCompatibility");
    internal string RestoreCompatibility => Get("RestoreCompatibility");
    internal string RestoreButton => Get("RestoreButton");
    internal string CompatibilityModifiedTooltip => Get("CompatibilityModifiedTooltip");
    internal string CompatibilityTitle => Get("CompatibilityTitle");
    internal string Cancel => Get("Cancel");
    internal string Sync => Get("Sync");
    internal string SyncTitle => Get("SyncTitle");
    internal string SyncConfirmation => Get("SyncConfirmation");
    internal string SortDependencies => Get("SortDependencies");
    internal string EnabledFirst => Get("EnabledFirst");
    internal string EnabledFirstTooltip => Get("EnabledFirstTooltip");
    internal string Change => Get("Change");
    internal string Close => Get("Close");
    internal string LocalTooltip => Get("LocalTooltip");
    internal string WorkshopTooltip => Get("WorkshopTooltip");
    internal string Refresh => Get("Refresh");
    internal string Local => Get("Local");
    internal string Workshop => Get("Workshop");
    internal string Preferred => Get("Preferred");
    internal string Lower => Get("Lower");
    internal string Identical => Get("Identical");
    internal string Failed => Get("Failed");
    internal string Empty => Get("Empty");
    internal string KindName(ModContent kind) => Get(kind switch
    {
        ModContent.Missions => "KindMissions",
        ModContent.Campaigns => "KindCampaigns",
        ModContent.Units => "KindUnits",
        ModContent.Dll => "KindDll",
        _ => "KindAll"
    });
    internal string FilteredSelection(string name) => Format("FilteredSelection", name);
    internal string CompatibilityConfirmation(string name, string version) => Format("CompatibilityConfirmation", name, version);
    internal string CompatibilityRestoreConfirmation(string name) => Format("CompatibilityRestoreConfirmation", name);
    internal string CompatibilityFailure(string reason) => Format("CompatibilityFailure", reason);
    internal string Summary(int count) => Format("Summary", count);
    internal string Tooltip(ModImpact impact, bool selected) => Format("Tooltip", impact.Preferred, impact.Overridden,
        Get(selected ? "ClearFilterAction" : "FilterOverridesAction"));
}
