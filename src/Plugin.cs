using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AnchorChain;
using BepInEx.Logging;
using HarmonyLib;
using SeaPower;
using SeapowerUI;
using UnityEngine;

namespace ModManagerEnhancements;

[ACPlugin(ModInfo.Id, ModInfo.Name, ModInfo.Version)]
public sealed class Plugin : IAnchorChainMod
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.Name);
    private static readonly ConditionalWeakTable<ModMenuView, ManagerController> Controllers = new();
    private static bool initialized;
    internal static ManagerController? Active { get; set; }

    public void TriggerEntryPoint()
    {
        if (initialized) return;
        initialized = true;
        Harmony? patches = null;
        GameObject? host = null;
        try
        {
            string package = Path.GetDirectoryName(typeof(Plugin).Assembly.Location)
                ?? throw new InvalidOperationException("The mod package has no directory.");
            var source = FileManager.Instance.Directories.SingleOrDefault(d => SamePath(d.DirectoryInfo.FullName, package))
                ?? throw new InvalidOperationException("The mod package is not registered by Sea Power: " + package);
            if (!source.IsChecked) { Log.LogInfo("Disabled in the mod manager; no UI changes installed."); return; }
            patches = new Harmony(ModInfo.Id);
            var constructor = AccessTools.Constructor(typeof(ModMenuView), Type.EmptyTypes)
                ?? throw new MissingMethodException(typeof(ModMenuView).FullName, ".ctor");
            patches.Patch(constructor, postfix: new HarmonyMethod(typeof(Plugin), nameof(MenuCreated)));
            host = new GameObject("ModManagerEnhancements.Runtime");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ManagerDriver>();
            if (ModMenuView.Instance != null) MenuCreated(ModMenuView.Instance);
            Log.LogInfo("Initialized " + ModInfo.Version + ". File overrides are shown in the built-in mod manager.");
        }
        catch (Exception error)
        {
            try { patches?.UnpatchSelf(); }
            catch (Exception rollback) { Error(rollback); }
            if (host != null) UnityEngine.Object.Destroy(host);
            Error(error);
        }
    }

    private static void MenuCreated(ModMenuView __instance) => Guard(() =>
    {
        _ = Controllers.GetValue(__instance, view => new ManagerController(view));
    });
    internal static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'), Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    internal static void Error(Exception error) => Log.LogError(error);
    internal static void Guard(Action action) { try { action(); } catch (Exception error) { Error(error); } }
}

public sealed class ManagerDriver : MonoBehaviour
{
    private void Update()
    {
        var controller = Plugin.Active;
        if (controller == null) return;
        try { controller.Tick(); }
        catch (Exception error) { controller.Fail(error); }
    }
}
