using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Builds the field (carpet, perimeter, alliance walls, hub, climb towers, markings) and the
    /// venue around it, matching the layout in <see cref="Arena"/>. The parent should sit at the
    /// world origin, because robots, AI and the hub work in Arena (world) coordinates.
    /// </summary>
    public static class FieldBuilder
    {
        static GameObject current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
        }

        /// <summary>The root of the most recently built field (null before <see cref="Build"/>).</summary>
        public static GameObject Root => current;

        /// <summary>Builds the whole field and venue under <paramref name="parent"/>. Called once at startup; calling it again replaces the old field.</summary>
        public static GameObject Build(Transform parent)
        {
            if (current != null)
            {
                current.SetActive(false);   // colliders and the old hub stop working immediately
                Object.Destroy(current);
                current = null;
            }

            var root = new GameObject("Field");
            root.transform.SetParent(parent, false);
            Transform t = root.transform;

            FieldPerimeter.Build(t);
            FieldMarkings.Build(t);
            Hub.Create(t);
            ClimbTower.Create(t, Alliance.Red);
            ClimbTower.Create(t, Alliance.Blue);
            VenueBuilder.Build(t);

            current = root;
            return root;
        }

        /// <summary>Despawns every ball (GamePiece.DespawnAll), empties the hub and lays out the match-start configuration.</summary>
        public static void ResetGamePieces()
        {
            GamePiece.DespawnAll();
            if (Hub.Instance != null) Hub.Instance.ClearPending();
            FuelLayout.SpawnMatchStart();
        }
    }
}
