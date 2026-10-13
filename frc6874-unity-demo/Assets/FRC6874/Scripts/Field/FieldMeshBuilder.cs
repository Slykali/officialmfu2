using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FRC6874
{
    /// <summary>
    /// Builds simple procedural meshes (boxes, cones/cylinders, rings) with correct normals and
    /// Unity's clockwise front-face winding. Used for the hub, field markings, bleachers and the
    /// crowd (many boxes combined into one mesh keeps the renderer count low).
    /// Round shapes are centered on the local Y axis; angle 0 points along +X and increases toward +Z.
    /// </summary>
    internal sealed class FieldMeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int> triangles = new List<int>();

        public int VertexCount => vertices.Count;
        public bool IsEmpty => vertices.Count == 0;

        /// <summary>Adds a quad given its corners as seen from the front: bottom-left, top-left, top-right, bottom-right.</summary>
        public void AddQuad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 normal)
        {
            int i = vertices.Count;
            vertices.Add(bl); vertices.Add(tl); vertices.Add(tr); vertices.Add(br);
            normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }

        /// <summary>Adds a box with six outward faces.</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation)
        {
            AddBoxFace(center, size, rotation, Vector3.right, Vector3.up);
            AddBoxFace(center, size, rotation, Vector3.left, Vector3.up);
            AddBoxFace(center, size, rotation, Vector3.forward, Vector3.up);
            AddBoxFace(center, size, rotation, Vector3.back, Vector3.up);
            AddBoxFace(center, size, rotation, Vector3.up, Vector3.forward);
            AddBoxFace(center, size, rotation, Vector3.down, Vector3.forward);
        }

        public void AddBox(Vector3 center, Vector3 size) => AddBox(center, size, Quaternion.identity);

        void AddBoxFace(Vector3 center, Vector3 size, Quaternion rotation, Vector3 n, Vector3 v)
        {
            // Viewed from outside (looking along -n) with v as screen-up, screen-right is Cross(n, v).
            Vector3 u = Vector3.Cross(n, v);
            Vector3 hn = n * (Mathf.Abs(Vector3.Dot(n, size)) * 0.5f);
            Vector3 hu = u * (Mathf.Abs(Vector3.Dot(u, size)) * 0.5f);
            Vector3 hv = v * (Mathf.Abs(Vector3.Dot(v, size)) * 0.5f);
            Vector3 c = hn;
            AddQuad(
                center + rotation * (c - hu - hv),
                center + rotation * (c - hu + hv),
                center + rotation * (c + hu + hv),
                center + rotation * (c + hu - hv),
                rotation * n);
        }

        /// <summary>
        /// Adds the side surface of a cone frustum from radius <paramref name="r0"/> at <paramref name="y0"/>
        /// to <paramref name="r1"/> at <paramref name="y1"/> (y1 &gt; y0), smooth-shaded, facing outward or inward.
        /// </summary>
        public void AddFrustum(int segments, float r0, float y0, float r1, float y1, bool outward)
        {
            float dy = y1 - y0;
            float dr = r1 - r0;
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                Vector3 n = new Vector3(cos * dy, -dr, sin * dy).normalized;
                if (!outward) n = -n;
                vertices.Add(new Vector3(cos * r0, y0, sin * r0)); normals.Add(n);
                vertices.Add(new Vector3(cos * r1, y1, sin * r1)); normals.Add(n);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;      // bottom i
                int b = a + 1;              // top i
                int c = a + 3;              // top i + 1
                int d = a + 2;              // bottom i + 1
                if (outward)
                {
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }
                else
                {
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                }
            }
        }

        /// <summary>Adds a flat ring (or a disc when <paramref name="rInner"/> is 0) at height <paramref name="y"/>.</summary>
        public void AddAnnulus(int segments, float rInner, float rOuter, float y, bool facingUp)
        {
            Vector3 n = facingUp ? Vector3.up : Vector3.down;
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                vertices.Add(new Vector3(cos * rInner, y, sin * rInner)); normals.Add(n);
                vertices.Add(new Vector3(cos * rOuter, y, sin * rOuter)); normals.Add(n);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;      // inner i
                int b = a + 2;              // inner i + 1
                int c = a + 3;              // outer i + 1
                int d = a + 1;              // outer i
                if (facingUp)
                {
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }
                else
                {
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                }
            }
        }

        /// <summary>Adds a closed thick-walled tube (ring band) between two radii and two heights.</summary>
        public void AddTube(int segments, float rInner, float rOuter, float y0, float y1)
        {
            AddFrustum(segments, rOuter, y0, rOuter, y1, true);
            if (rInner > 0.0001f) AddFrustum(segments, rInner, y0, rInner, y1, false);
            AddAnnulus(segments, rInner, rOuter, y1, true);
            AddAnnulus(segments, rInner, rOuter, y0, false);
        }

        /// <summary>Adds a solid cylinder with caps.</summary>
        public void AddCylinder(int segments, float radius, float y0, float y1) => AddTube(segments, 0f, radius, y0, y1);

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            triangles.Clear();
        }
    }
}
