using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Decorative arena around the field: dark floor, bleachers with a block crowd (combined into
    /// a few meshes per color), dasher boards, end backdrops with banners, an overhead truss with
    /// emissive light bars and three live video boards. Nothing here has a collider.
    /// </summary>
    internal static class VenueBuilder
    {
        const int BleacherRows = 9;
        const float RowDepth = 0.85f;
        const float RowRise = 0.42f;
        const float FirstRowHeight = 0.45f;
        const float BleacherLength = 28f;
        const float SeatPitch = 0.6f;
        const float TrussHeight = 9.5f;
        static readonly float[] Aisles = { -9f, 0f, 9f };

        static readonly Color[] ShirtColors =
        {
            GameConfig.RedAllianceColor,
            GameConfig.BlueAllianceColor,
            GameConfig.TeamAccentColor,
            new Color(0.92f, 0.92f, 0.92f),
            new Color(0.35f, 0.36f, 0.40f),
            new Color(0.12f, 0.12f, 0.14f),
            new Color(0.15f, 0.55f, 0.30f),
            new Color(0.95f, 0.50f, 0.12f),
        };

        static readonly Color[] SkinColors =
        {
            new Color(0.96f, 0.80f, 0.66f),
            new Color(0.84f, 0.64f, 0.48f),
            new Color(0.62f, 0.43f, 0.30f),
            new Color(0.38f, 0.26f, 0.18f),
        };

        public static void Build(Transform root)
        {
            Transform venue = FieldKit.Group("Venue", root);
            BuildFloor(venue);
            var crowd = FieldKit.Group("Crowd", venue).gameObject.AddComponent<CrowdCheer>();
            var random = new System.Random(GameConfig.TeamNumber);
            BuildBleachers(venue, crowd, +1f, random);
            BuildBleachers(venue, crowd, -1f, random);
            BuildDasherBoards(venue);
            BuildBackdrops(venue);
            BuildTruss(venue);
            BuildVideoBoards(venue);
        }

        static void BuildFloor(Transform venue)
        {
            FieldKit.Box("Arena Floor", venue, new Vector3(0f, -0.03f, 0f), new Vector3(100f, 0.02f, 70f),
                MaterialFactory.Lit(FieldKit.VenueFloorColor, 0.25f), shadows: false);
            FieldKit.Box("Field Apron", venue, new Vector3(0f, -0.015f, 0f),
                new Vector3(GameConfig.FieldLength + 6f, 0.01f, GameConfig.FieldWidth + 4.2f),
                MaterialFactory.Lit(new Color(0.19f, 0.195f, 0.21f), 0.2f), shadows: false);
        }

        static void BuildBleachers(Transform venue, CrowdCheer crowd, float side, System.Random random)
        {
            string label = side > 0f ? "+Z" : "-Z";
            Transform group = FieldKit.Group("Bleachers " + label, venue);
            float zStart = Arena.HalfWidth + 2.8f;

            var steps = new FieldMeshBuilder();
            var edges = new FieldMeshBuilder();
            for (int r = 0; r < BleacherRows; r++)
            {
                float top = FirstRowHeight + r * RowRise;
                float zc = side * (zStart + (r + 0.5f) * RowDepth);
                steps.AddBox(new Vector3(0f, (top - 0.02f) * 0.5f, zc), new Vector3(BleacherLength, top + 0.02f, RowDepth));
                edges.AddBox(new Vector3(0f, top - 0.02f, zc - side * (RowDepth * 0.5f - 0.03f)),
                    new Vector3(BleacherLength + 0.02f, 0.045f, 0.07f));
            }
            // Back wall behind the top row.
            float backTop = FirstRowHeight + BleacherRows * RowRise + 1.6f;
            steps.AddBox(new Vector3(0f, backTop * 0.5f, side * (zStart + BleacherRows * RowDepth + 0.15f)),
                new Vector3(BleacherLength, backTop, 0.3f));
            FieldKit.MeshObject("Steps", group, steps.Build("Bleacher Steps " + label),
                MaterialFactory.Lit(FieldKit.VenueDarkColor, 0.2f), Vector3.zero);
            FieldKit.MeshObject("Step Edges", group, edges.Build("Bleacher Edges " + label),
                FieldKit.DarkAluminum, Vector3.zero, shadows: false);

            // Crowd: one combined mesh per shirt color and per skin tone.
            var shirts = new FieldMeshBuilder[ShirtColors.Length];
            var skins = new FieldMeshBuilder[SkinColors.Length];
            for (int i = 0; i < shirts.Length; i++) shirts[i] = new FieldMeshBuilder();
            for (int i = 0; i < skins.Length; i++) skins[i] = new FieldMeshBuilder();

            int seats = Mathf.FloorToInt((BleacherLength - 1f) / SeatPitch);
            for (int r = 0; r < BleacherRows; r++)
            {
                float top = FirstRowHeight + r * RowRise;
                float zc = side * (zStart + (r + 0.5f) * RowDepth + 0.08f);
                for (int s = 0; s < seats; s++)
                {
                    float x = -BleacherLength * 0.5f + 0.5f + s * SeatPitch;
                    if (NearAisle(x) || random.NextDouble() > 0.8) continue;

                    bool standing = random.NextDouble() < 0.07;
                    float bodyH = standing ? 0.85f : 0.5f;
                    float lean = (float)(random.NextDouble() - 0.5) * 0.08f;
                    float jx = (float)(random.NextDouble() - 0.5) * 0.12f;
                    Quaternion yaw = Quaternion.Euler(0f, (float)(random.NextDouble() - 0.5) * 24f, lean * 60f);
                    Vector3 bodyCenter = new Vector3(x + jx, top + bodyH * 0.5f, zc);
                    shirts[PickShirt(random)].AddBox(bodyCenter, new Vector3(0.44f, bodyH, 0.3f), yaw);
                    Vector3 headCenter = bodyCenter + yaw * new Vector3(0f, bodyH * 0.5f + 0.14f, 0f);
                    skins[random.Next(SkinColors.Length)].AddBox(headCenter, new Vector3(0.22f, 0.24f, 0.22f), yaw);
                }
            }

            Transform crowdRoot = crowd.transform;
            for (int i = 0; i < shirts.Length; i++)
            {
                if (shirts[i].IsEmpty) continue;
                MeshRenderer mr = FieldKit.MeshObject("Crowd " + label + " Shirts " + i, crowdRoot,
                    shirts[i].Build("Crowd Shirts"), MaterialFactory.Lit(ShirtColors[i], 0.2f), Vector3.zero, shadows: false);
                crowd.Add(mr.transform, (float)random.NextDouble() * 6.28f);
            }
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i].IsEmpty) continue;
                MeshRenderer mr = FieldKit.MeshObject("Crowd " + label + " Heads " + i, crowdRoot,
                    skins[i].Build("Crowd Heads"), MaterialFactory.Lit(SkinColors[i], 0.25f), Vector3.zero, shadows: false);
                crowd.Add(mr.transform, (float)random.NextDouble() * 6.28f);
            }
        }

        static bool NearAisle(float x)
        {
            foreach (float a in Aisles)
                if (Mathf.Abs(x - a) < 0.55f) return true;
            return false;
        }

        /// <summary>Mostly alliance colors and team gold, with the rest of the palette mixed in.</summary>
        static int PickShirt(System.Random random)
        {
            double roll = random.NextDouble();
            if (roll < 0.22) return 0;          // red
            if (roll < 0.44) return 1;          // blue
            if (roll < 0.56) return 2;          // team gold
            return 3 + random.Next(ShirtColors.Length - 3);
        }

        static void BuildDasherBoards(Transform venue)
        {
            Material board = MaterialFactory.Lit(new Color(0.09f, 0.09f, 0.1f), 0.4f);
            Material ribbon = MaterialFactory.Emissive(GameConfig.TeamAccentColor, 0.9f);
            float length = GameConfig.FieldLength + 6f;
            for (int s = -1; s <= 1; s += 2)
            {
                Transform group = FieldKit.Group(s > 0 ? "Dasher Board +Z" : "Dasher Board -Z", venue);
                float z = s * (Arena.HalfWidth + 2.2f);
                Vector3 inward = new Vector3(0f, 0f, -s);
                FieldKit.Box("Board", group, new Vector3(0f, 0.5f, z), new Vector3(length, 1.0f, 0.08f), board);
                FieldKit.Box("LED Ribbon", group, new Vector3(0f, 0.78f, z - s * 0.045f), new Vector3(length, 0.26f, 0.01f),
                    ribbon, shadows: false);
                for (int i = -1; i <= 1; i++)
                {
                    FieldKit.Text("FRC TEAM " + GameConfig.TeamNumber + "  |  ROBOT ARENA", group,
                        new Vector3(i * 7.5f, 0.78f, z - s * 0.05f), inward, 0.14f, new Color(0.08f, 0.08f, 0.1f));
                }
            }
        }

        static void BuildBackdrops(Transform venue)
        {
            Material curtain = MaterialFactory.Lit(new Color(0.045f, 0.045f, 0.06f), 0.1f);
            Material bannerMat = MaterialFactory.Lit(GameConfig.TeamSecondaryColor, 0.3f);
            Material trim = MaterialFactory.Emissive(GameConfig.TeamAccentColor, 1.0f);
            for (int s = -1; s <= 1; s += 2)
            {
                Transform group = FieldKit.Group(s > 0 ? "Backdrop +X" : "Backdrop -X", venue);
                float x = s * (Arena.HalfLength + 6.5f);
                Vector3 inward = new Vector3(-s, 0f, 0f);
                FieldKit.Box("Curtain", group, new Vector3(x, 5f, 0f), new Vector3(0.3f, 10f, 36f), curtain);

                float face = x - s * 0.15f;
                FieldKit.Box("Top Banner", group, new Vector3(face - s * 0.03f, 8.1f, 0f), new Vector3(0.05f, 1.0f, 17f),
                    bannerMat, shadows: false);
                FieldKit.Box("Top Banner Trim", group, new Vector3(face - s * 0.04f, 7.58f, 0f), new Vector3(0.04f, 0.05f, 17f),
                    trim, shadows: false);
                FieldKit.Text("FIRST ROBOTICS COMPETITION-STYLE DEMO", group, new Vector3(face - s * 0.06f, 8.1f, 0f), inward,
                    0.42f, Color.white);

                FieldKit.Box("Team Banner", group, new Vector3(face - s * 0.03f, 2.6f, 0f), new Vector3(0.05f, 1.6f, 10f),
                    bannerMat, shadows: false);
                FieldKit.Box("Team Banner Trim", group, new Vector3(face - s * 0.04f, 1.78f, 0f), new Vector3(0.04f, 0.05f, 10f),
                    trim, shadows: false);
                FieldKit.Text("TEAM " + GameConfig.TeamNumber, group, new Vector3(face - s * 0.06f, 2.6f, 0f), inward, 0.95f,
                    GameConfig.TeamAccentColor);
            }
        }

        static void BuildTruss(Transform venue)
        {
            Transform group = FieldKit.Group("Truss", venue);
            Material steel = MaterialFactory.Lit(new Color(0.2f, 0.21f, 0.23f), 0.4f, 0.2f);
            Material lamp = MaterialFactory.Emissive(new Color(1f, 0.97f, 0.9f), 2.5f);
            float xEnd = Arena.HalfLength + 3f;
            float zSide = Arena.HalfWidth + 2f;
            float y = TrussHeight;

            for (int s = -1; s <= 1; s += 2)
            {
                FieldKit.Box("Side Chord", group, new Vector3(0f, y, s * zSide), new Vector3(xEnd * 2f + 0.4f, 0.4f, 0.4f), steel,
                    shadows: false);
                FieldKit.Box("End Chord", group, new Vector3(s * xEnd, y, 0f), new Vector3(0.4f, 0.4f, zSide * 2f), steel,
                    shadows: false);
                for (int t = -1; t <= 1; t += 2)
                {
                    FieldKit.Box("Tower", group, new Vector3(s * xEnd, y * 0.5f, t * zSide), new Vector3(0.4f, y, 0.4f), steel,
                        shadows: false);
                }
            }

            float[] beams = { -6.6f, -2.2f, 2.2f, 6.6f };
            float[] lamps = { -2.8f, 0f, 2.8f };
            foreach (float bx in beams)
            {
                FieldKit.Box("Cross Beam", group, new Vector3(bx, y, 0f), new Vector3(0.3f, 0.3f, zSide * 2f), steel,
                    shadows: false);
                foreach (float lz in lamps)
                {
                    FieldKit.Box("Lamp Hanger", group, new Vector3(bx, y - 0.25f, lz), new Vector3(0.06f, 0.25f, 0.06f), steel,
                        shadows: false);
                    FieldKit.Box("Lamp Housing", group, new Vector3(bx, y - 0.4f, lz), new Vector3(1.3f, 0.12f, 0.42f), steel,
                        shadows: false);
                    FieldKit.Box("Lamp", group, new Vector3(bx, y - 0.47f, lz), new Vector3(1.2f, 0.03f, 0.34f), lamp,
                        shadows: false);
                }
            }
        }

        static void BuildVideoBoards(Transform venue)
        {
            Transform group = FieldKit.Group("Video Boards", venue);
            Material steel = MaterialFactory.Lit(new Color(0.2f, 0.21f, 0.23f), 0.4f, 0.2f);

            // Main board above the back rows of the +Z stands: behind the broadcast camera and the
            // menu orbit, high enough to clear the crowd in the top rows.
            float boardZ = Arena.HalfWidth + 9.6f;
            float boardY = 7.0f, boardH = 3.4f;
            FieldScoreboard.Create("Main Video Board", group, new Vector3(0f, boardY, boardZ), Vector3.back, 9f, boardH);
            FieldKit.Box("Board Truss", group, new Vector3(0f, TrussHeight, boardZ + 0.2f), new Vector3(10f, 0.3f, 0.3f), steel,
                shadows: false);
            for (int s = -1; s <= 1; s += 2)
            {
                float cableBottom = boardY + boardH * 0.5f + 0.15f;
                float cableLength = TrussHeight - cableBottom;
                FieldKit.Box("Cable", group, new Vector3(s * 3.5f, cableBottom + cableLength * 0.5f, boardZ + 0.2f),
                    new Vector3(0.03f, cableLength, 0.03f), steel, shadows: false);
                FieldKit.Box("Truss Link", group, new Vector3(s * 4.8f, TrussHeight, (boardZ + Arena.HalfWidth + 2f) * 0.5f),
                    new Vector3(0.25f, 0.25f, boardZ - Arena.HalfWidth - 2f), steel, shadows: false);
            }

            // End boards, readable from each driver station camera.
            float endX = Arena.HalfLength + 5.85f;
            FieldScoreboard.Create("End Video Board +X", group, new Vector3(endX, 5.4f, 0f), Vector3.left, 6.4f, 2.6f);
            FieldScoreboard.Create("End Video Board -X", group, new Vector3(-endX, 5.4f, 0f), Vector3.right, 6.4f, 2.6f);
        }
    }
}
