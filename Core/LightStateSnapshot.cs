using System;
using System.Collections.Generic;
using AC.Scene.Touch;
using Beautify.Universal;
using H;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AC_RoomDarkener.Core;

/// <summary>
/// 変更前の Light / RenderSettings / Emission マテリアルを保存し、復元する。
/// すべての値は「消灯前の 1 回だけ」記録し、再適用時は記録値から計算し直す (累積しない)。
/// </summary>
internal sealed class LightStateSnapshot
{
    private sealed class LightEntry
    {
        public Light Light = null!;
        public Color Color;
        public float Intensity;
        public float ShadowStrength;
        public bool Enabled;
        public bool ForceOff;
    }

    private sealed class ObjectEntry
    {
        public GameObject Go = null!;
        public bool Active;
    }

    private sealed class MaterialEntry
    {
        public Material Material = null!;
        public Color EmissionColor;
        public bool EmissionKeyword;
    }

    private const string EmissionProperty = "_EmissionColor";
    private const string EmissionKeyword = "_EMISSION";

    private readonly List<LightEntry> _lights = new();
    private readonly List<MaterialEntry> _materials = new();
    private readonly List<ObjectEntry> _namedObjects = new();
    private readonly HashSet<int> _lightIds = new();
    private readonly HashSet<int> _materialIds = new();
    private readonly HashSet<int> _namedObjectIds = new();

    private AmbientMode _ambientMode;
    private Color _ambientLight;
    private Color _ambientSkyColor;
    private Color _ambientEquatorColor;
    private Color _ambientGroundColor;
    private float _ambientIntensity;
    private float _reflectionIntensity;

    private GameObject? _fillObject;
    private Light? _fillLight;
    private Transform? _root;
    private string _sceneName = string.Empty;

    private Beautify.Universal.Beautify? _beautify;
    private float _beautifyBrightness;
    private bool _beautifyBrightnessOverride;
    private float _beautifyContrast;
    private bool _beautifyContrastOverride;
    private Color _beautifyTint;
    private bool _beautifyTintOverride;

    private GameObject? _overlayObject;

    public int LightCount => _lights.Count;

    public int MaterialCount => _materials.Count;

    private LightStateSnapshot()
    {
    }

    // ------------------------------------------------------------------ capture

    public static LightStateSnapshot Capture(Light[] extraLights, DarkenerSettings settings)
    {
        var snapshot = new LightStateSnapshot();
        var roots = CollectRoots(settings.ScanWholeScene);

        foreach (var root in roots)
        {
            snapshot.CaptureLights(root);
            if (settings.HandleEmission)
                snapshot.CaptureMaterials(root, settings.MaxEmissionMaterials);
        }

        // H シーンの照明パネルが持つライト (環境ルート外のことがある)
        snapshot.CaptureLightArray(extraLights);

        // BuildingLight / PointLight の名前で全シーンから探してオフにする
        snapshot.CaptureNamedLights(settings);

        snapshot.CaptureAmbient();
        snapshot._root = roots.Count > 0 ? roots[0] : null;

        if (roots.Count == 0)
            Plugin.Log.LogWarning("[Snapshot] ライトのルートが見つかりません。ScanWholeScene=true を試してください。");
        else
            Plugin.Log.LogInfo("[Snapshot] root=" + GetPath(roots[0], null));

        if (snapshot._lights.Count == 0)
        {
            Plugin.Log.LogWarning("[Snapshot] Light が 0 本です。ScanWholeScene=true を試してください。");
        }
        else
        {
            var names = new System.Text.StringBuilder();
            int limit = Math.Min(snapshot._lights.Count, 12);

            for (int i = 0; i < limit; i++)
                names.Append(i == 0 ? string.Empty : ", ").Append(GetPath(snapshot._lights[i].Light.transform, null));

            Plugin.Log.LogInfo($"[Snapshot] Light {snapshot._lights.Count} 本: {names}");
        }

        try
        {
            snapshot._sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
        catch
        {
            snapshot._sceneName = string.Empty;
        }

        Plugin.Log.LogInfo(
            $"[Snapshot] 保存しました: Light={snapshot._lights.Count}, Emission={snapshot._materials.Count}, " +
            $"NamedObjects={snapshot._namedObjects.Count}, Root={roots.Count}");
        return snapshot;
    }

    private static string GetPath(Transform? target, Transform? root)
    {
        if (target == null)
            return "(null)";

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

    private static List<Transform> CollectRoots(bool wholeScene)
    {
        var roots = new List<Transform>();
        var seen = new HashSet<int>();

        void Add(Transform? t)
        {
            if (t == null)
                return;

            int id;
            try
            {
                id = t.GetInstanceID();
            }
            catch
            {
                return;
            }

            if (seen.Add(id))
                roots.Add(t);
        }

        try
        {
            Add(Manager.Environment.Root);
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Snapshot] Manager.Environment.Root を取得できません: " + e.Message);
        }

        // 追加指定のルート (Hierarchy パス)
        try
        {
            var extraPath = DarkenerConfig.ExtraLightRootPath.Value;
            if (!string.IsNullOrWhiteSpace(extraPath))
            {
                var go = GameObject.Find(extraPath);
                if (go != null)
                {
                    Add(go.transform);
                    Plugin.Log.LogInfo("[Snapshot] ExtraLightRootPath=" + extraPath);
                }
                else
                {
                    Plugin.Log.LogWarning("[Snapshot] ExtraLightRootPath が見つかりません: " + extraPath);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Snapshot] ExtraLightRootPath 解決失敗: " + e.Message);
        }

        // H シーン直下 (環境ルートに含まれないライトの保険)
        try
        {
            if (H.HScene.IsActive())
            {
                var hScene = H.HScene.Instance;
                if (hScene != null)
                    Add(hScene.transform);
            }
        }
        catch
        {
            // ignore
        }

        // マップごとの時間帯ライト (TimeZoneOfLightH) も対象にする
        try
        {
            var touch = AC.Scene.Touch.TouchController.Instance;
            var table = touch != null ? touch._timeZoneOfLight : null;
            if (table != null)
            {
                var e = table.GetEnumerator();
                while (e.MoveNext())
                {
                    var info = e.Current.Value;
                    if (info == null)
                        continue;

                    var objects = info.GetRootTimeZones();
                    if (objects == null)
                        continue;

                    for (int i = 0; i < objects.Length; i++)
                    {
                        var go = objects[i];
                        if (go != null)
                            Add(go.transform);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug("[Snapshot] _timeZoneOfLight を走査できません: " + e.Message);
        }

        if (wholeScene)
        {
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go != null)
                        Add(go.transform);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[Snapshot] シーン全体の走査に失敗: " + e.Message);
            }
        }

        return roots;
    }

    private void CaptureLights(Transform root)
    {
        Light[] lights;
        try
        {
            lights = root.GetComponentsInChildren<Light>(true);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Snapshot] Light 取得に失敗 ({root.name}): {e.Message}");
            return;
        }

        foreach (var light in lights)
        {
            if (light == null)
                continue;

            if (!_lightIds.Add(light.GetInstanceID()))
                continue;

            _lights.Add(new LightEntry
            {
                Light = light,
                Color = light.color,
                Intensity = light.intensity,
                ShadowStrength = light.shadowStrength,
            });
        }
    }

    private void CaptureLightArray(Light[] lights)
    {
        if (lights == null)
            return;

        foreach (var light in lights)
        {
            if (light == null)
                continue;

            if (!_lightIds.Add(light.GetInstanceID()))
                continue;

            _lights.Add(new LightEntry
            {
                Light = light,
                Color = light.color,
                Intensity = light.intensity,
                ShadowStrength = light.shadowStrength,
            });
        }
    }

    private void CaptureNamedLights(DarkenerSettings settings)
    {
        if (!settings.DisableBuildingLights && !settings.DisablePointLights)
            return;

        try
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int lightCount = 0;

            foreach (var root in roots)
            {
                if (root == null)
                    continue;

                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light == null || !IsNamedLight(light.name, settings))
                        continue;

                    if (!_lightIds.Add(light.GetInstanceID()))
                        continue;

                    _lights.Add(new LightEntry
                    {
                        Light = light,
                        Color = light.color,
                        Intensity = light.intensity,
                ShadowStrength = light.shadowStrength,
                        Enabled = light.enabled,
                        ForceOff = true,
                    });

                    lightCount++;
                }
            }

            if (settings.DisableNamedLightObjects)
            {
                foreach (var root in roots)
                {
                    if (root == null)
                        continue;

                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (transform == null || !IsNamedLight(transform.name, settings))
                            continue;

                        int id = transform.gameObject.GetInstanceID();
                        if (!_namedObjectIds.Add(id))
                            continue;

                        _namedObjects.Add(new ObjectEntry
                        {
                            Go = transform.gameObject,
                            Active = transform.gameObject.activeSelf,
                        });
                    }
                }
            }

            Plugin.Log.LogInfo(
                $"[Snapshot] Building/Point Light: {lightCount} 本, 対象 GameObject: {_namedObjects.Count}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[Snapshot] Building/Point Light の走査に失敗: " + e.Message);
        }
    }

    private static bool IsNamedLight(string name, DarkenerSettings settings)
    {
        string normalized = Normalize(name);

        return (settings.DisableBuildingLights && normalized.Contains("buildinglight")) ||
               (settings.DisablePointLights && normalized.Contains("pointlight"));
    }

    private static string Normalize(string name)
        => (name ?? string.Empty).Replace(" ", string.Empty)
                                  .Replace("_", string.Empty)
                                  .Replace("-", string.Empty)
                                  .ToLowerInvariant();

    private void CaptureMaterials(Transform root, int maxMaterials)
    {
        Renderer[] renderers;
        try
        {
            renderers = root.GetComponentsInChildren<Renderer>(true);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Snapshot] Renderer 取得に失敗 ({root.name}): {e.Message}");
            return;
        }

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (_materials.Count >= maxMaterials)
                break;

            Material[] materials;
            try
            {
                materials = renderer.sharedMaterials;
            }
            catch
            {
                continue;
            }

            if (materials == null)
                continue;

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null || !_materialIds.Add(material.GetInstanceID()))
                    continue;

                if (!material.HasProperty(EmissionProperty))
                    continue;

                _materials.Add(new MaterialEntry
                {
                    Material = material,
                    EmissionColor = material.GetColor(EmissionProperty),
                    EmissionKeyword = material.IsKeywordEnabled(EmissionKeyword),
                });

                if (_materials.Count >= maxMaterials)
                    break;
            }
        }
    }

    private void CaptureAmbient()
    {
        _ambientMode = RenderSettings.ambientMode;
        _ambientLight = RenderSettings.ambientLight;
        _ambientSkyColor = RenderSettings.ambientSkyColor;
        _ambientEquatorColor = RenderSettings.ambientEquatorColor;
        _ambientGroundColor = RenderSettings.ambientGroundColor;
        _ambientIntensity = RenderSettings.ambientIntensity;
        _reflectionIntensity = RenderSettings.reflectionIntensity;
    }

    // ------------------------------------------------------------------ apply

    public void Apply(DarkenerSettings settings)
    {
        try
        {
            ApplyLights(settings);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Apply] Light の変更に失敗: " + e);
        }

        try
        {
            ApplyAmbient(settings);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Apply] RenderSettings の変更に失敗: " + e);
        }

        try
        {
            if (settings.HandleEmission)
                ApplyMaterials(settings);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Apply] Emission の変更に失敗: " + e);
        }

        try
        {
            ApplyFillLight(settings);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Apply] 月明かりの生成に失敗: " + e);
        }

        try
        {
            ApplyNamedObjects(settings);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Apply] Building/Point Light の停止に失敗: " + e);
        }

        try
        {
            ApplyScreen(settings);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[Apply] 画面の露出/ガンマ変更に失敗: " + e);
        }
    }

    private void ApplyLights(DarkenerSettings settings)
    {
        int changed = 0;

        foreach (var entry in _lights)
        {
            var light = entry.Light;
            if (light == null)
                continue;

            if (entry.ForceOff)
            {
                light.enabled = false;
                changed++;
                continue;
            }

            bool warm = light.type != LightType.Directional &&
                        entry.Color.r - entry.Color.b > settings.WarmColorThreshold;

            float factor = settings.Brightness;
            if (settings.KeepWarmAccentLights && warm)
                factor = Mathf.Max(factor, settings.WarmLightKeep);

            light.intensity = Mathf.Max(0f, entry.Intensity * factor);
            light.shadowStrength = entry.ShadowStrength * Mathf.Clamp01(settings.CharacterShadow / 100f);

            if (!warm && settings.LightColorMix > 0f)
                light.color = Color.Lerp(entry.Color, settings.NightColor, settings.LightColorMix);

            changed++;

            if (settings.VerboseLog)
            {
                Plugin.Log.LogDebug(
                    $"[Apply] Light {light.name}: {entry.Intensity:0.00} -> {light.intensity:0.00} " +
                    $"(warm={warm})");
            }
        }

        Plugin.Log.LogInfo($"[Apply] Light を {changed} 本変更しました。");
    }

    private static void ApplyAmbient(DarkenerSettings settings)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = settings.AmbientColor;
        RenderSettings.ambientIntensity = Mathf.Max(settings.MinAmbientIntensity, settings.AmbientIntensity);
        RenderSettings.reflectionIntensity = settings.ReflectionIntensity;
    }

    private void ApplyMaterials(DarkenerSettings settings)
    {
        int changed = 0;

        foreach (var entry in _materials)
        {
            var material = entry.Material;
            if (material == null)
                continue;

            var color = entry.EmissionColor;
            material.SetColor(EmissionProperty, new Color(
                color.r * settings.EmissionScale,
                color.g * settings.EmissionScale,
                color.b * settings.EmissionScale,
                color.a));

            material.EnableKeyword(EmissionKeyword);
            changed++;
        }

        Plugin.Log.LogInfo($"[Apply] Emission マテリアルを {changed} 件変更しました。");
    }

    private void ApplyFillLight(DarkenerSettings settings)
    {
        if (_fillObject == null)
        {
            _fillObject = new GameObject("AC_RoomDarkener_MoonFill");

            var light = _fillObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.cullingMask = ~0;

            _fillLight = light;
        }

        if (_fillObject == null || _fillLight == null)
            return;

        try
        {
            if (_root != null)
                _fillObject.transform.SetParent(_root, false);
        }
        catch
        {
            // 親が破棄済みならシーンルートのままにする
        }

        _fillObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        _fillLight.color = settings.NightColor;

        float ambient = settings.AmbientIntensity * ColorTemperature.Luminance(settings.AmbientColor);
        float moon = settings.MoonLightIntensity * ColorTemperature.Luminance(settings.NightColor);
        float total = ambient + moon;

        float intensity = settings.MoonLightIntensity;
        if (total < settings.MinLuminance)
        {
            float moonLum = Mathf.Max(ColorTemperature.Luminance(settings.NightColor), 0.05f);
            intensity += (settings.MinLuminance - total) / moonLum;
        }

        _fillLight.intensity = Mathf.Max(0f, intensity);

        Plugin.Log.LogInfo(
            $"[Apply] 月明かりを追加: intensity={_fillLight.intensity:0.00}, " +
            $"ambientLum={ambient:0.00}, min={settings.MinLuminance:0.00}");
    }

    // ------------------------------------------------------------------ screen

    private void ApplyNamedObjects(DarkenerSettings settings)
    {
        if (!settings.DisableNamedLightObjects)
            return;

        int count = 0;

        foreach (var entry in _namedObjects)
        {
            var go = entry.Go;
            if (go == null || !go.activeSelf)
                continue;

            go.SetActive(false);
            count++;
        }

        Plugin.Log.LogInfo($"[Apply] Building/Point GameObject を {count} 件非アクティブにしました。");
    }

    private void ApplyScreen(DarkenerSettings settings)
    {
        if (settings.UsePostProcessing)
            ApplyBeautify(settings);

        if (settings.UseScreenOverlay)
            ApplyOverlay(settings);
    }

    /// <summary>
    /// ゲーム標準の Beautify (URP VolumeComponent) の brightness / contrast / tint を下げる。
    /// ライトマップ焼き込みのマップでも確実に画面を暗くできる。
    /// </summary>
    private void ApplyBeautify(DarkenerSettings settings)
    {
        var beautify = BeautifySettings.settings;
        if (beautify == null)
        {
            Plugin.Log.LogWarning("[Apply] BeautifySettings.settings が null のため露出を変更できません。");
            return;
        }

        if (_beautify == null)
        {
            _beautify = beautify;
            _beautifyBrightness = beautify.brightness.value;
            _beautifyBrightnessOverride = beautify.brightness.overrideState;
            _beautifyContrast = beautify.contrast.value;
            _beautifyContrastOverride = beautify.contrast.overrideState;
            _beautifyTint = beautify.tintColor.value;
            _beautifyTintOverride = beautify.tintColor.overrideState;
        }

        float exposure = Mathf.Clamp01(settings.ScreenExposure / 100f);
        float gamma = Mathf.Clamp01(settings.ScreenGamma / 100f);

        beautify.brightness.Override(Mathf.Lerp(_beautifyBrightness, 0.05f, exposure));
        beautify.contrast.Override(Mathf.Lerp(_beautifyContrast, _beautifyContrast + 0.6f, gamma));
        beautify.tintColor.Override(Color.Lerp(_beautifyTint, settings.OverlayColor, 0.5f * exposure));

        Plugin.Log.LogInfo(
            $"[Apply] Beautify: brightness={beautify.brightness.value:0.00}, " +
            $"contrast={beautify.contrast.value:0.00}, exposure={settings.ScreenExposure:0}%");
    }

    /// <summary>3D 画面だけを暗くする保険。UI より下の sortingOrder で描画する。</summary>
    private void ApplyOverlay(DarkenerSettings settings)
    {
        float exposure = Mathf.Clamp01(settings.ScreenExposure / 100f);
        float alpha = exposure * 0.75f;

        if (alpha <= 0.001f)
            return;

        if (_overlayObject == null)
        {
            _overlayObject = new GameObject("AC_RoomDarkener_Overlay");

            var canvas = _overlayObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -30000;

            var imageObject = new GameObject("Dim");
            imageObject.transform.SetParent(_overlayObject.transform, false);

            var image = imageObject.AddComponent<Image>();
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        var dim = _overlayObject.GetComponentInChildren<Image>(true);
        if (dim != null)
        {
            var color = settings.OverlayColor;
            color.a = alpha;
            dim.color = color;
        }

        Plugin.Log.LogInfo($"[Apply] 暗幕 alpha={alpha:0.00}");
    }

    // ------------------------------------------------------------------ restore

    private bool IsSameScene()
    {
        try
        {
            string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return string.Equals(current, _sceneName, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public void Restore()
    {
        foreach (var entry in _namedObjects)
        {
            try
            {
                if (entry.Go != null)
                    entry.Go.SetActive(entry.Active);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[Restore] GameObject の復元をスキップ: " + e.Message);
            }
        }

        _namedObjects.Clear();
        _namedObjectIds.Clear();

        foreach (var entry in _lights)
        {
            try
            {
                var light = entry.Light;
                if (light == null)
                    continue;

                light.intensity = entry.Intensity;
                light.color = entry.Color;
                light.shadowStrength = entry.ShadowStrength;

                if (entry.ForceOff)
                    light.enabled = entry.Enabled;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[Restore] Light の復元をスキップ: " + e.Message);
            }
        }

        foreach (var entry in _materials)
        {
            try
            {
                var material = entry.Material;
                if (material == null)
                    continue;

                material.SetColor(EmissionProperty, entry.EmissionColor);
                if (entry.EmissionKeyword)
                    material.EnableKeyword(EmissionKeyword);
                else
                    material.DisableKeyword(EmissionKeyword);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[Restore] Emission の復元をスキップ: " + e.Message);
            }
        }

        try
        {
            if (_beautify != null)
            {
                _beautify.brightness.value = _beautifyBrightness;
                _beautify.brightness.overrideState = _beautifyBrightnessOverride;
                _beautify.contrast.value = _beautifyContrast;
                _beautify.contrast.overrideState = _beautifyContrastOverride;
                _beautify.tintColor.value = _beautifyTint;
                _beautify.tintColor.overrideState = _beautifyTintOverride;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[Restore] Beautify の復元に失敗: " + e.Message);
        }
        finally
        {
            _beautify = null;
        }

        try
        {
            if (_overlayObject != null)
                UnityEngine.Object.Destroy(_overlayObject);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[Restore] 暗幕の破棄に失敗: " + e.Message);
        }
        finally
        {
            _overlayObject = null;
        }

        try
        {
            if (IsSameScene())
            {
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambientLight;
                RenderSettings.ambientSkyColor = _ambientSkyColor;
                RenderSettings.ambientEquatorColor = _ambientEquatorColor;
                RenderSettings.ambientGroundColor = _ambientGroundColor;
                RenderSettings.ambientIntensity = _ambientIntensity;
                RenderSettings.reflectionIntensity = _reflectionIntensity;
            }
            else
            {
                // シーンが変わっている場合、新しいシーンの RenderSettings を上書きしない
                Plugin.Log.LogInfo("[Restore] シーンが変わっているため RenderSettings の復元をスキップします。");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[Restore] RenderSettings の復元に失敗: " + e.Message);
        }

        try
        {
            if (_fillObject != null)
                UnityEngine.Object.Destroy(_fillObject);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[Restore] 月明かりの破棄に失敗: " + e.Message);
        }
        finally
        {
            _fillObject = null;
            _fillLight = null;
        }

        _lights.Clear();
        _materials.Clear();
        _lightIds.Clear();
        _materialIds.Clear();
    }
}
