using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Carpet, side rails and alliance (driver station) walls. Wall inner faces sit exactly on
    /// the field boundary (x = ±<see cref="Arena.HalfLength"/>, z = ±<see cref="Arena.HalfWidth"/>),
    /// and the colliders overlap at the corners so nothing can slip out.
    /// </summary>
    internal static class FieldPerimeter
    {
        const float SideWallThickness = 0.10f;
        const float SideWallFullHeight = 1.0f;
        const float AllianceWallThickness = 0.15f;
        const float AllianceLowerHeight = 0.95f;
        /// <summary>Lateral centers of the three driver stations, in alliance-wall local X.</summary>
        static readonly float[] StationOffsets = { -2.6f, 0f, 2.6f };

        public static void Build(Transform root)
        {
            BuildCarpet(root);
            BuildSideWall(root, +1f);
            BuildSideWall(root, -1f);
            BuildAllianceWall(root, Alliance.Red);
            BuildAllianceWall(root, Alliance.Blue);
        }

        static void BuildCarpet(Transform root)
        {
            Transform group = FieldKit.Group("Carpet", root);
            float length = GameConfig.FieldLength + 0.4f;
            float width = GameConfig.FieldWidth + 0.4f;
            GameObject carpet = FieldKit.Box("Carpet Surface", group, new Vector3(0f, -0.025f, 0f),
                new Vector3(length, 0.05f, width), MaterialFactory.Lit(FieldKit.CarpetColor, 0.04f), shadows: false);
            carpet.GetComponent<Renderer>().receiveShadows = true;

            // One big collider whose top face is exactly y = 0. It extends past the walls so a ball
            // that escapes lands on something before GamePiece's safety net brings it back.
            float margin = 1.5f;
            FieldKit.SolidBox("Carpet Collider", group, new Vector3(0f, -0.25f, 0f),
                new Vector3(GameConfig.FieldLength + margin * 2f, 0.5f, GameConfig.FieldWidth + margin * 2f),
                0.3f, 0.6f);
        }

        /// <summary>Side rail along z = side * HalfWidth: solid kick plate, polycarbonate above, aluminum frame.</summary>
        static void BuildSideWall(Transform root, float side)
        {
            Transform group = FieldKit.Group(side > 0f ? "Side Wall +Z" : "Side Wall -Z", root);
            float length = GameConfig.FieldLength + 2f * AllianceWallThickness;
            float zMid = side * (Arena.HalfWidth + SideWallThickness * 0.5f);
            float zFace = side * Arena.HalfWidth;
            float rail = GameConfig.PerimeterHeight;
            Vector3 inward = new Vector3(0f, 0f, -side);

            FieldKit.Box("Kick Plate", group, new Vector3(0f, rail * 0.5f, zMid),
                new Vector3(length, rail, SideWallThickness), FieldKit.DarkAluminum);
            FieldKit.Box("Kick Plate Stripe", group, new Vector3(0f, rail - 0.06f, zFace - side * 0.004f),
                new Vector3(length, 0.04f, 0.008f), MaterialFactory.Lit(GameConfig.TeamAccentColor, 0.4f), shadows: false);
            FieldKit.Quad("Polycarbonate", group, new Vector3(0f, (rail + SideWallFullHeight) * 0.5f, zMid),
                Quaternion.identity, new Vector2(length, SideWallFullHeight - rail), FieldKit.Polycarbonate);
            FieldKit.Box("Mid Rail", group, new Vector3(0f, rail, zMid), new Vector3(length, 0.05f, 0.09f), FieldKit.Aluminum);
            FieldKit.Box("Top Rail", group, new Vector3(0f, SideWallFullHeight, zMid), new Vector3(length, 0.05f, 0.09f),
                FieldKit.Aluminum);

            int posts = 9;
            for (int i = 0; i < posts; i++)
            {
                float x = Mathf.Lerp(-Arena.HalfLength, Arena.HalfLength, i / (float)(posts - 1));
                FieldKit.Box("Post", group, new Vector3(x, SideWallFullHeight * 0.5f, zMid),
                    new Vector3(0.05f, SideWallFullHeight, 0.09f), FieldKit.Aluminum);
            }

            // Team signage on the kick plate, readable from inside the field.
            for (int i = -1; i <= 1; i += 2)
            {
                FieldKit.Text("TEAM " + GameConfig.TeamNumber, group,
                    new Vector3(i * 4.6f, rail * 0.45f, zFace), inward, 0.2f, new Color(1f, 1f, 1f, 0.9f));
            }

            // Collider covers the full rail + polycarbonate height.
            FieldKit.SolidBox("Side Wall Collider", group, new Vector3(0f, SideWallFullHeight * 0.5f, zMid),
                new Vector3(length, SideWallFullHeight, SideWallThickness));
        }

        /// <summary>
        /// Driver station wall. Built in a local frame where +Z points into the field and the inner
        /// face is at local z = 0, so red and blue are exact 180° rotations of each other.
        /// </summary>
        static void BuildAllianceWall(Transform root, Alliance alliance)
        {
            Transform wall = FieldKit.Group(alliance.DisplayName() + " Alliance Wall", root);
            wall.localPosition = new Vector3(Arena.WallX(alliance), 0f, 0f);
            wall.localRotation = Quaternion.LookRotation(Arena.AllianceForward(alliance));

            Color allianceColor = alliance.ToColor();
            Material allianceMat = MaterialFactory.Lit(allianceColor, 0.35f);
            Material darkAllianceMat = MaterialFactory.Lit(FieldKit.Mix(allianceColor, Color.black, 0.45f), 0.3f);
            float width = GameConfig.FieldWidth + 2f * SideWallThickness + 0.02f;
            float height = GameConfig.AllianceWallHeight;
            float t = AllianceWallThickness;
            float zMid = -t * 0.5f;

            // Lower solid section: dark base strip + alliance colored panel.
            FieldKit.Box("Base Strip", wall, new Vector3(0f, 0.09f, zMid), new Vector3(width, 0.18f, t), FieldKit.Charcoal);
            FieldKit.Box("Lower Panel", wall, new Vector3(0f, (0.18f + AllianceLowerHeight) * 0.5f, zMid),
                new Vector3(width, AllianceLowerHeight - 0.18f, t), allianceMat);

            // Upper polycarbonate and aluminum frame.
            FieldKit.Quad("Polycarbonate", wall, new Vector3(0f, (AllianceLowerHeight + height) * 0.5f, zMid),
                Quaternion.identity, new Vector2(width, height - AllianceLowerHeight), FieldKit.Polycarbonate);
            FieldKit.Box("Mid Rail", wall, new Vector3(0f, AllianceLowerHeight, zMid), new Vector3(width, 0.06f, t + 0.02f),
                FieldKit.Aluminum);
            FieldKit.Box("Top Rail", wall, new Vector3(0f, height, zMid), new Vector3(width, 0.07f, t + 0.02f), FieldKit.Aluminum);
            float halfW = width * 0.5f;
            foreach (float x in new[] { -halfW + 0.04f, -3.9f, -1.3f, 1.3f, 3.9f, halfW - 0.04f })
            {
                FieldKit.Box("Post", wall, new Vector3(x, (AllianceLowerHeight + height) * 0.5f, zMid),
                    new Vector3(0.07f, height - AllianceLowerHeight, t + 0.02f), FieldKit.Aluminum);
            }

            // Three driver station windows with station plates and a counter behind each.
            Material frameMat = FieldKit.DarkAluminum;
            for (int i = 0; i < StationOffsets.Length; i++)
            {
                float x = StationOffsets[i];
                float wy0 = 1.08f, wy1 = 1.82f, ww = 1.8f;
                float wyMid = (wy0 + wy1) * 0.5f;
                FieldKit.Box("Window Frame Top", wall, new Vector3(x, wy1, zMid), new Vector3(ww, 0.05f, t + 0.03f), frameMat);
                FieldKit.Box("Window Frame Bottom", wall, new Vector3(x, wy0, zMid), new Vector3(ww, 0.05f, t + 0.03f), frameMat);
                FieldKit.Box("Window Frame Left", wall, new Vector3(x - ww * 0.5f, wyMid, zMid),
                    new Vector3(0.05f, wy1 - wy0, t + 0.03f), frameMat);
                FieldKit.Box("Window Frame Right", wall, new Vector3(x + ww * 0.5f, wyMid, zMid),
                    new Vector3(0.05f, wy1 - wy0, t + 0.03f), frameMat);

                // Station number plate above the window (field side).
                FieldKit.Box("Station Plate", wall, new Vector3(x, 1.91f, 0.004f), new Vector3(0.62f, 0.12f, 0.008f),
                    darkAllianceMat, shadows: false);
                FieldKit.Text(alliance.DisplayName() + " " + (i + 1), wall, new Vector3(x, 1.91f, 0.008f), Vector3.forward,
                    0.07f, Color.white);

                // Driver station counter behind the wall.
                FieldKit.Box("Station Counter", wall, new Vector3(x, 0.45f, -t - 0.4f), new Vector3(1.9f, 0.9f, 0.6f),
                    FieldKit.Charcoal);
                FieldKit.Box("Counter Top", wall, new Vector3(x, 0.915f, -t - 0.4f), new Vector3(1.96f, 0.03f, 0.66f),
                    FieldKit.Aluminum);
                FieldKit.Box("Driver Laptop", wall, new Vector3(x + 0.3f, 0.95f, -t - 0.45f), new Vector3(0.36f, 0.03f, 0.26f),
                    FieldKit.Rubber, shadows: false);
            }

            // Alliance signage on the field-facing lower panel (offset away from the climb tower).
            FieldKit.Text(alliance.DisplayName() + " ALLIANCE", wall, new Vector3(1.15f, 0.56f, 0f), Vector3.forward,
                0.3f, Color.white);

            // "FRC 6874" banner on top of the wall, readable from the field and from the driver station.
            FieldKit.Box("Team Banner", wall, new Vector3(0f, height + 0.24f, zMid), new Vector3(2.7f, 0.42f, 0.06f),
                MaterialFactory.Lit(GameConfig.TeamSecondaryColor, 0.4f));
            FieldKit.Box("Team Banner Trim", wall, new Vector3(0f, height + 0.05f, zMid), new Vector3(2.7f, 0.04f, 0.07f),
                MaterialFactory.Emissive(GameConfig.TeamAccentColor, 0.8f), shadows: false);
            FieldKit.Text("FRC " + GameConfig.TeamNumber, wall, new Vector3(0f, height + 0.25f, zMid + 0.03f), Vector3.forward,
                0.24f, GameConfig.TeamAccentColor);
            FieldKit.Text("FRC " + GameConfig.TeamNumber, wall, new Vector3(0f, height + 0.25f, zMid - 0.03f), Vector3.back,
                0.24f, GameConfig.TeamAccentColor);

            // Solid collider for the full wall height, wide enough to overlap both side walls.
            FieldKit.SolidBox("Alliance Wall Collider", wall, new Vector3(0f, height * 0.5f, zMid),
                new Vector3(width + 0.2f, height, t));
        }
    }
}
