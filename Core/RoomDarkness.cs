using System;
using System.Collections.Generic;
using System.Text;
using AC.Scene.Touch;
using AC_RoomDarkener.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AC_RoomDarkener.Core;

/// <summary>
/// 消灯状態の管理。H シーン UI (H.HSprite) と探索の演出 UI (TouchController) の
/// どちらでも動くようにし、H 終了 / マップ変更 / シーン変更 / ボタン操作を 1 か所で処理する。
/// Tick は 0.2 秒間隔で、重い走査は「消灯した瞬間」だけ行う。
/// </summary>
internal static class RoomDarkness
{
    /// <summary>ボタンを差し込む UI と、その UI が持つライト。</summary>
    private sealed class UiAnchor
    {
        public GameObject Root = null!;
        public Button? PreferredButton;
        public Light[] ExtraLights = Array.Empty<Light>();
        public string Kind = string.Empty;
    }

    private static LightStateSnapshot? _snapshot;
    private static UiAnchor? _anchor;
    private static bool _dark;
    private static bool _hActive;

    private static int _lastMapId = int.MinValue;
    private static string _lastScene = string.Empty;
    private static float _nextTick;
    private static float _nextNightCheck;
    private static bool _night = true;
    private static string _nightSource = string.Empty;
    private static float _nextAnchorLog;
    private static bool _panelPathsLogged;

    public static bool IsDark => _dark;

    // ------------------------------------------------------------------ tick

    public static void HandleHotkey()
    {
        if (!DarkenerConfig.Enabled.Value)
            return;

        if (!DarkenerConfig.Hotkey.Value.IsPressedKey())
            return;

        if (DarkenerConfig.HotkeyRequireCtrl.Value &&
            !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            return;

        Plugin.Log.LogInfo($"[Hotkey] {DarkenerConfig.Hotkey.Value} を検知しました。");
        Toggle("hotkey");
    }

    public static void Tick()
    {
        try
        {
            if (!DarkenerConfig.Enabled.Value)
            {
                if (_dark)
                    Restore("plugin disabled");

                DarkenButton.Destroy();
                return;
            }

            if (Time.realtimeSinceStartup < _nextTick)
                return;

            _nextTick = Time.realtimeSinceStartup + 0.2f;

            // 1) シーンが変わったら必ず復元して状態を捨てる
            string scene = GetSceneName();
            if (!string.Equals(scene, _lastScene, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(_lastScene))
                    Restore($"scene changed: {_lastScene} -> {scene}");

                _lastScene = scene;
                _lastMapId = int.MinValue;
                DarkenButton.Destroy();
            }

            // 2) H シーンの開始/終了。開始時は前の UI の消灯を必ず戻す。
            bool h = IsHActive();
            if (h != _hActive)
            {
                Restore(h ? "H start" : "H end");
                DarkenButton.Destroy();
                _hActive = h;
                _lastMapId = GetMapId();
            }

            // 3) マップが変わったら復元 (新しいマップは素の状態から)
            int mapId = GetMapId();
            if (mapId != _lastMapId)
            {
                if (_lastMapId != int.MinValue && _dark)
                    Restore($"map changed: {_lastMapId} -> {mapId}");

                _lastMapId = mapId;
                DarkenButton.Destroy();
            }

            // 4) 夜判定 (1 秒ごと)
            if (Time.realtimeSinceStartup >= _nextNightCheck)
            {
                _nextNightCheck = Time.realtimeSinceStartup + 1f;
                bool night = NightDetector.IsNight(h, out string source);
                if (night != _night || !string.Equals(source, _nightSource, StringComparison.Ordinal))
                {
                    Plugin.Log.LogInfo($"[Night] {(night ? "夜" : "夜ではない")} ({source})");
                    _nightSource = source;
                }
                _night = night;
            }

            // 5) 操作対象の UI を決める
            var anchor = ResolveAnchor(h);
            if (anchor == null)
            {
                if (Time.realtimeSinceStartup >= _nextAnchorLog)
                {
                    _nextAnchorLog = Time.realtimeSinceStartup + 3f;
                    Plugin.Log.LogInfo("[UI] 対象 UI を待機中: " + DescribeTarget(h));
                }

                if (_dark)
                    Restore("target UI closed");

                DarkenButton.Destroy();
                _anchor = null;
                return;
            }

            if (_anchor == null || _anchor.Root != anchor.Root)
            {
                Plugin.Log.LogInfo($"[UI] 対象 UI = {anchor.Kind} ({anchor.Root.name})");
                _anchor = anchor;
                DarkenButton.Destroy();
            }

            if (!_night)
            {
                if (_dark)
                    Restore("not night");

                DarkenButton.Destroy();
                return;
            }

            if (DarkenerConfig.ShowButton.Value)
                DarkenButton.Ensure(anchor.Root, anchor.PreferredButton, _dark);
            else
                DarkenButton.Destroy();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Tick] 例外: " + e);
        }
    }

    // ------------------------------------------------------------------ toggle

    public static void Toggle(string reason)
    {
        try
        {
            if (_dark)
                Restore(reason);
            else
                Apply(reason);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Toggle] 例外: " + e);
        }
    }

    private static void Apply(string reason)
    {
        // 二重適用を避ける
        if (_snapshot != null)
            Restore("re-apply");

        var settings = DarkenerSettings.FromConfig();
        var extra = _anchor != null ? _anchor.ExtraLights : Array.Empty<Light>();
        var kind = _anchor != null ? _anchor.Kind : "none";

        _snapshot = LightStateSnapshot.Capture(extra, settings);
        _snapshot.Apply(settings);
        _dark = true;

        DarkenButton.Refresh(true);
        Plugin.Log.LogInfo(
            $"[Apply] 消灯しました ({reason}, UI={kind}). " +
            $"Light={_snapshot.LightCount}, Emission={_snapshot.MaterialCount}");
    }

    /// <summary>
    /// 復元。破棄済みオブジェクトが混じっていても最後まで進む。
    /// H 終了 / マップ遷移 / シーンアンロード / ボタン再押下のすべてから呼ぶ。
    /// </summary>
    public static void Restore(string reason)
    {
        if (_snapshot == null)
        {
            _dark = false;
            DarkenButton.Refresh(false);
            return;
        }

        try
        {
            _snapshot.Restore();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Restore] 復元中に例外: " + e);
        }
        finally
        {
            _snapshot = null;
            _dark = false;
            DarkenButton.Refresh(false);
        }

        Plugin.Log.LogInfo($"[Restore] 元のライト状態へ戻しました ({reason}).");
    }

    // ------------------------------------------------------------------ hooks

    /// <summary>TouchController.Setup から呼ばれる (演出 UI が出た直後)。次の Tick で拾う。</summary>
    public static void OnTouchUiReady(TouchController instance)
    {
        Plugin.Log.LogDebug("[UI] TouchController.Setup を検知しました: " + instance.name);
    }

    /// <summary>H.HScene.Initialize の Prefix から呼ばれる。H 開始前に必ず復元する。</summary>
    public static void OnHStarting()
    {
        Restore("H.HScene.Initialize");
        DarkenButton.Destroy();
        _anchor = null;
    }

    /// <summary>Manager.Environment.ChangeTimeZone の Postfix から呼ばれる。</summary>
    public static void OnTimeZoneChanged()
    {
        _nextNightCheck = 0f;

        if (_dark && !NightDetector.IsNight(_hActive, out _))
            Restore("timezone changed to day");
    }

    /// <summary>将来 UI スライダーを付けるときの入口。消灯中なら新しい値で即時再適用する。</summary>
    public static void ApplyLiveSettings()
    {
        if (!_dark || _snapshot == null)
            return;

        _snapshot.Apply(DarkenerSettings.FromConfig());
    }

    public static void Shutdown()
    {
        Restore("shutdown");
        DarkenButton.Destroy();
    }

    // ------------------------------------------------------------------ anchors

    private static UiAnchor? ResolveAnchor(bool hActive)
    {
        return hActive ? ResolveHAnchor() : ResolveTouchAnchor();
    }

    /// <summary>H シーンの左メニュー (H.HSprite)。準備完了まで null を返して再試行させる。</summary>
    private static UiAnchor? ResolveHAnchor()
    {
        try
        {
            var scene = H.HScene.Instance;
            if (scene == null || !scene.Ready)
                return null;

            var sprite = scene.Sprite;
            if (sprite == null)
                return null;

            // 左メニューが HSprite の子ではなく Canvas 配下にある場合があるため、
            // 探索の起点は Canvas を優先する (ボタンの親は実際のボタン位置から決まる)。
            var root = sprite.Canvas != null ? sprite.Canvas.gameObject : sprite.gameObject;

            if (!_panelPathsLogged)
            {
                _panelPathsLogged = true;
                Plugin.Log.LogInfo("[UI] パネル: " + DescribePanels(sprite, root.transform));
            }

            var extra = new List<Light>();
            var panel = sprite.LightPanel;
            if (panel != null)
            {
                if (panel._light != null)
                    extra.Add(panel._light);

                var sub = panel._subLights;
                if (sub != null)
                {
                    for (int i = 0; i < sub.Length; i++)
                    {
                        if (sub[i] != null)
                            extra.Add(sub[i]);
                    }
                }
            }

            return new UiAnchor
            {
                Root = root,
                PreferredButton = null,
                ExtraLights = extra.ToArray(),
                Kind = "H.HSprite",
            };
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[UI] H.HSprite を取得できません: " + e.Message);
            return null;
        }
    }

    /// <summary>探索の演出 UI (TouchController)。電球ボタンのフィールドを持っている。</summary>
    private static UiAnchor? ResolveTouchAnchor()
    {
        try
        {
            var touch = TouchController.Instance;
            if (touch == null || !touch.isActiveAndEnabled)
                return null;

            return new UiAnchor
            {
                Root = touch.gameObject,
                PreferredButton = touch._buttonLight,
                ExtraLights = Array.Empty<Light>(),
                Kind = "TouchController",
            };
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ helpers

    private static string DescribePanels(H.HSprite sprite, Transform root)
    {
        var sb = new StringBuilder();

        void Add(string label, Component? component)
        {
            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(label).Append('=')
              .Append(component != null ? GetPath(component.transform, root) : "null");
        }

        Add("light", sprite.LightPanel);
        Add("cloth", sprite._clothPanel);
        Add("acs", sprite._acsPanel);
        Add("coorde", sprite._coordePanel);
        Add("move", sprite._freeMovePanel);
        Add("item", sprite._useItemPanel);
        return sb.ToString();
    }

    private static string GetPath(Transform target, Transform root)
    {
        var stack = new List<string>();
        var current = target;

        while (current != null)
        {
            stack.Add(current.name);

            if (current == root)
                break;

            current = current.parent;
        }

        stack.Reverse();
        return string.Join("/", stack);
    }

    private static string DescribeTarget(bool h)    {
        try
        {
            if (h)
            {
                var scene = H.HScene.Instance;
                if (scene == null)
                    return "H active, HScene.Instance=null";

                var sprite = scene.Sprite;
                return $"H active, Ready={scene.Ready}, Sprite={(sprite != null)}, " +
                       $"Canvas={(sprite != null && sprite.Canvas != null)}";
            }

            return $"not H, TouchController={(TouchController.Instance != null)}";
        }
        catch (Exception e)
        {
            return "describe failed: " + e.Message;
        }
    }

    private static bool IsHActive()
    {
        try
        {
            return H.HScene.IsActive();
        }
        catch
        {
            return false;
        }
    }

    private static int GetMapId()
    {
        try
        {
            return Manager.Environment.CurrentID;
        }
        catch
        {
            return int.MinValue;
        }
    }

    private static string GetSceneName()
    {
        try
        {
            return SceneManager.GetActiveScene().name;
        }
        catch
        {
            return string.Empty;
        }
    }
}

/// <summary>KeyCode を Input.GetKeyDown で判定する小さな拡張。</summary>
internal static class KeyCodeExtensions
{
    public static bool IsPressedKey(this KeyCode key)
    {
        try
        {
            return key != KeyCode.None && Input.GetKeyDown(key);
        }
        catch
        {
            return false;
        }
    }
}
