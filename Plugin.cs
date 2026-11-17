using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace AC_RoomDarkener;

/// <summary>プラグインの入口。Config の生成と Harmony パッチ適用、常駐コンポーネントの追加だけを行う。</summary>
[BepInPlugin(PluginGuid, PluginName, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Aicomi.exe")]
public sealed class Plugin : BasePlugin
{
    /// <summary>他 MOD と衝突しない独自 GUID。</summary>
    public const string PluginGuid = "samur.aicomi.roomdarkener";

    public const string PluginName = "AC_RoomDarkener";

    internal static new ManualLogSource Log = null!;

    internal static Plugin Instance = null!;

    public override void Load()
    {
        Log = base.Log;
        Instance = this;

        DarkenerConfig.Initialize(Config);

        try
        {
            Harmony.CreateAndPatchAll(typeof(HarmonyPatches), PluginGuid);
            Log.LogInfo("[Init] Harmony パッチを適用しました。");
        }
        catch (Exception e)
        {
            // パッチが 1 つ失敗してもボタンと手動トグルは動かせるようにする
            Log.LogError("[Init] Harmony パッチの適用に失敗: " + e);
        }

        AddComponent<RoomDarkenerBehaviour>();

        Log.LogInfo($"{PluginName} {MyPluginInfo.PLUGIN_VERSION} loaded. " +
                    $"Enabled={DarkenerConfig.Enabled.Value}, " +
                    $"OnlyAtNight={DarkenerConfig.OnlyAtNight.Value}, " +
                    $"Brightness={DarkenerConfig.Brightness.Value:0.00}");
    }
}
