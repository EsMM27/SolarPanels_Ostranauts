using System;
using System.Collections.Generic;

// Simple AU helpers for mods/patches
public static class AUUtils
{
    private const double SHIP_AU_CACHE_SECONDS = 30.0;

    private sealed class ShipAUCacheEntry
    {
        public double DistAU;
        public double ExpiresAt;
    }

    private static readonly Dictionary<Ship, ShipAUCacheEntry> ShipAUCache =
        new Dictionary<Ship, ShipAUCacheEntry>();

    // Kilometers per astronomical unit used in the codebase (matches BodyOrbit usage)
    public const double AU_KM = 149597872.0;

    // Meters per astronomical unit
    public const double AU_M = AU_KM * 1000.0;

    // Legacy game constant seen in BodyOrbit.CreateBOFromShip (kept for reference)
    public const double LEGACY_AU_METERS = 149597863936.0;

    // Convert kilometers to AU
    public static double KmToAU(double km)
    {
        return km / AU_KM;
    }

    // Convert meters to AU
    public static double MToAU(double meters)
    {
        return meters / AU_M;
    }

    // Convert AU to kilometers
    public static double AUToKm(double au)
    {
        return au * AU_KM;
    }

    // Convert AU to meters
    public static double AUToM(double au)
    {
        return au * AU_M;
    }

    // Euclidean distance in AU between two points (x,y in AU)
    public static double DistanceAU(double x0, double y0, double x1, double y1)
    {
        double dx = x1 - x0;
        double dy = y1 - y0;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // Convenience: distance from a Ship to Sol (returns AU). Returns NaN if data not available.
    public static double DistanceShipToSolAU(Ship ship)
    {
        if (ship == null || ship.objSS == null || CrewSim.system == null || CrewSim.system.boStar == null)
        {
            return double.NaN;
        }

        double now = StarSystem.fEpoch;
        if (ShipAUCache.TryGetValue(ship, out ShipAUCacheEntry cached) && cached.ExpiresAt > now)
        {
            return cached.DistAU;
        }

        double distAU = DistanceAU(
            ship.objSS.vPosx,
            ship.objSS.vPosy,
            CrewSim.system.boStar.dXReal,
            CrewSim.system.boStar.dYReal);

        ShipAUCache[ship] = new ShipAUCacheEntry
        {
            DistAU = distAU,
            ExpiresAt = now + SHIP_AU_CACHE_SECONDS
        };

        return distAU;
    }
}
