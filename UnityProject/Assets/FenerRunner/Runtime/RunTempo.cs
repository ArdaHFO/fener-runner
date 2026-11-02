using System;

namespace FenerRunner
{
    // Shared deterministic contract for gameplay speed and spatial course safety.
    // Kept independent of Unity so the exact production formula can be audited.
    public static class RunTempo
    {
        public const float HardCap = 42f;
        public const float MaximumBonus = 8f;
        public static float BaseSpeedAtDistance(double distance)
        {
            distance = Math.Max(0, distance);
            if(distance <= 220) return (float)(18 + 6 * distance / 220);
            if(distance <= 800) return (float)(24 + 6 * (distance - 220) / 580);
            if(distance <= 1800) return (float)(30 + 4 * (distance - 800) / 1000);
            return 34f;
        }
        public static float CeilingAtDistance(double distance)
        { return Math.Min(HardCap, BaseSpeedAtDistance(distance) + MaximumBonus); }
    }
}
