using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace AC_RoomDarkener;

/// <summary>BepInEx Config の定義。ここに追加した値は ConfigManager から即時変更できる。</summary>
internal static class DarkenerConfig
{
    public static ConfigEntry<bool> Enabled = null!;
    public static ConfigEntry<bool> OnlyAtNight = null!;
    public static ConfigEntry<string> NightTimeZoneIds = null!;
    public static ConfigEntry<bool> ShowButton = null!;
    public static ConfigEntry<bool> ShowWhenUnknown = null!;
    public static ConfigEntry<KeyCode> Hotkey = null!;
    public static ConfigEntry<bool> HotkeyRequireCtrl = null!;

    public static ConfigEntry<float> Brightness = null!;
    public static ConfigEntry<int> ColorTemperatureK = null!;
    public static ConfigEntry<string> AmbientColor = null!;
    public static ConfigEntry<float> AmbientIntensity = null!;
    public static ConfigEntry<float> MinLuminance = null!;
    public static ConfigEntry<float> MinAmbientIntensity = null!;
    public static ConfigEntry<float> ReflectionIntensity = null!;

    public static ConfigEntry<bool> KeepWarmAccentLights = null!;
    public static ConfigEntry<float> WarmLightKeep = null!;
    public static ConfigEntry<float> WarmColorThreshold = null!;
    public static ConfigEntry<float> LightColorMix = null!;

    public static ConfigEntry<bool> HandleEmission = null!;
    public static ConfigEntry<float> EmissionScale = null!;
    public static ConfigEntry<int> MaxEmissionMaterials = null!;

    public static ConfigEntry<bool> DisableBuildingLights = null!;
    public static ConfigEntry<bool> DisablePointLights = null!;
    public static ConfigEntry<bool> DisableNamedLightObjects = null!;

    public static ConfigEntry<float> ScreenExposure = null!;
    public static ConfigEntry<float> ScreenGamma = null!;
    public static ConfigEntry<float> CharacterShadow = null!;
    public static ConfigEntry<bool> UsePostProcessing = null!;
    public static ConfigEntry<bool> UseScreenOverlay = null!;
    public static ConfigEntry<string> OverlayColor = null!;

    public static ConfigEntry<float> MoonLightIntensity = null!;

    public static ConfigEntry<bool> ScanWholeScene = null!;
    public static ConfigEntry<string> ExtraLightRootPath = null!;
    public static ConfigEntry<bool> VerboseLog = null!;
    public static ConfigEntry<bool> TintIconByState = null!;
    public static ConfigEntry<string> OffIconTint = null!;
    public static ConfigEntry<string> SourceButtonPath = null!;
    public static ConfigEntry<bool> DumpButtonHierarchy = null!;

    private static readonly HashSet<int> _nightZoneIds = new() { 4 };

    public static void Initialize(ConfigFile cfg)
    {
        Enabled = cfg.Bind("General", "Enabled", true,
            "プラグイン全体の有効/無効。");

        OnlyAtNight = cfg.Bind("General", "OnlyAtNight", true,
            "true のとき、夜と判定したマップでのみボタンを表示する。");

        NightTimeZoneIds = cfg.Bind("General", "NightTimeZoneIds", "4",
            "夜とみなす時間帯 ID (カンマ区切り)。AC.User.TimeZones: 0=早朝,1=朝,2=昼,3=夕方,4=夜,5=帰宅。");

        ShowButton = cfg.Bind("General", "ShowButton", true,
            "左メニューに電球ボタンを追加する。false でもホットキーで切替できる。");

        ShowWhenUnknown = cfg.Bind("General", "ShowWhenUnknown", false,
            "時間帯を取得できない場合でもボタンを表示する (デバッグ用)。");

        Hotkey = cfg.Bind("General", "Hotkey", KeyCode.L,
            "明かり ON/OFF を切り替えるキー。");

        HotkeyRequireCtrl = cfg.Bind("General", "HotkeyRequireCtrl", true,
            "ホットキーに Ctrl 同時押しを要求する。");

        Brightness = cfg.Bind("Darkness", "Brightness", 0.12f,
            new ConfigDescription("消灯時のライト強度倍率 (0=消灯, 1=変更なし)。", new AcceptableValueRange<float>(0f, 1f)));

        ColorTemperatureK = cfg.Bind("Darkness", "ColorTemperatureK", 8000,
            new ConfigDescription("月明かりの色温度 (K)。低い=暖色, 高い=寒色。", new AcceptableValueRange<int>(2000, 12000)));

        AmbientColor = cfg.Bind("Darkness", "AmbientColor", "#22385C",
            "消灯時の環境光の色 (#RRGGBB)。青みのある夜の部屋にするための基準色。");

        AmbientIntensity = cfg.Bind("Darkness", "AmbientIntensity", 0.6f,
            new ConfigDescription("消灯時の環境光の強さ (RenderSettings.ambientIntensity)。", new AcceptableValueRange<float>(0f, 2f)));

        MinAmbientIntensity = cfg.Bind("Darkness", "MinAmbientIntensity", 0.15f,
            new ConfigDescription("環境光の下限。0 にすると完全な暗闇になり得るため、必ず正の値を推奨。", new AcceptableValueRange<float>(0f, 1f)));

        MinLuminance = cfg.Bind("Darkness", "MinLuminance", 0.22f,
            new ConfigDescription("人物が視認できる最低輝度。環境光+月明かりの合成輝度がこれを下回らないよう補正する。", new AcceptableValueRange<float>(0f, 1.5f)));

        ReflectionIntensity = cfg.Bind("Darkness", "ReflectionIntensity", 0.3f,
            new ConfigDescription("消灯時の ReflectionProbe 強度 (RenderSettings.reflectionIntensity)。", new AcceptableValueRange<float>(0f, 1f)));

        KeepWarmAccentLights = cfg.Bind("Darkness", "KeepWarmAccentLights", true,
            "スタンドライト等の暖色系の小物ライトを弱く残す。");

        WarmLightKeep = cfg.Bind("Darkness", "WarmLightKeep", 0.35f,
            new ConfigDescription("暖色ライトを残す割合 (0=消灯, 1=そのまま)。", new AcceptableValueRange<float>(0f, 1f)));

        WarmColorThreshold = cfg.Bind("Darkness", "WarmColorThreshold", 0.08f,
            new ConfigDescription("暖色と判定する R-B のしきい値。", new AcceptableValueRange<float>(0f, 1f)));

        LightColorMix = cfg.Bind("Darkness", "LightColorMix", 0.25f,
            new ConfigDescription("消灯時に通常ライトの色を月明かり色へ寄せる割合。", new AcceptableValueRange<float>(0f, 1f)));

        HandleEmission = cfg.Bind("Darkness", "HandleEmission", true,
            "Emission マテリアル (_EmissionColor) も暗くする。");

        EmissionScale = cfg.Bind("Darkness", "EmissionScale", 0.25f,
            new ConfigDescription("Emission の倍率。", new AcceptableValueRange<float>(0f, 1f)));

        MaxEmissionMaterials = cfg.Bind("Darkness", "MaxEmissionMaterials", 4000,
            new ConfigDescription("Emission を処理するマテリアルの上限。重い場合は下げる。", new AcceptableValueRange<int>(0, 20000)));

        DisableBuildingLights = cfg.Bind("Lights", "DisableBuildingLights", true,
            "名前に BuildingLight を含むライトを消灯時にオフにする (MyRoom など)。");

        DisablePointLights = cfg.Bind("Lights", "DisablePointLights", true,
            "名前に PointLight / Point Light を含むライトを消灯時にオフにする (Hotel など)。");

        DisableNamedLightObjects = cfg.Bind("Lights", "DisableNamedLightObjects", true,
            "上記に該当する GameObject 自体も非アクティブにする (子ライトごと消える)。");

        ScreenExposure = cfg.Bind("Screen", "ScreenExposure", 55f,
            new ConfigDescription("画面の露出を下げる強さ (0=変更なし, 100=最大)。",
                new AcceptableValueRange<float>(0f, 100f)));

        ScreenGamma = cfg.Bind("Screen", "ScreenGamma", 0f,
            new ConfigDescription("画面のガンマを下げる強さ (0=変更なし, 100=最大)。",
                new AcceptableValueRange<float>(0f, 100f)));

        CharacterShadow = cfg.Bind("Screen", "CharacterShadow", 100f,
            new ConfigDescription("キャラクターの影の濃さ (0=影なし, 100=元のまま)。リアルタイムシャドウのみ対象。",
                new AcceptableValueRange<float>(0f, 100f)));

        UsePostProcessing = cfg.Bind("Screen", "UsePostProcessing", true,
            "URP Volume の ColorAdjustments / LiftGammaGain で露出・ガンマを下げる。");

        UseScreenOverlay = cfg.Bind("Screen", "UseScreenOverlay", false,
            "3D 画面の上に暗幕を重ねる (UI も暗くなる場合がある)。ポストプロセスが効かない環境向け。");

        OverlayColor = cfg.Bind("Screen", "OverlayColor", "#05080F",
            "暗幕の色 (#RRGGBB)。");

        MoonLightIntensity = cfg.Bind("Darkness", "MoonLightIntensity", 0.45f,
            new ConfigDescription("追加する月明かり Directional Light の強さ。", new AcceptableValueRange<float>(0f, 2f)));

        ScanWholeScene = cfg.Bind("Advanced", "ScanWholeScene", false,
            "true にするとシーン全体の Light/Renderer を対象にする (重い/UI ライトも含む)。通常は false。");

        ExtraLightRootPath = cfg.Bind("Advanced", "ExtraLightRootPath", "",
            "追加で走査するライトのルート (Hierarchy のフルパス)。例: 2_Classroom/LightRoot");

        VerboseLog = cfg.Bind("Advanced", "VerboseLog", false,
            "ライト 1 本ごとの詳細ログを出す。");

        TintIconByState = cfg.Bind("UI", "TintIconByState", true,
            "消灯中はボタンのアイコンを青系に色付けする。");

        OffIconTint = cfg.Bind("UI", "OffIconTint", "#5A7BB0",
            "消灯中のアイコン色 (#RRGGBB)。");

        SourceButtonPath = cfg.Bind("UI", "SourceButtonPath", "",
            "複製元ボタンのフルパス (UI ルートからの相対パス)。空なら電球ボタンを名前から自動探索する。");

        DumpButtonHierarchy = cfg.Bind("Advanced", "DumpButtonHierarchy", true,
            "自動探索に失敗したとき、UI 配下のボタン一覧をログに 1 回だけ出す。");

        RefreshNightZones();

        NightTimeZoneIds.SettingChanged += (_, _) => RefreshNightZones();
        HookLiveApply();
    }

    /// <summary>ConfigManager のスライダーを動かした瞬間に、消灯中なら再適用する。</summary>
    private static void HookLiveApply()
    {
        void Hook<T>(ConfigEntry<T> entry)
            => entry.SettingChanged += (_, _) => Core.RoomDarkness.ApplyLiveSettings();

        Hook(Brightness);
        Hook(ColorTemperatureK);
        Hook(AmbientColor);
        Hook(AmbientIntensity);
        Hook(MinAmbientIntensity);
        Hook(MinLuminance);
        Hook(ReflectionIntensity);
        Hook(KeepWarmAccentLights);
        Hook(WarmLightKeep);
        Hook(WarmColorThreshold);
        Hook(LightColorMix);
        Hook(HandleEmission);
        Hook(EmissionScale);
        Hook(MoonLightIntensity);
        Hook(DisableBuildingLights);
        Hook(DisablePointLights);
        Hook(DisableNamedLightObjects);
        Hook(ScreenExposure);
        Hook(ScreenGamma);
        Hook(CharacterShadow);
        Hook(UsePostProcessing);
        Hook(UseScreenOverlay);
        Hook(OverlayColor);
    }

    public static bool IsNightZone(int timeZone)
        => _nightZoneIds.Contains(timeZone);

    public static void RefreshNightZones()
    {
        _nightZoneIds.Clear();

        foreach (var token in (NightTimeZoneIds.Value ?? string.Empty).Split(','))
        {
            if (int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                _nightZoneIds.Add(id);
        }

        if (_nightZoneIds.Count == 0)
            _nightZoneIds.Add(4);
    }

    public static Color ParseColor(string text, Color fallback)
        => ColorUtility.TryParseHtmlString(text, out var color) ? color : fallback;
}
