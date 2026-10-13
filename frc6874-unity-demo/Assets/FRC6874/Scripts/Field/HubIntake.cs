using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Sits on the hub's scoring trigger and hands every ball that touches it to the <see cref="Hub"/>.
    /// The balls carry the Rigidbody, so this static trigger receives the callbacks. OnTriggerStay
    /// is a backstop for a ball that comes to rest inside the funnel without an enter event.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HubIntake : MonoBehaviour
    {
        /// <summary>The hub that owns this trigger.</summary>
        public Hub Hub;

        void OnTriggerEnter(Collider other) => Consume(other);

        void OnTriggerStay(Collider other) => Consume(other);

        void Consume(Collider other)
        {
            if (Hub == null || other == null) return;
            GamePiece piece = other.GetComponent<GamePiece>();
            if (piece != null && piece.State != PieceState.Pooled) Hub.Accept(piece);
        }
    }
}
