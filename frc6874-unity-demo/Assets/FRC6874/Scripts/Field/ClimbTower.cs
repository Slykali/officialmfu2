using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// An alliance's climbing tower at <see cref="Arena.ClimbAnchor"/>, flat against the alliance wall
    /// and as wide as the climb zone (three robots side by side). Robots "hook" onto the rung at
    /// <see cref="BarHeight"/>. Only the two uprights have colliders, and they stand just outside the
    /// zone's sides, so robots can drive all the way in. The rung lights pulse while climbing is allowed.
    /// </summary>
    public class ClimbTower : MonoBehaviour
    {
        /// <summary>Height of the climbing rung's center above the carpet.</summary>
        public const float BarHeight = 1.35f;
        /// <summary>Distance of the rung's center from the alliance wall's inner face, into the field.</summary>
        public const float BarDepth = 0.2f;
        /// <summary>Deepest point of the tower structure into the field.</summary>
        public const float StructureDepth = 0.25f;

        const float TowerHeight = 2.0f;
        const float UprightSize = 0.1f;
        const float RungRadius = 0.035f;

        /// <summary>Alliance this tower belongs to.</summary>
        public Alliance Alliance { get; private set; }

        Material lightMaterial;
        Color baseColor;
        float shownLevel = -1f;

        /// <summary>World-space center of the climbing rung for an alliance.</summary>
        public static Vector3 BarCenter(Alliance alliance) =>
            Arena.ClimbAnchor(alliance) + Arena.AllianceForward(alliance) * BarDepth + Vector3.up * BarHeight;

        /// <summary>Builds the tower for <paramref name="alliance"/> under <paramref name="parent"/>.</summary>
        public static ClimbTower Create(Transform parent, Alliance alliance)
        {
            var go = new GameObject(alliance.DisplayName() + " Climb Tower");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Arena.ClimbAnchor(alliance);
            // Local frame: +Z into the field, X along the wall, the wall's inner face at z = 0.
            go.transform.localRotation = Quaternion.LookRotation(Arena.AllianceForward(alliance));
            var tower = go.AddComponent<ClimbTower>();
            tower.Alliance = alliance;
            tower.Build();
            return tower;
        }

        void Build()
        {
            Color color = Alliance.ToColor();
            baseColor = color;
            Material allianceMat = MaterialFactory.Lit(color, 0.4f);
            Material aluminum = FieldKit.Aluminum;
            Material hookMat = MaterialFactory.Lit(GameConfig.FuelColor, 0.4f);
            Transform t = transform;

            float zoneWidth = GameConfig.ClimbZoneZMax - GameConfig.ClimbZoneZMin;
            float halfSpan = zoneWidth * 0.5f + UprightSize * 0.5f + 0.01f;   // uprights stand just outside the zone
            float uprightZ = UprightSize * 0.5f + 0.02f;

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * halfSpan;
                FieldKit.Box("Upright", t, new Vector3(x, TowerHeight * 0.5f, uprightZ),
                    new Vector3(UprightSize, TowerHeight, UprightSize), allianceMat, collider: true);
                FieldKit.Box("Foot", t, new Vector3(x, 0.01f, uprightZ + 0.02f), new Vector3(0.22f, 0.02f, 0.2f),
                    FieldKit.DarkAluminum, shadows: false);
                FieldKit.Box("Brace", t, new Vector3(x, TowerHeight * 0.5f, 0.01f), new Vector3(0.04f, TowerHeight, 0.02f),
                    aluminum);
            }

            // Climbing rung, an upper rung and a top beam.
            float span = halfSpan * 2f + UprightSize;
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
            FieldKit.Cylinder("Rung", t, new Vector3(0f, BarHeight, BarDepth), alongX, RungRadius, span, aluminum);
            FieldKit.Cylinder("Upper Rung", t, new Vector3(0f, 1.8f, BarDepth - 0.04f), alongX, RungRadius * 0.8f, span,
                aluminum);
            FieldKit.Box("Top Beam", t, new Vector3(0f, TowerHeight - 0.04f, uprightZ),
                new Vector3(span, 0.08f, UprightSize), allianceMat);
            for (int side = -1; side <= 1; side += 2)
            {
                // Short arms that carry the rungs out from the uprights.
                FieldKit.Box("Rung Arm", t, new Vector3(side * halfSpan, BarHeight, (uprightZ + BarDepth) * 0.5f + 0.02f),
                    new Vector3(UprightSize * 0.8f, 0.08f, BarDepth - uprightZ + 0.06f), aluminum);
            }

            // Yellow hook targets, one per robot slot.
            float slot = zoneWidth / 3f;
            for (int i = -1; i <= 1; i++)
            {
                FieldKit.Cylinder("Hook Target", t, new Vector3(i * slot, BarHeight, BarDepth), alongX, RungRadius + 0.012f,
                    0.18f, hookMat, shadows: false);
            }

            // Light strip on the top beam (animated) and the CLIMB sign above it.
            lightMaterial = MaterialFactory.NewLitInstance(color, 0.6f);
            FieldKit.Box("Light Strip", t, new Vector3(0f, TowerHeight - 0.04f, uprightZ + UprightSize * 0.5f + 0.006f),
                new Vector3(span - 0.3f, 0.04f, 0.012f), lightMaterial, shadows: false);
            Vector3 signCenter = new Vector3(0f, TowerHeight + 0.22f, uprightZ);
            FieldKit.Box("Sign", t, signCenter, new Vector3(1.3f, 0.36f, 0.05f),
                MaterialFactory.Lit(FieldKit.Mix(color, Color.black, 0.35f), 0.35f));
            FieldKit.Box("Sign Post", t, new Vector3(0f, TowerHeight + 0.02f, uprightZ), new Vector3(0.08f, 0.06f, 0.06f), aluminum);
            FieldKit.Text("CLIMB", t, signCenter + new Vector3(0f, 0f, 0.025f), Vector3.forward, 0.2f, Color.white);
            FieldKit.Text("CLIMB", t, signCenter - new Vector3(0f, 0f, 0.025f), Vector3.back, 0.2f, Color.white);

            ApplyLight(0.35f);
        }

        void Update()
        {
            float level = MatchState.ClimbAllowed ? 1.1f + 0.7f * Mathf.Sin(Time.time * 6f) : 0.35f;
            if (Mathf.Abs(level - shownLevel) > 0.01f) ApplyLight(level);
        }

        void ApplyLight(float level)
        {
            shownLevel = level;
            if (lightMaterial == null) return;
            MaterialFactory.SetEmission(lightMaterial, baseColor * level);
        }
    }
}
