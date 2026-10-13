using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Shared look for the IMGUI HUD and menus: the palette, generated textures (anti-aliased
    /// rounded panels, circles, a marker triangle, a vignette), cached font styles and small drawing
    /// helpers. One instance per HUD. Call <see cref="EnsureBuilt"/> at the top of OnGUI and
    /// <see cref="Dispose"/> when the HUD goes away. All sizes are virtual 1080p canvas units.
    /// </summary>
    public sealed class HudSkin
    {
        // ---- Palette ---------------------------------------------------------------------------
        public static readonly Color PanelColor = new Color(0.055f, 0.065f, 0.095f, 0.88f);
        public static readonly Color PanelLight = new Color(0.13f, 0.145f, 0.19f, 0.92f);
        public static readonly Color TextColor = new Color(0.96f, 0.97f, 1f, 1f);
        public static readonly Color MutedText = new Color(0.64f, 0.68f, 0.76f, 1f);
        public static readonly Color DarkText = new Color(0.06f, 0.06f, 0.08f, 1f);
        public static readonly Color Amber = new Color(1f, 0.66f, 0.12f, 1f);
        public static readonly Color Warning = new Color(1f, 0.32f, 0.22f, 1f);
        public static readonly Color Good = new Color(0.27f, 0.86f, 0.45f, 1f);
        public static readonly Color ButtonColor = new Color(0.15f, 0.17f, 0.23f, 0.95f);
        public static readonly Color Neutral = new Color(0.30f, 0.32f, 0.38f, 0.95f);

        /// <summary>Team accent (from <see cref="GameConfig.TeamAccentColor"/>), opaque.</summary>
        public static Color Accent => WithAlpha(GameConfig.TeamAccentColor, 1f);

        /// <summary>Readable alliance color for text on dark backgrounds.</summary>
        public static Color AllianceText(Alliance alliance) => Color.Lerp(alliance.ToColor(), Color.white, 0.35f);

        /// <summary>Alliance color for filled panels.</summary>
        public static Color AlliancePanel(Alliance alliance)
        {
            Color c = alliance.ToColor();
            return new Color(c.r * 0.85f, c.g * 0.85f, c.b * 0.85f, 0.93f);
        }

        // ---- Textures and styles ---------------------------------------------------------------
        public Texture2D WhiteTexture { get; private set; }
        public Texture2D CircleTexture { get; private set; }
        public Texture2D RingTexture { get; private set; }
        public Texture2D TriangleTexture { get; private set; }
        /// <summary>Vertical vignette: opaque at the top and bottom edges, clear in the middle.</summary>
        public Texture2D VignetteTexture { get; private set; }

        Texture2D roundedTexture;
        Texture2D roundedSmallTexture;
        GUIStyle roundedStyle;
        GUIStyle roundedSmallStyle;
        readonly Dictionary<int, GUIStyle> fonts = new Dictionary<int, GUIStyle>();
        readonly GUIContent content = new GUIContent();

        /// <summary>Multiplies the alpha of everything drawn through this skin (for fades). Reset it after use.</summary>
        public float Opacity = 1f;

        /// <summary>Creates textures and styles if needed. Must be called from OnGUI.</summary>
        public void EnsureBuilt()
        {
            if (WhiteTexture != null && roundedTexture != null && roundedSmallTexture != null && roundedStyle != null) return;
            Dispose();

            WhiteTexture = MakeTexture(1, 1, (x, y) => 1f);
            roundedTexture = MakeRounded(32, 11f);
            roundedSmallTexture = MakeRounded(16, 6f);
            CircleTexture = MakeTexture(64, 64, (x, y) => Circle(x, y, 64f, 0f));
            RingTexture = MakeTexture(64, 64, (x, y) => Circle(x, y, 64f, 7f));
            TriangleTexture = MakeTexture(32, 24, Triangle);
            VignetteTexture = MakeTexture(2, 128, (x, y) =>
            {
                float v = Mathf.Abs(y / 128f * 2f - 1f);
                return v * v * v;
            }, 1);

            roundedStyle = new GUIStyle { border = new RectOffset(12, 12, 12, 12) };
            roundedStyle.normal.background = roundedTexture;
            roundedSmallStyle = new GUIStyle { border = new RectOffset(7, 7, 7, 7) };
            roundedSmallStyle.normal.background = roundedSmallTexture;
            fonts.Clear();
        }

        /// <summary>Destroys the generated textures.</summary>
        public void Dispose()
        {
            DestroyTexture(WhiteTexture);
            DestroyTexture(roundedTexture);
            DestroyTexture(roundedSmallTexture);
            DestroyTexture(CircleTexture);
            DestroyTexture(RingTexture);
            DestroyTexture(TriangleTexture);
            DestroyTexture(VignetteTexture);
            WhiteTexture = roundedTexture = roundedSmallTexture = null;
            CircleTexture = RingTexture = TriangleTexture = VignetteTexture = null;
            roundedStyle = roundedSmallStyle = null;
            fonts.Clear();
        }

        /// <summary>Cached label style. Text is white; tint it with the color passed to <see cref="Text"/>.</summary>
        public GUIStyle Font(int size, bool bold = true, TextAnchor anchor = TextAnchor.MiddleCenter, bool wrap = false)
        {
            int key = size * 64 + (int)anchor * 4 + (bold ? 2 : 0) + (wrap ? 1 : 0);
            if (fonts.TryGetValue(key, out GUIStyle style) && style != null) return style;

            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = anchor,
                wordWrap = wrap,
                richText = false,
                clipping = wrap ? TextClipping.Clip : TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            fonts[key] = style;
            return style;
        }

        /// <summary>Largest font size (stepping down by 4) at which <paramref name="text"/> fits <paramref name="maxWidth"/>.</summary>
        public int FitFontSize(string text, int maxSize, float maxWidth, int minSize = 20)
        {
            int size = maxSize;
            while (size > minSize && Measure(text, Font(size)).x > maxWidth) size -= 4;
            return Mathf.Max(size, minSize);
        }

        // ---- Drawing ---------------------------------------------------------------------------

        /// <summary>Solid rectangle.</summary>
        public void Fill(Rect r, Color color) => Image(r, WhiteTexture, color);

        /// <summary>Tinted texture stretched over <paramref name="r"/>.</summary>
        public void Image(Rect r, Texture texture, Color color)
        {
            if (texture == null || Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = Faded(color);
            GUI.DrawTexture(r, texture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        /// <summary>Rounded panel (falls back to a plain rectangle when it is too small for rounded corners).</summary>
        public void Panel(Rect r, Color color)
        {
            float smallest = Mathf.Min(r.width, r.height);
            if (smallest >= 26f) DrawStyle(roundedStyle, r, color);
            else if (smallest >= 14f) DrawStyle(roundedSmallStyle, r, color);
            else Fill(r, color);
        }

        /// <summary>Rounded panel with a thin outline of <paramref name="border"/>.</summary>
        public void OutlinedPanel(Rect r, Color fill, Color border, float thickness = 2f)
        {
            Panel(r, border);
            Panel(Inset(r, thickness), fill);
        }

        /// <summary>Text with an optional soft drop shadow.</summary>
        public void Text(Rect r, string text, GUIStyle style, Color color, float shadow = 2f)
        {
            if (string.IsNullOrEmpty(text)) return;
            EventType type = Event.current.type;
            if (type != EventType.Repaint) return;

            Color old = GUI.color;
            content.text = text;
            if (shadow > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f * color.a * Opacity);
                GUI.Label(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), content, style);
            }
            GUI.color = Faded(color);
            GUI.Label(r, content, style);
            GUI.color = old;
        }

        public Vector2 Measure(string text, GUIStyle style)
        {
            content.text = text ?? "";
            return style.CalcSize(content);
        }

        public float MeasureHeight(string text, GUIStyle style, float width)
        {
            content.text = text ?? "";
            return style.CalcHeight(content, width);
        }

        /// <summary>
        /// Rounded button with hover feedback and an accent focus ring. Returns true when clicked with
        /// the mouse (and plays the UI click).
        /// </summary>
        public bool Button(Rect r, string label, Color color, bool focused, int fontSize = 26)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            if (focused) Panel(Expand(r, 3f), Accent);
            Color fill = hover ? Lighten(color, 0.14f) : color;
            Panel(r, fill);
            Fill(new Rect(r.x + 14f, r.y + 3f, r.width - 28f, 1f), new Color(1f, 1f, 1f, hover ? 0.16f : 0.08f));
            Text(r, label, Font(fontSize), TextColor, 1.5f);

            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (clicked) GameAudio.Play(GameSound.UIClick);
            return clicked;
        }

        /// <summary>Small rounded tag with centered text; returns its width.</summary>
        public float Pill(float x, float y, string text, int fontSize, Color fill, Color textColor, float height = 30f, float padding = 14f)
        {
            GUIStyle style = Font(fontSize);
            float width = Measure(text, style).x + padding * 2f;
            Rect r = new Rect(x, y, width, height);
            Panel(r, fill);
            Text(r, text, style, textColor, 0f);
            return width;
        }

        // ---- Color and rect helpers -----------------------------------------------------------------

        public static Color WithAlpha(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        public static Color Lighten(Color c, float amount)
        {
            Color l = Color.Lerp(c, Color.white, amount);
            l.a = c.a;
            return l;
        }

        public static Rect Expand(Rect r, float amount) =>
            new Rect(r.x - amount, r.y - amount, r.width + amount * 2f, r.height + amount * 2f);

        public static Rect Inset(Rect r, float amount) => Expand(r, -amount);

        Color Faded(Color c) => new Color(c.r, c.g, c.b, c.a * Opacity);

        void DrawStyle(GUIStyle style, Rect r, Color color)
        {
            if (style == null || Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = Faded(color);
            style.Draw(r, false, false, false, false);
            GUI.color = old;
        }

        // ---- Texture generation ----------------------------------------------------------------------

        /// <summary>Builds a white texture whose alpha comes from <paramref name="coverage"/> (4×4 supersampled).</summary>
        static Texture2D MakeTexture(int width, int height, System.Func<float, float, float> coverage, int samples = 4)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "FRC6874 HUD",
            };
            var pixels = new Color32[width * height];
            float inv = 1f / samples;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                            sum += coverage(x + (sx + 0.5f) * inv, y + (sy + 0.5f) * inv);
                    float alpha = Mathf.Clamp01(sum * inv * inv);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D MakeRounded(int size, float radius) =>
            MakeTexture(size, size, (x, y) =>
            {
                float dx = Mathf.Max(Mathf.Max(radius - x, 0f), x - (size - radius));
                float dy = Mathf.Max(Mathf.Max(radius - y, 0f), y - (size - radius));
                return dx * dx + dy * dy <= radius * radius ? 1f : 0f;
            });

        /// <summary>Filled disc, or a ring when <paramref name="thickness"/> &gt; 0.</summary>
        static float Circle(float x, float y, float size, float thickness)
        {
            float c = size * 0.5f;
            float outer = c - 1f;
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            if (d > outer) return 0f;
            if (thickness > 0f && d < outer - thickness) return 0f;
            return 1f;
        }

        /// <summary>Downward-pointing triangle (texture y = 0 is the bottom row).</summary>
        static float Triangle(float x, float y)
        {
            const float width = 32f, height = 24f;
            float apexY = 1f, topY = height - 1f;
            if (y < apexY || y > topY) return 0f;
            float halfWidth = (y - apexY) / (topY - apexY) * (width * 0.5f - 1f);
            return Mathf.Abs(x - width * 0.5f) <= halfWidth ? 1f : 0f;
        }

        static void DestroyTexture(Texture2D texture)
        {
            if (texture != null) Object.Destroy(texture);
        }
    }
}
