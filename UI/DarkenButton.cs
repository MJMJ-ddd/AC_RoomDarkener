using System;
using System.Text;
using AC_RoomDarkener.Core;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AC_RoomDarkener.UI;

/// <summary>
/// 左メニューの既存ボタンを複製して、一番下に「明かり ON/OFF」ボタンを追加する。
/// H シーン (H.HSprite) でも探索の演出 UI (TouchController) でも同じ仕組みで動く。
/// </summary>
internal static class DarkenButton
{
    private const string CloneName = "AC_RoomDarkener_LightButton";

    private static GameObject? _clone;
    private static Button? _button;
    private static Image? _icon;
    private static Color _iconOriginal = Color.white;
    private static GameObject? _owner;
    private static int _lastFailedRootId = int.MinValue;
    private static bool _hierarchyDumped;
    private static bool _globalScanDone;

    public static void Ensure(GameObject uiRoot, Button? preferred, bool dark)
    {
        if (_clone != null && _owner == uiRoot)
        {
            Refresh(dark);
            return;
        }

        Destroy();

        var source = preferred != null ? preferred : FindSourceButton(uiRoot);

        // H シーンのメニューがスプライトの外 (兄弟 Canvas) にある場合の保険
        if (source == null && uiRoot.transform.parent != null)
            source = FindSourceButton(uiRoot.transform.parent.gameObject);

        // 最後の保険: シーン全体から電球ボタンを探す (1 回だけ)
        if (source == null && !_globalScanDone)
        {
            _globalScanDone = true;
            source = FindSourceButtonInScene();
        }

        if (source == null)
        {
            int rootId = SafeRootId(uiRoot);
            if (_lastFailedRootId != rootId)
            {
                _lastFailedRootId = rootId;
                Plugin.Log.LogWarning("[Button] 複製元ボタンが見つかりません。SourceButtonPath を設定してください。");
                DumpUiTree(uiRoot);
            }
            return;
        }

        Create(uiRoot, source);
        Refresh(dark);
    }

    private static void Create(GameObject uiRoot, Button source)
    {
        try
        {
            var parent = source.transform.parent;
            if (parent == null)
            {
                Plugin.Log.LogWarning("[Button] 複製元ボタンに親がありません。");
                return;
            }

            var clone = UnityEngine.Object.Instantiate(source.gameObject, parent);
            clone.name = CloneName;
            clone.SetActive(true);

            _clone = clone;
            _owner = uiRoot;

            var button = clone.GetComponent<Button>();
            if (button == null)
            {
                Plugin.Log.LogWarning("[Button] 複製したボタンに Button コンポーネントがありません。");
                Destroy();
                return;
            }

            _button = button;

            // 元のリスナー (照明パネルを開く等) を外し、ON/OFF に差し替える
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(
                DelegateSupport.ConvertDelegate<UnityAction>((Action)(() => RoomDarkness.Toggle("button"))));

            _icon = button.image;
            if (_icon == null)
                _icon = clone.GetComponentInChildren<Image>(true);

            if (_icon != null)
            {
                _iconOriginal = _icon.color;

                // 左メニューに電球アイコンがあれば、そのスプライトを流用する
                var bulb = FindLightSprite(uiRoot);
                if (bulb != null)
                {
                    _icon.sprite = bulb;
                    Plugin.Log.LogInfo("[Button] 電球スプライトを流用しました。");
                }
            }

            ForceVisible(clone);
            LayoutAtBottom(clone, source.transform as RectTransform);

            var rect = clone.GetComponent<RectTransform>();
            Plugin.Log.LogInfo(
                $"[Button] 配置: parent={GetPath(parent, uiRoot.transform)}, " +
                $"sibling={clone.transform.GetSiblingIndex()}/{parent.childCount}, " +
                $"anchored={rect?.anchoredPosition}, size={rect?.sizeDelta}, " +
                $"layoutGroup={(parent.GetComponent<LayoutGroup>() != null)}");

            if (DarkenerConfig.DumpButtonHierarchy.Value && !_hierarchyDumped)
            {
                _hierarchyDumped = true;
                DumpHierarchy(uiRoot);
            }

            if (!clone.activeInHierarchy)
            {
                Plugin.Log.LogWarning(
                    $"[Button] 複製ボタンの親が非アクティブのため表示されません: {GetPath(parent, uiRoot.transform)}");
            }

            Plugin.Log.LogInfo($"[Button] 左メニューに電球ボタンを追加しました (複製元: {GetPath(source.transform, uiRoot.transform)})。");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Button] 生成に失敗: " + e);
            Destroy();
        }
    }

    /// <summary>消灯中はアイコンの色を青系に寄せて状態を示す。</summary>
    public static void Refresh(bool dark)
    {
        try
        {
            if (_icon == null)
                return;

            if (!DarkenerConfig.TintIconByState.Value || !dark)
            {
                _icon.color = _iconOriginal;
                return;
            }

            var tint = DarkenerConfig.ParseColor(DarkenerConfig.OffIconTint.Value, new Color(0.35f, 0.48f, 0.69f));
            tint.a = _iconOriginal.a;
            _icon.color = tint;
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Button] 色の更新をスキップ: " + e.Message);
        }
    }

    public static void Destroy()
    {
        try
        {
            if (_clone != null)
                UnityEngine.Object.Destroy(_clone);
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Button] 破棄をスキップ: " + e.Message);
        }
        finally
        {
            _clone = null;
            _button = null;
            _icon = null;
            _owner = null;
        }
    }

    // ------------------------------------------------------------------ discovery

    /// <summary>
    /// 1) Config のフルパス → 2) 名前に light/照明 → 3) 縦並びボタン群の最下段 の順に探す。
    /// </summary>
    private static Button? FindSourceButton(GameObject uiRoot)
    {
        var path = DarkenerConfig.SourceButtonPath.Value;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var target = uiRoot.transform.Find(path);
                if (target != null)
                {
                    var button = target.GetComponent<Button>();
                    if (button != null)
                        return button;

                    Plugin.Log.LogWarning($"[Button] SourceButtonPath='{path}' に Button がありません。");
                }
                else
                {
                    Plugin.Log.LogWarning($"[Button] SourceButtonPath='{path}' が見つかりません。");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[Button] SourceButtonPath の解決に失敗: " + e.Message);
            }
        }

        Button[] buttons;
        try
        {
            buttons = uiRoot.GetComponentsInChildren<Button>(true);
        }
        catch
        {
            return null;
        }

        // 2) 名前で電球ボタンを探す
        foreach (var button in buttons)
        {
            if (button == null || button.gameObject == _clone)
                continue;

            string name = button.name ?? string.Empty;
            if (name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("denki", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.Contains("照明") || name.Contains("電球"))
            {
                return button;
            }
        }

        // 2.5) Inspector で「照明を開く」処理が紐付いているボタン
        foreach (var button in buttons)
        {
            if (button == null || button.gameObject == _clone || button.onClick == null)
                continue;

            try
            {
                int count = button.onClick.GetPersistentEventCount();
                for (int i = 0; i < count; i++)
                {
                    string method = button.onClick.GetPersistentMethodName(i) ?? string.Empty;
                    if (method.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        method.IndexOf("denki", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return button;
                    }
                }
            }
            catch
            {
                // 取得できないボタンは無視
            }
        }

        // 3) 既知の左メニュー (H シーンの歯車・Hポイント)
        string[] known =
        {
            "SubCategory/btnConfig", "UI/SubCategory/btnConfig",
            "SubCategory/btnHPoint", "UI/SubCategory/btnHPoint",
            "btnEnd", "UI/btnEnd",
        };

        foreach (var knownPath in known)
        {
            try
            {
                var target = uiRoot.transform.Find(knownPath);
                var button = target != null ? target.GetComponent<Button>() : null;

                if (button != null)
                {
                    Plugin.Log.LogInfo($"[Button] 既知パス '{knownPath}' を複製元にします。");
                    return button;
                }
            }
            catch
            {
                // ignore
            }
        }

        // 4) 縦メニュー (VerticalLayoutGroup) の最下段ボタン
        try
        {
            VerticalLayoutGroup? bestGroup = null;
            int bestCount = 0;

            foreach (var group in uiRoot.GetComponentsInChildren<VerticalLayoutGroup>(true))
            {
                if (group == null)
                    continue;

                int count = group.GetComponentsInChildren<Button>(true).Length;
                if (count >= 4 && count > bestCount)
                {
                    bestCount = count;
                    bestGroup = group;
                }
            }

            if (bestGroup != null)
            {
                Button? lowest = null;
                float lowestY = float.MaxValue;

                foreach (var button in bestGroup.GetComponentsInChildren<Button>(true))
                {
                    if (button == null)
                        continue;

                    var rect = button.transform as RectTransform;
                    if (rect == null)
                        continue;

                    float y = rect.position.y;
                    if (y < lowestY)
                    {
                        lowestY = y;
                        lowest = button;
                    }
                }

                if (lowest != null)
                {
                    Plugin.Log.LogInfo($"[Button] 縦メニューの最下段ボタンを複製元にします ({bestCount} 個中)。");
                    return lowest;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Button] 縦メニュー探索をスキップ: " + e.Message);
        }

        return null;
    }

    private static Sprite? FindLightSprite(GameObject uiRoot)
    {
        try
        {
            foreach (var toggle in uiRoot.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle == null)
                    continue;

                string path = GetPath(toggle.transform, uiRoot.transform);
                if (!IsLightName(path))
                    continue;

                var image = toggle.GetComponent<Image>();
                if (image != null && image.sprite != null)
                    return image.sprite;

                var graphic = toggle.graphic;
                if (graphic != null)
                {
                    var graphicImage = graphic.TryCast<Image>();
                    if (graphicImage != null && graphicImage.sprite != null)
                        return graphicImage.sprite;
                }
            }

            foreach (var button in uiRoot.GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                    continue;

                string path = GetPath(button.transform, uiRoot.transform);
                if (!IsLightName(path))
                    continue;

                var image = button.image != null ? button.image : button.GetComponent<Image>();
                if (image != null && image.sprite != null)
                    return image.sprite;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Button] 電球スプライト探索スキップ: " + e.Message);
        }

        return null;
    }

    private static bool IsLightName(string text)
        => text.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0 ||
           text.IndexOf("denki", StringComparison.OrdinalIgnoreCase) >= 0 ||
           text.Contains("照明") || text.Contains("電球");

    private static Button? FindSourceButtonInScene()
    {
        try
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root == null)
                    continue;

                var source = FindSourceButton(root);
                if (source != null)
                {
                    Plugin.Log.LogInfo($"[Button] シーン全体から複製元を発見: {GetPath(source.transform, root.transform)}");
                    return source;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[Button] シーン全体の探索に失敗: " + e.Message);
        }

        return null;
    }

    private static void DumpSceneHierarchy()
    {
        if (!DarkenerConfig.DumpButtonHierarchy.Value)
            return;

        try
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root == null)
                    continue;

                if (root.GetComponentsInChildren<Button>(true).Length >= 4)
                    DumpHierarchy(root);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Button] シーン階層ダンプ失敗: " + e.Message);
        }
    }

    private static int SafeRootId(GameObject root)
    {
        try { return root.GetInstanceID(); }
        catch { return int.MinValue; }
    }

    private static void DumpUiTree(GameObject root)
    {
        try
        {
            var sb = new StringBuilder("[Button] UI 直下:\n");
            int count = root.transform.childCount;

            for (int i = 0; i < count && i < 60; i++)
            {
                var child = root.transform.GetChild(i);
                if (child == null)
                    continue;

                sb.Append("  - ").Append(child.name)
                  .Append(" active=").Append(child.gameObject.activeSelf)
                  .Append(" children=").Append(child.childCount)
                  .Append(" comps=").Append(ComponentNames(child.gameObject))
                  .Append('\n');
            }

            Plugin.Log.LogInfo(sb.ToString());
            DumpToggles(root);
            DumpHierarchy(root);
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Button] UI ツリーダンプ失敗: " + e.Message);
        }
    }

    private static string ComponentNames(GameObject go)
    {
        var sb = new StringBuilder();

        try
        {
            var components = go.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    continue;

                if (sb.Length > 0)
                    sb.Append(',');

                sb.Append(components[i].GetType().Name);
            }
        }
        catch
        {
            // ignore
        }

        return sb.ToString();
    }

    private static void DumpToggles(GameObject root)
    {
        try
        {
            var toggles = root.GetComponentsInChildren<Toggle>(true);
            Plugin.Log.LogInfo($"[Button] Toggle 一覧 ({toggles.Length}):");

            var sb = new StringBuilder();

            for (int i = 0; i < toggles.Length && i < 80; i++)
            {
                var toggle = toggles[i];
                if (toggle == null)
                    continue;

                sb.Append("  - ").Append(GetPath(toggle.transform, root.transform))
                  .Append(" isOn=").Append(toggle.isOn).Append('\n');
            }

            Plugin.Log.LogInfo(sb.ToString());
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Button] Toggle ダンプ失敗: " + e.Message);
        }
    }

    private static void DumpHierarchy(GameObject uiRoot)    {
        if (!DarkenerConfig.DumpButtonHierarchy.Value)
            return;

        try
        {
            var sb = new StringBuilder("[Button] UI 配下のボタン一覧:\n");
            var buttons = uiRoot.GetComponentsInChildren<Button>(true);
            int limit = Mathf.Min(buttons.Length, 80);

            for (int i = 0; i < limit; i++)
            {
                var button = buttons[i];
                if (button == null)
                    continue;

                sb.Append("  - ").Append(GetPath(button.transform, uiRoot.transform)).Append('\n');
            }

            if (buttons.Length > limit)
                sb.Append($"  ... 他 {buttons.Length - limit} 件\n");

            Plugin.Log.LogInfo(sb.ToString());
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Button] 階層ダンプに失敗: " + e.Message);
        }
    }

    private static string GetPath(Transform? target, Transform root)
    {
        if (target == null)
            return "(null)";

        var stack = new System.Collections.Generic.List<string>();
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

    // ------------------------------------------------------------------ layout

    private static void ForceVisible(GameObject clone)
    {
        try
        {
            foreach (var animator in clone.GetComponentsInChildren<Animator>(true))
            {
                if (animator != null)
                    animator.enabled = false;
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            foreach (var group in clone.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (group == null)
                    continue;

                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// レイアウトグループがある場合は最後の子 (最下部) にする。
    /// 手動配置の場合は最も下のボタンの下へずらす。
    /// </summary>
    private static void LayoutAtBottom(GameObject clone, RectTransform? source)
    {
        try
        {
            var parent = clone.transform.parent;
            if (parent == null)
                return;

            // 縦並び / グリッドなら末尾 = 最下部。横並びや手動配置なら座標で下へずらす。
            bool flowsVertically = parent.GetComponent<VerticalLayoutGroup>() != null ||
                                   parent.GetComponent<GridLayoutGroup>() != null;

            if (flowsVertically)
            {
                clone.transform.SetAsLastSibling();
                return;
            }

            var rect = clone.GetComponent<RectTransform>();
            if (rect == null || source == null)
                return;

            float minY = source.anchoredPosition.y;
            float height = source.sizeDelta.y;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null || child.gameObject == clone)
                    continue;

                var childRect = child.GetComponent<RectTransform>();
                if (childRect == null || !child.gameObject.activeSelf)
                    continue;

                minY = Mathf.Min(minY, childRect.anchoredPosition.y);
                height = Mathf.Max(height, childRect.sizeDelta.y);
            }

            var layoutElement = clone.GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.ignoreLayout = true;

            rect.anchoredPosition = new Vector2(source.anchoredPosition.x, minY - height - 8f);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[Button] 位置調整に失敗: " + e.Message);
        }
    }
}
