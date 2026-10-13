using UnityEngine;
using UnityEngine.Rendering;

namespace FRC6874
{
    /// <summary>
    /// Shared palette and small construction helpers for the field and venue builders.
    /// Every material comes from <see cref="MaterialFactory"/>; decorative parts are created
    /// without colliders, solid parts get explicit invisible box colliders.
    /// </summary>
    internal static class FieldKit
    {
        // ---- Palette ------------------------------------------------------------------------
        public static readonly Color CarpetColor = new Color(0.17f, 0.21f, 0.19f);
        public static readonly Color AluminumColor = new Color(0.74f, 0.76f, 0.79f);
        public static readonly Color DarkAluminumColor = new Color(0.43f, 0.45f, 0.48f);
        public static readonly Color CharcoalColor = new Color(0.13f, 0.14f, 0.16f);
        public static readonly Color RubberColor = new Color(0.05f, 0.05f, 0.06f);
        public static readonly Color TapeWhite = new Color(0.93f, 0.94f, 0.94f);
        public static readonly Color PolycarbonateColor = new Color(0.82f, 0.90f, 1.00f, 0.10f);
        public static readonly Color VenueFloorColor = new Color(0.065f, 0.065f, 0.075f);
        public static readonly Color VenueDarkColor = new Color(0.10f, 0.105f, 0.12f);

        // ---- Physics surfaces ---------------------------------------------------------------
        public const float WallBounce = 0.35f;
        public const float WallFriction = 0.15f;

        public static Material Aluminum => MaterialFactory.Lit(AluminumColor, 0.55f, 0.25f);
        public static Material DarkAluminum => MaterialFactory.Lit(DarkAluminumColor, 0.45f, 0.2f);
        public static Material Charcoal => MaterialFactory.Lit(CharcoalColor, 0.35f);
        public static Material Rubber => MaterialFactory.Lit(RubberColor, 0.1f);
        public static Material Polycarbonate => MaterialFactory.Unlit(PolycarbonateColor);
        public static Material Tape(Color color) => MaterialFactory.Lit(color, 0.15f);

        /// <summary>Alliance color mixed toward another color (opaque).</summary>
        public static Color Mix(Color a, Color b, float t)
        {
            Color c = Color.Lerp(a, b, t);
            c.a = 1f;
            return c;
        }

        /// <summary>Rotation for TextFactory text lying on a surface whose outward normal is <paramref name="normal"/>.</summary>
        public static Quaternion TextOn(Vector3 normal) => Quaternion.LookRotation(-normal);

        /// <summary>Axis-aligned (in parent space) cube. Collider-free unless <paramref name="collider"/> is true.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material,
            bool collider = false, bool shadows = true)
        {
            GameObject go = MaterialFactory.Primitive(PrimitiveType.Cube, name, parent, center, size, material, collider);
            if (!shadows) NoShadows(go);
            return go;
        }

        /// <summary>Rotated cube (rotation is local to the parent).</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Quaternion rotation, Vector3 size,
            Material material, bool collider = false, bool shadows = true)
        {
            GameObject go = Box(name, parent, center, size, material, collider, shadows);
            go.transform.localRotation = rotation;
            return go;
        }

        /// <summary>Visual-only cylinder. <paramref name="radius"/> and <paramref name="length"/> in meters along local Y.</summary>
        public static GameObject Cylinder(string name, Transform parent, Vector3 center, Quaternion rotation, float radius,
            float length, Material material, bool shadows = true)
        {
            GameObject go = MaterialFactory.Primitive(PrimitiveType.Cylinder, name, parent, center,
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), material, keepCollider: false);
            go.transform.localRotation = rotation;
            if (!shadows) NoShadows(go);
            return go;
        }

        /// <summary>Visual-only quad (faces local -Z before <paramref name="rotation"/>).</summary>
        public static GameObject Quad(string name, Transform parent, Vector3 center, Quaternion rotation, Vector2 size,
            Material material)
        {
            GameObject go = MaterialFactory.Primitive(PrimitiveType.Quad, name, parent, center,
                new Vector3(size.x, size.y, 1f), material, keepCollider: false);
            go.transform.localRotation = rotation;
            NoShadows(go);
            return go;
        }

        /// <summary>Invisible solid box collider (local to the parent).</summary>
        public static BoxCollider SolidBox(string name, Transform parent, Vector3 center, Vector3 size,
            float bounce = WallBounce, float friction = WallFriction)
        {
            return SolidBox(name, parent, center, Quaternion.identity, size, bounce, friction);
        }

        /// <summary>Invisible solid, rotated box collider (local to the parent).</summary>
        public static BoxCollider SolidBox(string name, Transform parent, Vector3 center, Quaternion rotation, Vector3 size,
            float bounce = WallBounce, float friction = WallFriction)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            PhysicsCompat.SetSurface(box, bounce, friction);
            return box;
        }

        /// <summary>GameObject that renders a procedural mesh.</summary>
        public static MeshRenderer MeshObject(string name, Transform parent, Mesh mesh, Material material,
            Vector3 localPosition, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (!shadows) renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        /// <summary>Text on a surface with outward normal <paramref name="normal"/> (parent space), lifted 6 mm off it.</summary>
        public static TextMesh Text(string text, Transform parent, Vector3 surfacePoint, Vector3 normal, float height,
            Color color)
        {
            return TextFactory.Create(text, parent, surfacePoint + normal.normalized * 0.006f, TextOn(normal), height, color);
        }

        public static void NoShadows(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        public static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
