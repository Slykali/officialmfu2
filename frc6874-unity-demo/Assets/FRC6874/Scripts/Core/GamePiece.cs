using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    public enum PieceState
    {
        /// <summary>Inactive in the pool.</summary>
        Pooled,
        /// <summary>On the field, can be picked up.</summary>
        Free,
        /// <summary>Launched by a robot and has not touched the carpet yet. Only these can score.</summary>
        InFlight,
    }

    /// <summary>
    /// A "fuel" ball. Balls are pooled: use <see cref="Spawn"/> to put one on the field and
    /// <see cref="Despawn"/> when a robot collects it or the hub swallows it. Robots store
    /// collected fuel as a count, not as objects.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class GamePiece : MonoBehaviour
    {
        /// <summary>Every ball currently on the field (Free or InFlight). Do not modify.</summary>
        public static readonly List<GamePiece> Active = new List<GamePiece>();

        static readonly Stack<GamePiece> pool = new Stack<GamePiece>();
        static Transform container;

        public Rigidbody Body { get; private set; }
        public SphereCollider Collider { get; private set; }
        public PieceState State { get; private set; } = PieceState.Pooled;

        /// <summary>Alliance of the robot that launched this ball. Only meaningful while InFlight.</summary>
        public Alliance ShotBy { get; private set; }
        /// <summary>Label of the robot that launched this ball (e.g. "6874"). Only meaningful while InFlight.</summary>
        public string ShooterLabel { get; private set; }
        public float LaunchTime { get; private set; }
        public float SpawnTime { get; private set; }

        /// <summary>True if a robot may intake this ball right now.</summary>
        public bool IsCollectable =>
            State == PieceState.Free ? Time.time - SpawnTime > 0.15f :
            State == PieceState.InFlight && Time.time - LaunchTime > 1.0f;

        TrailRenderer trail;
        Collider[] ignoredColliders;
        float restoreCollisionTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Supports "Enter Play Mode Options" with domain reload disabled.
            Active.Clear();
            pool.Clear();
            container = null;
        }

        /// <summary>Parent transform for all balls (created on demand).</summary>
        public static Transform Container
        {
            get
            {
                if (container == null) container = new GameObject("Game Pieces").transform;
                return container;
            }
        }

        /// <summary>Places a free ball on the field.</summary>
        public static GamePiece Spawn(Vector3 position, Vector3 velocity = default)
        {
            GamePiece piece = null;
            while (pool.Count > 0 && piece == null) piece = pool.Pop();
            if (piece == null) piece = Create();

            piece.transform.position = position;
            piece.transform.rotation = Random.rotation;
            piece.gameObject.SetActive(true);
            piece.Body.SetVelocity(velocity);
            piece.Body.angularVelocity = Vector3.zero;
            piece.State = PieceState.Free;
            piece.SpawnTime = Time.time;
            piece.ShooterLabel = null;
            piece.SetTrail(false);
            Active.Add(piece);
            return piece;
        }

        /// <summary>
        /// Turns this (already spawned) ball into a shot. Collisions with <paramref name="ignoreColliders"/>
        /// (the shooter's own colliders) are disabled briefly so the ball can leave the robot.
        /// </summary>
        public void Launch(Vector3 velocity, Alliance alliance, string shooterLabel, Collider[] ignoreColliders = null)
        {
            if (State == PieceState.Pooled) return;
            State = PieceState.InFlight;
            ShotBy = alliance;
            ShooterLabel = shooterLabel;
            LaunchTime = Time.time;
            Body.SetVelocity(velocity);
            Body.angularVelocity = Random.insideUnitSphere * 10f;
            SetIgnored(ignoreColliders);
            SetTrail(true);
        }

        public void Despawn()
        {
            if (State == PieceState.Pooled) return;
            State = PieceState.Pooled;
            SetIgnored(null);
            Active.Remove(this);
            gameObject.SetActive(false);
            pool.Push(this);
        }

        public static void DespawnAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i] != null) Active[i].Despawn();
                else Active.RemoveAt(i);
            }
        }

        static GamePiece Create()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Fuel";
            go.transform.SetParent(Container, false);
            float diameter = GameConfig.FuelRadius * 2f;
            go.transform.localScale = new Vector3(diameter, diameter, diameter);
            go.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Lit(GameConfig.FuelColor, 0.15f);

            var body = go.AddComponent<Rigidbody>();
            body.mass = GameConfig.FuelMass;
            body.SetDamping(0.05f, 1.5f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 50f;

            var collider = go.GetComponent<SphereCollider>();
            PhysicsCompat.SetSurface(collider, 0.45f, 0.6f);

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = diameter * 0.8f;
            trail.endWidth = 0f;
            trail.sharedMaterial = MaterialFactory.Unlit(new Color(1f, 0.9f, 0.3f, 0.6f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            var piece = go.AddComponent<GamePiece>();
            piece.Body = body;
            piece.Collider = collider;
            piece.trail = trail;
            go.SetActive(false);
            return piece;
        }

        void SetTrail(bool on)
        {
            if (trail == null) return;
            trail.Clear();
            trail.emitting = on;
        }

        void SetIgnored(Collider[] colliders)
        {
            if (ignoredColliders != null)
                foreach (Collider c in ignoredColliders)
                    if (c != null && Collider != null) Physics.IgnoreCollision(Collider, c, false);
            ignoredColliders = colliders;
            if (ignoredColliders != null)
            {
                foreach (Collider c in ignoredColliders)
                    if (c != null && Collider != null) Physics.IgnoreCollision(Collider, c, true);
                restoreCollisionTime = Time.time + 0.35f;
            }
        }

        void FixedUpdate()
        {
            if (State == PieceState.Pooled) return;

            if (ignoredColliders != null && Time.time >= restoreCollisionTime) SetIgnored(null);

            Vector3 p = transform.position;
            bool nearCarpet = p.y <= GameConfig.FuelRadius + 0.03f;

            if (State == PieceState.InFlight && Time.time - LaunchTime > 0.2f && (nearCarpet || Time.time - LaunchTime > 6f))
            {
                State = PieceState.Free;
                SpawnTime = Time.time - 1f;
                ShooterLabel = null;
                if (trail != null) trail.emitting = false;
            }

            if (State == PieceState.Free && nearCarpet)
            {
                // Rolling resistance so balls on the carpet settle instead of rolling forever.
                Vector3 v = Body.GetVelocity();
                float k = Mathf.Clamp01(1.2f * Time.fixedDeltaTime);
                Body.SetVelocity(new Vector3(v.x * (1f - k), v.y, v.z * (1f - k)));
            }

            // Safety net: a ball that escaped the field comes back near where it left.
            if (p.y < -2f || !Arena.IsInsideField(p, -1.0f))
            {
                transform.position = Arena.ClampToField(p, 0.5f) + Vector3.up * 0.5f;
                Body.SetVelocity(Vector3.zero);
                if (State == PieceState.InFlight)
                {
                    State = PieceState.Free;
                    SpawnTime = Time.time;
                    if (trail != null) trail.emitting = false;
                }
            }
        }

        void OnDestroy()
        {
            Active.Remove(this);
        }
    }
}
