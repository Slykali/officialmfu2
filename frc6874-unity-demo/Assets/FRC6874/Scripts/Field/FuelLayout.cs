using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// The match-start ball layout, point-symmetric like the field. For each alliance: two staggered
    /// rows parallel to the center line between its starting zone and the hub, plus a six-ball depot
    /// against the side wall at the edge of its starting zone. Positions are derived from
    /// <see cref="Arena"/>/<see cref="GameConfig"/> and validated (inside the field, clear of the hub
    /// keep-out ring and of every robot start position, no overlaps).
    /// </summary>
    public static class FuelLayout
    {
        const float StartClearance = 0.8f;

        /// <summary>Ball centers (y = FuelRadius + 0.01) for the match-start layout, both alliances.</summary>
        public static List<Vector3> MatchStartPositions()
        {
            var result = new List<Vector3>();
            foreach (Vector3 p in RedHalf())
            {
                TryAdd(result, p);
                TryAdd(result, new Vector3(-p.x, p.y, -p.z));   // blue: rotated 180° about the hub
            }
            return result;
        }

        /// <summary>Despawns every ball and spawns the match-start layout. Returns the number spawned.</summary>
        public static int SpawnMatchStart()
        {
            GamePiece.DespawnAll();
            List<Vector3> positions = MatchStartPositions();
            foreach (Vector3 p in positions) GamePiece.Spawn(p);
            return positions.Count;
        }

        static IEnumerable<Vector3> RedHalf()
        {
            float y = GameConfig.FuelRadius + 0.01f;
            float startLine = Arena.WallX(Alliance.Red) + GameConfig.StartingZoneDepth;   // inner edge of red's starting zone

            // Row A (nearer the starting zone): 8 balls, row B (nearer the hub): 7 balls, staggered.
            float rowA = startLine + 1.45f;
            float rowB = startLine + 2.05f;
            for (int i = 0; i < 8; i++) yield return new Vector3(rowA, y, (i - 3.5f) * 0.9f);
            for (int i = 0; i < 7; i++) yield return new Vector3(rowB, y, (i - 3f) * 0.9f);

            // Depot: 3 × 2 cluster against the -Z side wall (red's climb zone is on the +Z side).
            float wallZ = -Arena.HalfWidth;
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = 0; iz < 2; iz++)
                    yield return new Vector3(startLine - 0.25f + ix * 0.3f, y, wallZ + 0.25f + iz * 0.3f);
        }

        static void TryAdd(List<Vector3> list, Vector3 p)
        {
            float r = GameConfig.FuelRadius;
            if (!Arena.IsInsideField(p, r + 0.05f)) return;
            Vector3 flat = new Vector3(p.x, 0f, p.z);
            if ((flat - Arena.HubCenter).magnitude < Arena.HubKeepOutRadius + r) return;
            foreach (Alliance a in new[] { Alliance.Red, Alliance.Blue })
                for (int slot = 0; slot < GameConfig.RobotsPerAlliance; slot++)
                    if ((flat - Arena.StartPosition(a, slot)).magnitude < StartClearance) return;
            foreach (Vector3 q in list)
                if ((q - p).sqrMagnitude < (2f * r + 0.02f) * (2f * r + 0.02f)) return;
            list.Add(p);
        }
    }
}
