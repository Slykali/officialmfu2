using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Read-only description of the field layout, derived from <see cref="GameConfig"/>.
    /// The field builder constructs geometry that matches these numbers, and robots/AI/camera
    /// use them to navigate. The field is point-symmetric: blue's features are red's rotated 180°.
    /// </summary>
    public static class Arena
    {
        public static float HalfLength => GameConfig.FieldLength * 0.5f;
        public static float HalfWidth => GameConfig.FieldWidth * 0.5f;

        public static Vector3 HubCenter => Vector3.zero;

        /// <summary>Point shooters aim at: just above the middle of the hub opening.</summary>
        public static Vector3 HubAimPoint => new Vector3(0f, GameConfig.HubOpeningHeight + 0.15f, 0f);

        /// <summary>Robots should keep their center at least this far from the hub center.</summary>
        public static float HubKeepOutRadius => GameConfig.HubBaseRadius + GameConfig.RobotLength * 0.5f + 0.15f;

        /// <summary>-1 for red (wall at -X), +1 for blue (wall at +X).</summary>
        public static float WallSign(Alliance alliance) => alliance == Alliance.Red ? -1f : 1f;

        /// <summary>X coordinate of an alliance's own wall (inner face).</summary>
        public static float WallX(Alliance alliance) => WallSign(alliance) * HalfLength;

        /// <summary>Unit vector pointing from an alliance's wall toward the field center.</summary>
        public static Vector3 AllianceForward(Alliance alliance) => new Vector3(-WallSign(alliance), 0f, 0f);

        /// <summary>Yaw (degrees, Unity convention: 0 = +Z, 90 = +X) that faces downfield for an alliance.</summary>
        public static float AllianceForwardYaw(Alliance alliance) => alliance == Alliance.Red ? 90f : -90f;

        public static Bounds FieldBounds =>
            new Bounds(Vector3.zero, new Vector3(GameConfig.FieldLength, 4f, GameConfig.FieldWidth));

        /// <summary>The zone robots start in. Driving fully out of it during auto earns leave points.</summary>
        public static Bounds StartingZone(Alliance alliance)
        {
            float depth = GameConfig.StartingZoneDepth;
            float centerX = WallX(alliance) - WallSign(alliance) * depth * 0.5f;
            return new Bounds(new Vector3(centerX, 1f, 0f), new Vector3(depth, 2f, GameConfig.FieldWidth));
        }

        /// <summary>Endgame climb zone in front of the alliance's climbing tower.</summary>
        public static Bounds ClimbZone(Alliance alliance)
        {
            float depth = GameConfig.ClimbZoneDepth;
            float centerX = WallX(alliance) - WallSign(alliance) * depth * 0.5f;
            float centerZ = (GameConfig.ClimbZoneZMin + GameConfig.ClimbZoneZMax) * 0.5f;
            if (alliance == Alliance.Blue) centerZ = -centerZ;
            float width = GameConfig.ClimbZoneZMax - GameConfig.ClimbZoneZMin;
            return new Bounds(new Vector3(centerX, 1f, centerZ), new Vector3(depth, 2f, width));
        }

        /// <summary>Ground point at the base of the climbing tower (against the alliance wall, centered on the climb zone).</summary>
        public static Vector3 ClimbAnchor(Alliance alliance)
        {
            Bounds zone = ClimbZone(alliance);
            return new Vector3(WallX(alliance), 0f, zone.center.z);
        }

        /// <summary>Starting position for robot slot 0..RobotsPerAlliance-1 (y = 0, on the carpet).</summary>
        public static Vector3 StartPosition(Alliance alliance, int slot)
        {
            int count = Mathf.Max(1, GameConfig.RobotsPerAlliance);
            float spacing = 2.4f;
            float z = (slot - (count - 1) * 0.5f) * spacing;
            if (alliance == Alliance.Blue) z = -z;
            float x = WallX(alliance) - WallSign(alliance) * GameConfig.StartLineInset;
            return new Vector3(x, 0f, z);
        }

        public static bool IsInsideField(Vector3 p, float margin = 0f) =>
            Mathf.Abs(p.x) <= HalfLength - margin && Mathf.Abs(p.z) <= HalfWidth - margin;

        public static Vector3 ClampToField(Vector3 p, float margin = 0f)
        {
            p.x = Mathf.Clamp(p.x, -HalfLength + margin, HalfLength - margin);
            p.z = Mathf.Clamp(p.z, -HalfWidth + margin, HalfWidth - margin);
            return p;
        }

        /// <summary>Elevated camera position behind an alliance wall, like a real driver's view.</summary>
        public static Vector3 DriverStationCameraPosition(Alliance alliance) =>
            new Vector3(WallX(alliance) + WallSign(alliance) * 2.5f, 5.0f, 0f);
    }
}
