using System.Collections.Generic;
using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Creates materials at runtime that work in the Built-in pipeline, URP and HDRP, and in
    /// player builds. Lit materials are cloned from the default material Unity assigns to
    /// primitives (so the shader always matches the active pipeline and is never stripped).
    /// Unlit/transparent materials use "Sprites/Default", which is always included in builds.
    /// Materials are cached, so ask for the same parameters as often as you like.
    ///
    /// Emission is a shader feature that player builds strip unless a material asset in the build
    /// uses it, so glowing materials are cloned from <see cref="EmissiveTemplateResource"/> (a
    /// Resources material the editor setup script generates). Without it, everything still works
    /// in the editor and builds simply lose the glow.
    /// </summary>
    public static class MaterialFactory
    {
        /// <summary>Resources path of the emissive template material created by the editor setup.</summary>
        public const string EmissiveTemplateResource = "FRC6874_EmissiveTemplate";

        static Material litTemplate;
        static Material emissiveTemplate;
        static bool emissiveTemplateLoaded;
        static Shader unlitShader;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        /// <summary>Opaque lit material.</summary>
        public static Material Lit(Color color, float smoothness = 0.3f, float metallic = 0f)
        {
            string key = $"lit|{ColorKey(color)}|{smoothness:F2}|{metallic:F2}";
            if (cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            var material = new Material(LitTemplate) { name = key, color = color };
            SetFloatIfPresent(material, "_Glossiness", smoothness);   // Built-in Standard
            SetFloatIfPresent(material, "_Smoothness", smoothness);   // URP / HDRP Lit
            SetFloatIfPresent(material, "_Metallic", metallic);
            cache[key] = material;
            return material;
        }

        /// <summary>Lit material that also glows (LEDs, lights, scoring flashes).</summary>
        public static Material Emissive(Color color, float intensity = 1.5f)
        {
            string key = $"emis|{ColorKey(color)}|{intensity:F2}";
            if (cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            var material = new Material(EmissiveTemplate) { name = key, color = color };
            SetEmission(material, color * intensity);
            cache[key] = material;
            return material;
        }

        /// <summary>
        /// Unlit, vertex-colored, alpha-blended, double-sided material. Good for polycarbonate
        /// panels, field tape, glow quads, trails and line renderers.
        /// </summary>
        public static Material Unlit(Color color)
        {
            string key = $"unlit|{ColorKey(color)}";
            if (cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            var material = new Material(UnlitShader) { name = key, color = color };
            cache[key] = material;
            return material;
        }

        /// <summary>
        /// Creates an uncached lit material you can animate (e.g. LED color changes). It starts with
        /// no glow; call <see cref="SetEmission"/> to light it up.
        /// </summary>
        public static Material NewLitInstance(Color color, float smoothness = 0.3f)
        {
            var material = new Material(EmissiveTemplate) { color = color };
            SetFloatIfPresent(material, "_Glossiness", smoothness);
            SetFloatIfPresent(material, "_Smoothness", smoothness);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            return material;
        }

        /// <summary>Sets emission on any lit material created by this factory (works in Built-in and URP).</summary>
        public static void SetEmission(Material material, Color emission)
        {
            if (material == null) return;
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", emission); // HDRP
        }

        /// <summary>
        /// Creates a primitive with a collider and the given material. Pass <paramref name="keepCollider"/>
        /// false for purely visual parts.
        /// </summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
            }
            return go;
        }

        static Material LitTemplate
        {
            get
            {
                if (litTemplate != null) return litTemplate;
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                probe.SetActive(false);
                litTemplate = new Material(probe.GetComponent<Renderer>().sharedMaterial) { name = "FRC6874 Lit Template" };
                Object.Destroy(probe);
                return litTemplate;
            }
        }

        /// <summary>The generated emissive material if present and made for the active pipeline, else the lit template.</summary>
        static Material EmissiveTemplate
        {
            get
            {
                if (!emissiveTemplateLoaded)
                {
                    emissiveTemplateLoaded = true;
                    Material asset = Resources.Load<Material>(EmissiveTemplateResource);
                    if (asset != null && asset.shader == LitTemplate.shader) emissiveTemplate = asset;
                }
                return emissiveTemplate != null ? emissiveTemplate : LitTemplate;
            }
        }

        static Shader UnlitShader
        {
            get
            {
                if (unlitShader != null) return unlitShader;
                unlitShader = Shader.Find("Sprites/Default");
                if (unlitShader == null) unlitShader = LitTemplate.shader;
                return unlitShader;
            }
        }

        static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        static string ColorKey(Color c) => ColorUtility.ToHtmlStringRGBA(c);
    }
}
