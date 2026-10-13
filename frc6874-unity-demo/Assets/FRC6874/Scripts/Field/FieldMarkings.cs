using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Visual-only floor markings, a few millimeters above the carpet: zone tints, center line,
    /// starting lines, climb zone outlines, the hub keep-out ring and painted team numbers.
    /// Layers have distinct heights so they never z-fight.
    /// </summary>
    internal static class FieldMarkings
    {
        const float TapeWidth = 0.05f;
        const float TintTop = 0.0012f;
        const float FillTop = 0.0022f;
        const float TapeTop = 0.0035f;
        const float PaintTop = 0.0045f;

        public static void Build(Transform root)
        {
            Transform group = FieldKit.Group("Markings", root);

            foreach (Alliance alliance in new[] { Alliance.Red, Alliance.Blue })
                BuildAllianceMarkings(group, alliance);

            // Center line (the hub hides its middle).
            Tape(group, "Center Line", new Vector3(0f, 0f, 0f), new Vector3(TapeWidth, 0f, GameConfig.FieldWidth),
                FieldKit.TapeWhite, TapeTop);

            // Hub keep-out ring and a darker apron under the hub's exit ports.
            var builder = new FieldMeshBuilder();
            float r = Arena.HubKeepOutRadius;
            builder.AddAnnulus(72, r - TapeWidth * 0.5f, r + TapeWidth * 0.5f, TapeTop, true);
            FieldKit.MeshObject("Hub Keep-Out Ring", group, builder.Build("Hub Ring"), FieldKit.Tape(FieldKit.TapeWhite),
                Arena.HubCenter, shadows: false);

            builder.Clear();
            builder.AddAnnulus(48, GameConfig.HubBaseRadius - 0.02f, GameConfig.HubBaseRadius + 0.32f, FillTop, true);
            FieldKit.MeshObject("Hub Apron", group, builder.Build("Hub Apron"),
                MaterialFactory.Lit(FieldKit.Mix(FieldKit.CarpetColor, Color.black, 0.45f), 0.1f), Arena.HubCenter, shadows: false);
        }

        static void BuildAllianceMarkings(Transform group, Alliance alliance)
        {
            Color color = alliance.ToColor();
            float sign = Arena.WallSign(alliance);
            string prefix = alliance.DisplayName() + " ";

            // Starting zone: subtle tint plus a solid line on its inner edge.
            Bounds start = Arena.StartingZone(alliance);
            Tape(group, prefix + "Starting Zone Tint", new Vector3(start.center.x, 0f, 0f),
                new Vector3(start.size.x, 0f, GameConfig.FieldWidth), FieldKit.Mix(FieldKit.CarpetColor, color, 0.10f), TintTop);
            float startLineX = Arena.WallX(alliance) - sign * GameConfig.StartingZoneDepth;
            Tape(group, prefix + "Starting Line", new Vector3(startLineX, 0f, 0f),
                new Vector3(TapeWidth, 0f, GameConfig.FieldWidth), color, TapeTop);

            // Climb zone: tinted fill plus an outline on the three open sides (the wall is the fourth).
            Bounds climb = Arena.ClimbZone(alliance);
            Vector3 c = climb.center;
            Vector3 s = climb.size;
            Tape(group, prefix + "Climb Zone Fill", new Vector3(c.x, 0f, c.z), new Vector3(s.x, 0f, s.z),
                FieldKit.Mix(FieldKit.CarpetColor, color, 0.30f), FillTop);
            float innerX = Arena.WallX(alliance) - sign * (s.x - TapeWidth * 0.5f);
            Tape(group, prefix + "Climb Zone Front", new Vector3(innerX, 0f, c.z), new Vector3(TapeWidth, 0f, s.z), color, TapeTop);
            float zMin = c.z - s.z * 0.5f + TapeWidth * 0.5f;
            float zMax = c.z + s.z * 0.5f - TapeWidth * 0.5f;
            Tape(group, prefix + "Climb Zone Side A", new Vector3(c.x, 0f, zMin), new Vector3(s.x, 0f, TapeWidth), color, TapeTop);
            Tape(group, prefix + "Climb Zone Side B", new Vector3(c.x, 0f, zMax), new Vector3(s.x, 0f, TapeWidth), color, TapeTop);

            // Painted team number in each half, upright for that half's driver station.
            Vector3 forward = Arena.AllianceForward(alliance);
            Vector3 paintPos = new Vector3(sign * 2.45f, PaintTop, 0f);
            Quaternion flat = Quaternion.LookRotation(Vector3.down, forward);
            TextFactory.Create(GameConfig.TeamNumber.ToString(), group, paintPos, flat, 0.85f, new Color(1f, 1f, 1f, 0.14f));
            TextFactory.Create("FRC", group, paintPos + forward * -0.75f, flat, 0.3f, new Color(1f, 1f, 1f, 0.14f));
        }

        /// <summary>Flat strip: <paramref name="size"/>.y is ignored, the strip spans y = 0..<paramref name="top"/>.</summary>
        static void Tape(Transform parent, string name, Vector3 center, Vector3 size, Color color, float top)
        {
            FieldKit.Box(name, parent, new Vector3(center.x, top * 0.5f, center.z), new Vector3(size.x, top, size.z),
                FieldKit.Tape(color), shadows: false);
        }
    }
}
