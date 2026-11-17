using UnityEngine;

namespace AC_RoomDarkener.Core;

/// <summary>1 回の消灯に使う値のスナップショット。将来 UI スライダーから書き換えるための入口でもある。</summary>
internal sealed class DarkenerSettings
{
    public float Brightness = 0.12f;
    public float ColorTemperatureK = 8000f;
    public Color NightColor = new(0.62f, 0.76f, 1f);
    public Color AmbientColor = new(0.13f, 0.22f, 0.36f);
    public float AmbientIntensity = 0.6f;
    public float MinAmbientIntensity = 0.15f;
    public float MinLuminance = 0.22f;
    public float ReflectionIntensity = 0.3f;

    public bool KeepWarmAccentLights = true;
    public float WarmLightKeep = 0.35f;
    public float WarmColorThreshold = 0.08f;
    public float LightColorMix = 0.25f;

    public bool HandleEmission = true;
    public float EmissionScale = 0.25f;
    public int MaxEmissionMaterials = 4000;

    public bool DisableBuildingLights = true;
    public bool DisablePointLights = true;
    public bool DisableNamedLightObjects = true;

    public float ScreenExposure = 55f;
    public float ScreenGamma;
    public float CharacterShadow = 100f;
    public bool UsePostProcessing = true;
    public bool UseScreenOverlay = true;
    public Color OverlayColor = new(0.02f, 0.03f, 0.06f);

    public float MoonLightIntensity = 0.45f;

    public bool ScanWholeScene;
    public bool VerboseLog;

    public static DarkenerSettings FromConfig()
    {
        var settings = new DarkenerSettings
        {
            Brightness = DarkenerConfig.Brightness.Value,
            ColorTemperatureK = DarkenerConfig.ColorTemperatureK.Value,
            AmbientIntensity = DarkenerConfig.AmbientIntensity.Value,
            MinAmbientIntensity = DarkenerConfig.MinAmbientIntensity.Value,
            MinLuminance = DarkenerConfig.MinLuminance.Value,
            ReflectionIntensity = DarkenerConfig.ReflectionIntensity.Value,
            KeepWarmAccentLights = DarkenerConfig.KeepWarmAccentLights.Value,
            WarmLightKeep = DarkenerConfig.WarmLightKeep.Value,
            WarmColorThreshold = DarkenerConfig.WarmColorThreshold.Value,
            LightColorMix = DarkenerConfig.LightColorMix.Value,
            HandleEmission = DarkenerConfig.HandleEmission.Value,
            EmissionScale = DarkenerConfig.EmissionScale.Value,
            MaxEmissionMaterials = DarkenerConfig.MaxEmissionMaterials.Value,
            MoonLightIntensity = DarkenerConfig.MoonLightIntensity.Value,
            DisableBuildingLights = DarkenerConfig.DisableBuildingLights.Value,
            DisablePointLights = DarkenerConfig.DisablePointLights.Value,
            DisableNamedLightObjects = DarkenerConfig.DisableNamedLightObjects.Value,
            ScreenExposure = DarkenerConfig.ScreenExposure.Value,
            ScreenGamma = DarkenerConfig.ScreenGamma.Value,
            CharacterShadow = DarkenerConfig.CharacterShadow.Value,
            UsePostProcessing = DarkenerConfig.UsePostProcessing.Value,
            UseScreenOverlay = DarkenerConfig.UseScreenOverlay.Value,
            ScanWholeScene = DarkenerConfig.ScanWholeScene.Value,
            VerboseLog = DarkenerConfig.VerboseLog.Value,
        };

        settings.NightColor = ColorTemperature.KelvinToColor(settings.ColorTemperatureK);
        settings.AmbientColor = DarkenerConfig.ParseColor(DarkenerConfig.AmbientColor.Value, settings.AmbientColor);
        settings.OverlayColor = DarkenerConfig.ParseColor(DarkenerConfig.OverlayColor.Value, settings.OverlayColor);
        return settings;
    }
}

/// <summary>色温度 (Kelvin) → RGB の近似変換。Tanner Helland の近似式。</summary>
internal static class ColorTemperature
{
    public static Color KelvinToColor(float kelvin)
    {
        float temp = Mathf.Clamp(kelvin, 1000f, 40000f) / 100f;
        float r, g, b;

        if (temp <= 66f)
        {
            r = 255f;
            g = 99.4708025861f * Mathf.Log(temp) - 161.1195681661f;
        }
        else
        {
            r = 329.698727446f * Mathf.Pow(temp - 60f, -0.1332047592f);
            g = 288.1221695283f * Mathf.Pow(temp - 60f, -0.0755148492f);
        }

        if (temp >= 66f)
        {
            b = 255f;
        }
        else if (temp <= 19f)
        {
            b = 0f;
        }
        else
        {
            b = 138.5177312231f * Mathf.Log(temp - 10f) - 305.0447927307f;
        }

        return new Color(
            Mathf.Clamp01(r / 255f),
            Mathf.Clamp01(g / 255f),
            Mathf.Clamp01(b / 255f));
    }

    /// <summary>知覚輝度 (Rec.709)。最低輝度クランプの計算に使う。</summary>
    public static float Luminance(Color color)
        => 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
}
