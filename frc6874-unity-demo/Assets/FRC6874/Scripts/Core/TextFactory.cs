using UnityEngine;

namespace FRC6874
{
    /// <summary>3D world-space text (bumper numbers, field signage) using Unity's built-in font.</summary>
    public static class TextFactory
    {
        static Font builtinFont;

        public static Font BuiltinFont
        {
            get
            {
                if (builtinFont != null) return builtinFont;
#if UNITY_2022_2_OR_NEWER
                builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                return builtinFont;
            }
        }

        /// <summary>
        /// Creates a TextMesh. <paramref name="height"/> is the approximate world height of a capital
        /// letter in meters. With an identity rotation the text reads correctly when viewed from
        /// the parent's local -Z side (looking toward +Z), like a Unity quad. To put text on a
        /// surface whose outward normal is n, use Quaternion.LookRotation(-n) and offset it a few
        /// millimeters along n to avoid z-fighting.
        /// </summary>
        public static TextMesh Create(string text, Transform parent, Vector3 localPosition, Quaternion localRotation,
            float height, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject($"Text '{text}'");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;

            var mesh = go.AddComponent<TextMesh>();
            mesh.font = BuiltinFont;
            mesh.text = text;
            mesh.fontSize = 64;              // high resolution glyphs
            // A TextMesh line is roughly characterSize * fontSize / 10 units tall; capitals are ~70% of a line.
            mesh.characterSize = height * 10f / (mesh.fontSize * 0.7f);
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = style;
            mesh.color = color;

            var renderer = go.GetComponent<MeshRenderer>();
            if (BuiltinFont != null) renderer.sharedMaterial = BuiltinFont.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }
    }
}
