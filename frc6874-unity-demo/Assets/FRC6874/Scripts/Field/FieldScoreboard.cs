using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// A venue video board showing the live alliance totals, the match clock (mm:ss) and the
    /// current phase from <see cref="MatchState"/>. Red is always on the viewer's left. Text is
    /// rebuilt only when a shown value changes, and an alliance's panel flashes when it scores.
    /// </summary>
    public class FieldScoreboard : MonoBehaviour
    {
        static readonly Color ClockColor = new Color(1f, 0.9f, 0.35f);
        static readonly Color EndgameClockColor = new Color(1f, 0.45f, 0.2f);
        const float FlashDuration = 0.6f;

        TextMesh redScore, blueScore, clock, phase;
        Material redPanel, bluePanel;
        Color redPanelColor, bluePanelColor;
        float redFlash, blueFlash;
        bool panelsDirty = true;

        int shownRed = -1, shownBlue = -1, shownStateKey = int.MinValue;

        /// <summary>
        /// Builds a board of <paramref name="width"/> × <paramref name="height"/> meters centered at
        /// <paramref name="position"/> (parent space), whose screen faces <paramref name="facing"/>.
        /// </summary>
        public static FieldScoreboard Create(string name, Transform parent, Vector3 position, Vector3 facing,
            float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(-facing.normalized);
            var board = go.AddComponent<FieldScoreboard>();
            board.Build(width, height);
            return board;
        }

        /// <summary>Clock text for a number of seconds, e.g. 135 → "2:15".</summary>
        public static string FormatClock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60).ToString() + ":" + (s % 60).ToString("00");
        }

        void Build(float w, float h)
        {
            // Local frame: the screen is the z = 0 plane and faces local -Z (TextFactory's reading side).
            Transform t = transform;
            FieldKit.Box("Housing", t, new Vector3(0f, 0f, 0.2f), new Vector3(w + 0.3f, h + 0.3f, 0.4f), FieldKit.Charcoal);
            FieldKit.Box("Screen", t, new Vector3(0f, 0f, -0.005f), new Vector3(w, h, 0.01f),
                MaterialFactory.Lit(new Color(0.02f, 0.022f, 0.03f), 0.7f), shadows: false);
            FieldKit.Box("Accent Strip", t, new Vector3(0f, -h * 0.5f - 0.1f, -0.01f), new Vector3(w + 0.3f, 0.06f, 0.02f),
                MaterialFactory.Emissive(GameConfig.TeamAccentColor, 1.2f), shadows: false);

            redPanelColor = FieldKit.Mix(GameConfig.RedAllianceColor, Color.black, 0.3f);
            bluePanelColor = FieldKit.Mix(GameConfig.BlueAllianceColor, Color.black, 0.3f);
            redPanel = MaterialFactory.NewLitInstance(redPanelColor, 0.5f);
            bluePanel = MaterialFactory.NewLitInstance(bluePanelColor, 0.5f);

            float panelW = w * 0.29f, panelH = h * 0.62f, panelX = w * 0.33f, panelY = -h * 0.04f;
            FieldKit.Box("Red Panel", t, new Vector3(-panelX, panelY, -0.02f), new Vector3(panelW, panelH, 0.02f), redPanel,
                shadows: false);
            FieldKit.Box("Blue Panel", t, new Vector3(panelX, panelY, -0.02f), new Vector3(panelW, panelH, 0.02f), bluePanel,
                shadows: false);

            float textZ = -0.05f;
            Color dimWhite = new Color(1f, 1f, 1f, 0.85f);
            Text(GameConfig.GameTitle, new Vector3(0f, h * 0.5f - h * 0.1f, textZ), h * 0.075f, GameConfig.TeamAccentColor);
            Text("RED", new Vector3(-panelX, panelY + panelH * 0.36f, textZ), h * 0.075f, dimWhite);
            Text("BLUE", new Vector3(panelX, panelY + panelH * 0.36f, textZ), h * 0.075f, dimWhite);
            redScore = Text("0", new Vector3(-panelX, panelY - panelH * 0.08f, textZ), h * 0.3f, Color.white);
            blueScore = Text("0", new Vector3(panelX, panelY - panelH * 0.08f, textZ), h * 0.3f, Color.white);
            clock = Text("--:--", new Vector3(0f, h * 0.05f, textZ), h * 0.22f, ClockColor);
            phase = Text("", new Vector3(0f, -h * 0.2f, textZ), h * 0.055f, Color.white);
            Text("FIRST ROBOTICS COMPETITION-STYLE DEMO", new Vector3(0f, -h * 0.5f + h * 0.08f, textZ), h * 0.045f,
                new Color(1f, 1f, 1f, 0.6f));
            Refresh(true);
        }

        TextMesh Text(string text, Vector3 position, float height, Color color) =>
            TextFactory.Create(text, transform, position, Quaternion.identity, height, color);

        void OnEnable()
        {
            MatchState.Scored += OnScored;
        }

        void OnDisable()
        {
            MatchState.Scored -= OnScored;
        }

        void OnScored(ScoreEvent e)
        {
            if (e.Alliance == Alliance.Red) redFlash = 1f;
            else blueFlash = 1f;
            panelsDirty = true;
        }

        void Update()
        {
            Refresh(false);
            AnimatePanels();
        }

        void Refresh(bool force)
        {
            if (redScore == null) return;
            int red = MatchState.Red.Total;
            int blue = MatchState.Blue.Total;
            if (force || red != shownRed)
            {
                shownRed = red;
                redScore.text = red.ToString();
            }
            if (force || blue != shownBlue)
            {
                shownBlue = blue;
                blueScore.text = blue.ToString();
            }

            MatchPhase p = MatchState.Phase;
            bool timed = p == MatchPhase.Countdown || p == MatchPhase.Auto || p == MatchPhase.Transition || p == MatchPhase.Teleop;
            int seconds = timed ? Mathf.Max(0, Mathf.CeilToInt(MatchState.TimeRemaining)) : 0;
            int lead = red > blue ? 1 : red < blue ? 2 : 0;
            int key = (((int)p * 4 + (MatchState.AttractMode ? 1 : 0) + (MatchState.IsEndgame ? 2 : 0)) * 4 + lead) * 10000 + seconds;
            if (!force && key == shownStateKey) return;
            shownStateKey = key;

            clock.text = timed ? FormatClock(seconds) : p == MatchPhase.PostMatch ? "0:00" : "--:--";
            clock.color = MatchState.IsEndgame ? EndgameClockColor : ClockColor;
            phase.text = PhaseLabel(p, lead);
        }

        static string PhaseLabel(MatchPhase p, int lead)
        {
            switch (p)
            {
                case MatchPhase.Menu: return MatchState.AttractMode ? "DEMO MATCH" : "WELCOME";
                case MatchPhase.Countdown: return "MATCH STARTING";
                case MatchPhase.Auto: return "AUTONOMOUS";
                case MatchPhase.Transition: return "AUTO COMPLETE";
                case MatchPhase.Teleop: return MatchState.IsEndgame ? "ENDGAME - CLIMB!" : "TELEOP";
                case MatchPhase.PostMatch: return lead == 1 ? "FINAL - RED WINS" : lead == 2 ? "FINAL - BLUE WINS" : "FINAL - TIE";
                case MatchPhase.Practice: return "PRACTICE";
            }
            return "";
        }

        void AnimatePanels()
        {
            float dt = Time.deltaTime / FlashDuration;
            if (redFlash > 0f) { redFlash = Mathf.Max(0f, redFlash - dt); panelsDirty = true; }
            if (blueFlash > 0f) { blueFlash = Mathf.Max(0f, blueFlash - dt); panelsDirty = true; }
            if (!panelsDirty) return;
            panelsDirty = false;
            SetPanel(redPanel, redPanelColor, redFlash);
            SetPanel(bluePanel, bluePanelColor, blueFlash);
        }

        static void SetPanel(Material material, Color color, float flash)
        {
            if (material == null) return;
            MaterialFactory.SetEmission(material, color * (0.55f + 1.6f * flash * flash));
        }
    }
}
