using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Makes the (combined-mesh) crowd sway gently and jump when anything scores or a match
    /// period starts or ends. Each registered group bobs with its own phase, so the stands ripple.
    /// </summary>
    public class CrowdCheer : MonoBehaviour
    {
        const float IdleAmplitude = 0.012f;
        const float CheerAmplitude = 0.11f;
        const float CalmDownRate = 0.45f;   // excitement per second

        readonly List<Transform> groups = new List<Transform>();
        readonly List<Vector3> basePositions = new List<Vector3>();
        readonly List<float> phases = new List<float>();
        float excitement;

        /// <summary>Registers a crowd group transform to animate.</summary>
        public void Add(Transform group, float phase)
        {
            if (group == null) return;
            groups.Add(group);
            basePositions.Add(group.localPosition);
            phases.Add(phase);
        }

        /// <summary>Gets the crowd going (0..1).</summary>
        public void Cheer(float amount)
        {
            excitement = Mathf.Clamp01(Mathf.Max(excitement, 0f) + amount);
        }

        void OnEnable()
        {
            MatchState.Scored += OnScored;
            MatchState.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            MatchState.Scored -= OnScored;
            MatchState.PhaseChanged -= OnPhaseChanged;
        }

        void OnScored(ScoreEvent e) => Cheer(e.Kind == ScoreKind.Climb ? 1f : 0.45f);

        void OnPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            if (next == MatchPhase.Auto || next == MatchPhase.Teleop || next == MatchPhase.PostMatch) Cheer(1f);
        }

        void Update()
        {
            excitement = Mathf.Max(0f, excitement - CalmDownRate * Time.deltaTime);
            float t = Time.time;
            float cheer = excitement * excitement * CheerAmplitude;
            for (int i = 0; i < groups.Count; i++)
            {
                Transform g = groups[i];
                if (g == null) continue;
                float phase = phases[i];
                float offset = IdleAmplitude * Mathf.Sin(t * 1.3f + phase) + cheer * Mathf.Abs(Mathf.Sin(t * 8.5f + phase * 3f));
                g.localPosition = basePositions[i] + Vector3.up * offset;
            }
        }
    }
}
