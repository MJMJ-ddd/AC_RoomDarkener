using System;
using Manager;

namespace AC_RoomDarkener.Core;

/// <summary>
/// 現在のマップが夜かどうかを判定する。
/// H 中は FreeH の選択時間帯を最優先し、次に環境の時間帯、最後にセーブデータの Cycle を使う。
/// </summary>
internal static class NightDetector
{
    /// <summary>
    /// 夜なら true。判定できなかった場合は ShowWhenUnknown に従う。
    /// source には判定に使った値が入る (ログ用)。
    /// </summary>
    public static bool IsNight(bool preferFreeH, out string source)
    {
        if (!DarkenerConfig.OnlyAtNight.Value)
        {
            source = "OnlyAtNight=false";
            return true;
        }

        int? env = TryGetEnvironmentTimeZone();
        int? freeH = TryGetFreeHTimeZone();
        int? cycle = TryGetCycleTimeZone();

        source = $"env={Format(env)}, freeH={Format(freeH)}, cycle={Format(cycle)}";

        if (preferFreeH && freeH.HasValue)
        {
            source += " -> freeH";
            return DarkenerConfig.IsNightZone(freeH.Value);
        }

        if (env.HasValue)
        {
            source += " -> env";
            return DarkenerConfig.IsNightZone(env.Value);
        }

        if (freeH.HasValue)
        {
            source += " -> freeH";
            return DarkenerConfig.IsNightZone(freeH.Value);
        }

        if (cycle.HasValue)
        {
            source += " -> cycle";
            return DarkenerConfig.IsNightZone(cycle.Value);
        }

        source += " -> unknown";
        return DarkenerConfig.ShowWhenUnknown.Value;
    }

    private static string Format(int? value) => value.HasValue ? value.Value.ToString() : "?";

    private static int? TryGetEnvironmentTimeZone()
    {
        try
        {
            var entity = Manager.Environment.Entity;
            if (entity == null)
                return null;

            int value = entity.TimeZone;
            return value >= 0 && value <= 5 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetFreeHTimeZone()
    {
        try
        {
            var freeH = AC.Scene.FreeH.FreeHScene.Instance;
            if (freeH == null)
                return null;

            int value = (int)freeH._timeZones;
            return value >= 0 && value <= 5 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetCycleTimeZone()
    {
        try
        {
            var game = Manager.Game.Instance;
            if (game == null)
                return null;

            var save = game.SaveData;
            if (save == null || save.Cycle == null)
                return null;

            int value = save.Cycle.TimeZone;
            return value >= 0 && value <= 5 ? value : null;
        }
        catch
        {
            return null;
        }
    }
}
