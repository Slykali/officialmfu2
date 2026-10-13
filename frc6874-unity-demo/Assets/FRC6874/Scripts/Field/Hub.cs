using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// The shared goal at <see cref="Arena.HubCenter"/>. A solid drum (radius
    /// <see cref="GameConfig.HubBaseRadius"/>) carries a funnel whose rim sits at
    /// <see cref="GameConfig.HubOpeningHeight"/> with radius <see cref="GameConfig.HubOpeningRadius"/>.
    /// A trigger inside the funnel swallows every ball that drops in: shots (InFlight) are scored
    /// through <see cref="MatchState.Award"/>, loose balls are just swallowed. Each swallowed ball is
    /// queued and rolled back out of one of four exit ports after <see cref="GameConfig.HubEjectDelay"/>,
    /// so no ball is ever lost. Scoring flashes the light rings in the scoring alliance's color.
    /// </summary>
    public class Hub : MonoBehaviour
    {
        /// <summary>The hub in the scene (null before the field is built).</summary>
        public static Hub Instance { get; private set; }

        // ---- Geometry (meters, relative to the hub center at carpet level) -----------------
        const float BaseTop = 1.15f;
        const float ShoulderTop = 1.40f;
        const float ShoulderRadius = 0.45f;
        const float NeckRadius = 0.42f;
        const float NeckBottom = 1.30f;
        const float NeckTop = 1.62f;
        const float FunnelBottomY = 1.60f;
        const float FunnelBottomRadius = 0.36f;
        const float FunnelThickness = 0.04f;
        const int FunnelSegments = 16;
        const int RimSegments = 16;
        const float RimThickness = 0.05f;
        const float RimHeight = 0.06f;

        // ---- Ball return ---------------------------------------------------------------------
        const float EjectHeight = 0.3f;
        const float EjectInterval = 0.07f;
        const float RetryDelay = 0.1f;
        const float MaxEjectWait = 1.5f;
        const float EjectSpreadDegrees = 8f;
        static readonly float[] PortAngles = { 45f, 135f, 225f, 315f };

        // ---- Feedback ------------------------------------------------------------------------
        static readonly Color IdleLightColor = new Color(0.95f, 0.88f, 0.70f);
        const float IdleLightIntensity = 0.55f;
        const float FlashLightIntensity = 3.2f;
        const float FlashDuration = 0.75f;
        const float PulseDuration = 0.45f;

        /// <summary>Balls currently inside the hub waiting to be returned to the field.</summary>
        public int BallsInside => pending.Count;

        /// <summary>Shots swallowed since the last <see cref="ClearPending"/> (counts in every phase, including attract mode).</summary>
        public int FuelScored { get; private set; }

        readonly Queue<float> pending = new Queue<float>();
        float nextEjectTime;
        int lastPort = -1;

        Material lightMaterial;
        Color flashColor = Color.white;
        float flash;
        bool lightDirty = true;

        Transform pulse;
        MeshRenderer pulseRenderer;
        Mesh pulseMesh;
        Color[] pulseColors;
        float pulseAge = -1f;

        readonly List<TextMesh> counterTexts = new List<TextMesh>();
        int shownCount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>Builds the hub (visuals, colliders, scoring trigger) under <paramref name="parent"/>.</summary>
        public static Hub Create(Transform parent)
        {
            var go = new GameObject("Hub");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Arena.HubCenter;
            var hub = go.AddComponent<Hub>();
            hub.Build();
            return hub;
        }

        /// <summary>Drops every queued ball (used when the field is reset for a new match) and zeroes the counter.</summary>
        public void ClearPending()
        {
            pending.Clear();
            FuelScored = 0;
            nextEjectTime = 0f;
        }

        /// <summary>
        /// Swallows a ball that dropped into the opening. Called by <see cref="HubIntake"/>.
        /// Safe to call several times for the same ball.
        /// </summary>
        public void Accept(GamePiece piece)
        {
            if (piece == null || piece.State == PieceState.Pooled) return;

            Vector3 position = piece.transform.position;
            bool scored = piece.State == PieceState.InFlight;
            Alliance alliance = piece.ShotBy;
            string shooter = piece.ShooterLabel;

            // Swallow and queue first so the ball is never lost, whatever happens below.
            piece.Despawn();
            float due = Time.time + GameConfig.HubEjectDelay;
            pending.Enqueue(due);

            if (!scored) return;
            FuelScored++;
            Flash(alliance.ToColor());
            GameAudio.Play(GameSound.Score, position);
            MatchState.Award(alliance, ScoreKind.Fuel, position, shooter);
        }

        /// <summary>Flashes the hub lights in <paramref name="color"/> (also used for scoring feedback).</summary>
        public void Flash(Color color)
        {
            flashColor = color;
            flash = 1f;
            lightDirty = true;
            pulseAge = 0f;
            if (pulseRenderer != null) pulseRenderer.enabled = true;
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            ProcessQueue();
            AnimateLights();
            AnimatePulse();
            UpdateCounter();
        }

        // ---- Ball return ---------------------------------------------------------------------

        void ProcessQueue()
        {
            float now = Time.time;
            while (pending.Count > 0 && now >= pending.Peek() && now >= nextEjectTime)
            {
                bool force = now - pending.Peek() > MaxEjectWait;
                if (!TryEject(force))
                {
                    nextEjectTime = now + RetryDelay;
                    return;
                }
                pending.Dequeue();
                nextEjectTime = now + EjectInterval;
            }
        }

        bool TryEject(bool force)
        {
            float radius = GameConfig.HubBaseRadius + 0.15f;
            int start = Random.Range(0, PortAngles.Length);
            int fallback = -1;
            for (int i = 0; i < PortAngles.Length; i++)
            {
                int port = (start + i) % PortAngles.Length;
                Vector3 pos = PortPosition(port, radius, EjectHeight);
                if (Physics.CheckSphere(pos, GameConfig.FuelRadius, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (port == lastPort && PortAngles.Length > 1)
                {
                    fallback = port;   // prefer a different port than last time
                    continue;
                }
                Eject(port, pos);
                return true;
            }
            if (fallback >= 0)
            {
                Eject(fallback, PortPosition(fallback, radius, EjectHeight));
                return true;
            }
            if (!force) return false;

            // Every port is blocked (robots parked against the hub) for too long: release anyway, a bit higher.
            int forced = Random.Range(0, PortAngles.Length);
            Eject(forced, PortPosition(forced, radius, EjectHeight + 0.25f));
            return true;
        }

        void Eject(int port, Vector3 position)
        {
            lastPort = port;
            Vector3 dir = PortDirection(port);
            dir = Quaternion.Euler(0f, Random.Range(-EjectSpreadDegrees, EjectSpreadDegrees), 0f) * dir;
            GamePiece.Spawn(position, dir * GameConfig.HubEjectSpeed);
        }

        static Vector3 PortDirection(int port)
        {
            float a = PortAngles[port] * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        Vector3 PortPosition(int port, float radius, float height) =>
            transform.position + PortDirection(port) * radius + Vector3.up * height;

        // ---- Feedback ------------------------------------------------------------------------

        void AnimateLights()
        {
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - Time.deltaTime / FlashDuration);
                lightDirty = true;
            }
            if (!lightDirty || lightMaterial == null) return;
            lightDirty = false;

            float k = flash * flash;
            lightMaterial.color = Color.Lerp(IdleLightColor, flashColor, Mathf.Clamp01(flash * 1.5f));
            MaterialFactory.SetEmission(lightMaterial,
                Color.Lerp(IdleLightColor * IdleLightIntensity, flashColor * FlashLightIntensity, k));
        }

        void AnimatePulse()
        {
            if (pulseAge < 0f || pulse == null) return;
            pulseAge += Time.deltaTime;
            float u = pulseAge / PulseDuration;
            if (u >= 1f)
            {
                pulseAge = -1f;
                pulseRenderer.enabled = false;
                return;
            }
            float scale = 1f + 0.9f * u;
            pulse.localScale = new Vector3(scale, 1f, scale);
            Color c = flashColor;
            c.a = 0.85f * (1f - u) * (1f - u);
            for (int i = 0; i < pulseColors.Length; i++) pulseColors[i] = c;
            pulseMesh.colors = pulseColors;
        }

        void UpdateCounter()
        {
            if (FuelScored == shownCount) return;
            shownCount = FuelScored;
            string text = shownCount.ToString();
            foreach (TextMesh mesh in counterTexts)
                if (mesh != null) mesh.text = text;
        }

        // ---- Construction ----------------------------------------------------------------------

        void Build()
        {
            float baseR = GameConfig.HubBaseRadius;
            float openR = GameConfig.HubOpeningRadius;
            float openY = GameConfig.HubOpeningHeight;
            Transform visuals = FieldKit.Group("Visuals", transform);
            Transform solids = FieldKit.Group("Colliders", transform);
            var b = new FieldMeshBuilder();

            Material charcoal = FieldKit.Charcoal;
            Material aluminum = FieldKit.Aluminum;
            Material interior = MaterialFactory.Lit(new Color(0.08f, 0.085f, 0.095f), 0.2f);
            lightMaterial = MaterialFactory.NewLitInstance(IdleLightColor, 0.7f);
            lightDirty = true;

            // Drum, shoulder and neck.
            b.AddFrustum(48, baseR, 0f, baseR, BaseTop, true);
            FieldKit.MeshObject("Drum", visuals, b.Build("Hub Drum"), charcoal, Vector3.zero);
            b.Clear();
            b.AddFrustum(48, baseR, BaseTop, ShoulderRadius, ShoulderTop, true);
            FieldKit.MeshObject("Shoulder", visuals, b.Build("Hub Shoulder"), aluminum, Vector3.zero);
            b.Clear();
            b.AddCylinder(32, NeckRadius, NeckBottom, NeckTop);
            FieldKit.MeshObject("Neck", visuals, b.Build("Hub Neck"), charcoal, Vector3.zero);
            b.Clear();
            b.AddTube(48, baseR - 0.01f, baseR + 0.025f, 0f, 0.14f);
            FieldKit.MeshObject("Bumper Band", visuals, b.Build("Hub Bumper"), FieldKit.Rubber, Vector3.zero);

            // Flared funnel: aluminum outside, dark inside, lip that meets the rim.
            b.Clear();
            b.AddFrustum(48, FunnelBottomRadius + FunnelThickness, FunnelBottomY, openR + FunnelThickness, openY, true);
            FieldKit.MeshObject("Funnel Outer", visuals, b.Build("Hub Funnel Outer"), aluminum, Vector3.zero);
            b.Clear();
            b.AddFrustum(48, FunnelBottomRadius, FunnelBottomY + 0.005f, openR, openY, false);
            b.AddAnnulus(32, 0f, NeckRadius, NeckTop + 0.002f, true);
            FieldKit.MeshObject("Funnel Inner", visuals, b.Build("Hub Funnel Inner"), interior, Vector3.zero, shadows: false);
            b.Clear();
            b.AddAnnulus(48, openR - 0.005f, openR + 0.03f, openY, true);
            FieldKit.MeshObject("Funnel Lip", visuals, b.Build("Hub Funnel Lip"), aluminum, Vector3.zero, shadows: false);

            // Light rings (one animated material shared by both).
            b.Clear();
            b.AddTube(48, baseR - 0.01f, baseR + 0.03f, 0.98f, 1.08f);
            FieldKit.MeshObject("Base Light Ring", visuals, b.Build("Hub Light Ring"), lightMaterial, Vector3.zero, shadows: false);
            b.Clear();
            float rimCenter = openR + RimThickness;
            b.AddTube(64, rimCenter - RimThickness * 0.5f, rimCenter + RimThickness * 0.5f,
                openY - RimHeight * 0.5f, openY + RimHeight * 0.5f);
            FieldKit.MeshObject("Rim Light", visuals, b.Build("Hub Rim"), lightMaterial, Vector3.zero, shadows: false);

            BuildPorts(visuals, baseR);
            BuildRibsAndSigns(visuals, baseR);
            BuildPulse(visuals, rimCenter, openY);
            BuildColliders(solids, baseR, openR, openY, rimCenter);
            AnimateLights();
        }

        void BuildPorts(Transform visuals, float baseR)
        {
            Material hole = MaterialFactory.Lit(new Color(0.03f, 0.03f, 0.035f), 0.1f);
            Material hazard = MaterialFactory.Lit(GameConfig.FuelColor, 0.35f);
            for (int i = 0; i < PortAngles.Length; i++)
            {
                Vector3 dir = PortDirection(i);
                Quaternion rot = Quaternion.LookRotation(dir);
                FieldKit.Box("Exit Port", visuals, dir * (baseR - 0.005f) + Vector3.up * 0.2f, rot,
                    new Vector3(0.44f, 0.3f, 0.05f), hole, shadows: false);
                FieldKit.Box("Exit Port Hood", visuals, dir * (baseR + 0.01f) + Vector3.up * 0.375f, rot,
                    new Vector3(0.5f, 0.05f, 0.07f), hazard);
                FieldKit.Box("Exit Port Chute", visuals, dir * (baseR + 0.03f) + Vector3.up * 0.06f, rot,
                    new Vector3(0.44f, 0.03f, 0.12f), FieldKit.DarkAluminum, shadows: false);
            }
        }

        void BuildRibsAndSigns(Transform visuals, float baseR)
        {
            // Aluminum ribs framing each exit port.
            foreach (float port in PortAngles)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float a = (port + side * 26f) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    FieldKit.Box("Rib", visuals, dir * (baseR + 0.005f) + Vector3.up * 0.56f, Quaternion.LookRotation(dir),
                        new Vector3(0.05f, 0.84f, 0.03f), FieldKit.Aluminum);
                }
            }

            // Between the ribs: a scored-fuel counter facing each driver station, team number facing the sides.
            float textR = baseR + 0.012f;
            for (int k = 0; k < 4; k++)
            {
                float a = k * 90f * Mathf.Deg2Rad;
                Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 surface = n * textR;
                bool facesDriver = k % 2 == 0;  // 0° faces +X (blue), 180° faces -X (red)
                if (facesDriver)
                {
                    FieldKit.Text("FUEL", visuals, surface + Vector3.up * 0.8f, n, 0.07f, new Color(1f, 1f, 1f, 0.75f));
                    counterTexts.Add(FieldKit.Text("0", visuals, surface + Vector3.up * 0.56f, n, 0.24f, GameConfig.FuelColor));
                }
                else
                {
                    FieldKit.Text(GameConfig.TeamNumber.ToString(), visuals, surface + Vector3.up * 0.6f, n, 0.16f,
                        GameConfig.TeamAccentColor);
                }
            }
        }

        void BuildPulse(Transform visuals, float rimCenter, float openY)
        {
            var b = new FieldMeshBuilder();
            b.AddAnnulus(48, rimCenter - 0.01f, rimCenter + 0.09f, 0f, true);
            pulseMesh = b.Build("Hub Score Pulse");
            pulseColors = new Color[pulseMesh.vertexCount];
            for (int i = 0; i < pulseColors.Length; i++) pulseColors[i] = new Color(1f, 1f, 1f, 0f);
            pulseMesh.colors = pulseColors;
            pulseRenderer = FieldKit.MeshObject("Score Pulse", visuals, pulseMesh, MaterialFactory.Unlit(Color.white),
                new Vector3(0f, openY + RimHeight * 0.5f + 0.01f, 0f), shadows: false);
            pulseRenderer.receiveShadows = false;
            pulseRenderer.enabled = false;
            pulse = pulseRenderer.transform;
        }

        void BuildColliders(Transform solids, float baseR, float openR, float openY, float rimCenter)
        {
            var b = new FieldMeshBuilder();

            // Solid drum + sloped shoulder (convex: balls that land on it roll off outward).
            b.AddFrustum(32, baseR, 0f, baseR, BaseTop, true);
            b.AddFrustum(32, baseR, BaseTop, ShoulderRadius, ShoulderTop, true);
            AddConvex("Drum Collider", solids, b.Build("Hub Drum Collider"), 0.35f, 0.2f, false);

            // Neck; its top is the floor of the funnel.
            b.Clear();
            b.AddCylinder(24, NeckRadius, NeckBottom, NeckTop);
            AddConvex("Neck Collider", solids, b.Build("Hub Neck Collider"), 0.2f, 0.4f, false);

            // Funnel wall: a ring of slanted plates whose inner faces follow the cone.
            float dr = openR - FunnelBottomRadius;
            float dy = openY - FunnelBottomY;
            float slant = Mathf.Sqrt(dr * dr + dy * dy);
            float plateWidth = 2f * Mathf.PI * (openR + FunnelThickness) / FunnelSegments * 1.15f;
            for (int i = 0; i < FunnelSegments; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / FunnelSegments;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 up = (radial * dr + Vector3.up * dy).normalized;
                Vector3 normal = (radial * dy - Vector3.up * dr).normalized;
                Vector3 mid = radial * ((FunnelBottomRadius + openR) * 0.5f) + Vector3.up * ((FunnelBottomY + openY) * 0.5f)
                              + normal * (FunnelThickness * 0.5f);
                FieldKit.SolidBox("Funnel Plate", solids, mid, Quaternion.LookRotation(normal, up),
                    new Vector3(plateWidth, slant, FunnelThickness), 0.2f, 0.4f);
            }

            // Rim: thin boxes whose inner faces sit just outside the opening radius.
            float segment = 2f * Mathf.PI * rimCenter / RimSegments * 1.12f;
            for (int i = 0; i < RimSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RimSegments;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                FieldKit.SolidBox("Rim Segment", solids, radial * rimCenter + Vector3.up * openY, Quaternion.LookRotation(radial),
                    new Vector3(segment, RimHeight, RimThickness), 0.45f, 0.3f);
            }

            // Scoring trigger: fills the funnel just below the rim. Nothing solid sits above it inside the opening.
            b.Clear();
            b.AddFrustum(24, FunnelBottomRadius - 0.06f, NeckTop + 0.02f, openR - 0.13f, openY - 0.12f, true);
            MeshCollider trigger = AddConvex("Scoring Trigger", solids, b.Build("Hub Scoring Trigger"), 0f, 0f, true);
            trigger.gameObject.AddComponent<HubIntake>().Hub = this;
        }

        static MeshCollider AddConvex(string name, Transform parent, Mesh mesh, float bounce, float friction, bool isTrigger)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            collider.isTrigger = isTrigger;
            if (!isTrigger) PhysicsCompat.SetSurface(collider, bounce, friction);
            return collider;
        }
    }
}
