using AC.Scene.Touch;
using AC_RoomDarkener.Core;
using HarmonyLib;
using UnityEngine;

namespace AC_RoomDarkener;

/// <summary>
/// Harmony パッチは 3 か所だけ。
///  (1) 演出 UI (TouchController) の構築完了  (2) H 開始直前  (3) 時間帯変更
/// マップ読み込みや H 終了は毎フレームの軽いポーリングで拾う。
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatches
{
    [HarmonyPatch(typeof(TouchController), "Setup")]
    [HarmonyPostfix]
    private static void TouchControllerSetupPostfix(TouchController __instance)
    {
        try
        {
            RoomDarkness.OnTouchUiReady(__instance);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("[Patch] TouchController.Setup の処理に失敗: " + e.Message);
        }
    }

    [HarmonyPatch(typeof(H.HScene), "Initialize", new[] { typeof(H.HScene.InputParameter), typeof(Transform) })]
    [HarmonyPrefix]
    private static void HSceneInitializePrefix()
    {
        try
        {
            RoomDarkness.OnHStarting();
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("[Patch] HScene.Initialize の処理に失敗: " + e.Message);
        }
    }

    [HarmonyPatch(typeof(Manager.Environment), nameof(Manager.Environment.ChangeTimeZone))]
    [HarmonyPostfix]
    private static void ChangeTimeZonePostfix()
    {
        try
        {
            RoomDarkness.OnTimeZoneChanged();
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("[Patch] Environment.ChangeTimeZone の処理に失敗: " + e.Message);
        }
    }
}
