using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// CONTRACT STUB — owned by the Field agent. Keep these public signatures; fill in the bodies.
    /// Builds the field (carpet, perimeter, alliance walls, hub, climb towers, markings) and the
    /// venue around it, matching the layout in <see cref="Arena"/>.
    /// </summary>
    public static class FieldBuilder
    {
        /// <summary>Builds the whole field and venue under <paramref name="parent"/>. Called once at startup.</summary>
        public static GameObject Build(Transform parent)
        {
            var root = new GameObject("Field");
            root.transform.SetParent(parent, false);
            return root;
        }

        /// <summary>Despawns every ball (GamePiece.DespawnAll) and lays out the match-start configuration.</summary>
        public static void ResetGamePieces()
        {
            GamePiece.DespawnAll();
        }
    }
}
