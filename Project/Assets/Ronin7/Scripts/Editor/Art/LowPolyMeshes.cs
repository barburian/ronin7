using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Flat-shaded faceted replacements for the Unity primitives the art builders compose with.
    /// Every mesh duplicates vertices per facet (per-face normals = the low-poly look) and bakes
    /// position-smoothed normals into UV2 so SamuraiOutline's inverted hull stays watertight at
    /// hard edges. All meshes match the Unity primitive they replace in pivot and extents
    /// (Cube 1x1x1, Sphere r0.5, Cylinder r0.5 h2, Capsule r0.5 h2) so existing pos/scale values
    /// in builders and character spec JSONs are reused unchanged.
    ///
    /// Assets are cached in Art/Meshes/ and refilled IN PLACE (Clear + refill) — never
    /// delete/recreate, because saved prefabs reference these meshes by GUID.
    /// </summary>
    internal static class LowPolyMeshes
    {
        private const string MeshFolder = "Assets/Ronin7/Art/Meshes";

        private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

        public static Mesh Cube()         => GetOrBuild("LowPoly_Cube", m => FillFacets(m, CubeFacets()));
        public static Mesh ChamferCube()  => GetOrBuild("LowPoly_ChamferCube", m => FillFacets(m, ChamferCubeFacets(0.1f)));
        public static Mesh Icosphere()    => GetOrBuild("LowPoly_Icosphere", m => FillFacets(m, IcosphereFacets(0.5f, 1)));
        public static Mesh Cylinder8()    => GetOrBuild("LowPoly_Cylinder8", m => FillFacets(m, CylinderFacets(8, 0.5f, 1f)));
        public static Mesh Capsule8()     => GetOrBuild("LowPoly_Capsule8", m => FillFacets(m, CapsuleFacets(8, 0.5f, 1f)));
        public static Mesh PlanetSphere() => GetOrBuild("LowPoly_PlanetSphere",
            m => FillFacets(m, IcosphereFacets(0.5f, 2), SphericalFacetUVs));

        public static Mesh ForType(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Sphere:   return Icosphere();
                case PrimitiveType.Cylinder: return Cylinder8();
                case PrimitiveType.Capsule:  return Capsule8();
                default:                     return Cube();
            }
        }

        /// <summary>Force-regenerates every mesh asset in place (existing prefab references survive).</summary>
        public static void RebuildAll()
        {
            _cache.Clear();
            Cube(); ChamferCube(); Icosphere(); Cylinder8(); Capsule8(); PlanetSphere();
            AssetDatabase.SaveAssets();
            Debug.Log($"[LowPolyMeshes] Rebuilt 6 faceted mesh assets in {MeshFolder}/.");
        }

        // ---------------- asset caching ----------------

        private static Mesh GetOrBuild(string name, Action<Mesh> fill)
        {
            if (_cache.TryGetValue(name, out var cached) && cached != null) return cached;

            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                fill(existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                _cache[name] = existing;
                return existing;
            }

            var mesh = new Mesh { name = name };
            fill(mesh);
            EnsureAssetFolder(MeshFolder);
            AssetDatabase.CreateAsset(mesh, path);
            var loaded = AssetDatabase.LoadAssetAtPath<Mesh>(path) ?? mesh;
            _cache[name] = loaded;
            return loaded;
        }

        // ---------------- facet bake ----------------

        /// <summary>
        /// Bakes a facet list (3-4 CCW-outward corners each; winding is auto-corrected for these
        /// origin-centered convex shapes) into a flat-shaded mesh: vertices duplicated per facet
        /// with the facet normal, UV0 from <paramref name="facetUVs"/> (default: dominant-axis
        /// planar projection, so the texel grid lands axis-aligned on flat faces), and UV2 holding
        /// position-smoothed area-weighted normals for crack-free inverted-hull outlines.
        /// </summary>
        private static void FillFacets(Mesh mesh, List<Vector3[]> facets,
            Func<Vector3[], Vector3, Vector2[]> facetUVs = null)
        {
            facetUVs = facetUVs ?? PlanarFacetUVs;

            // Pass 1: fix winding, accumulate smoothed normals per quantized position.
            var smoothAccum = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 p) => new Vector3Int(
                Mathf.RoundToInt(p.x * 10000f),
                Mathf.RoundToInt(p.y * 10000f),
                Mathf.RoundToInt(p.z * 10000f));

            foreach (var f in facets)
            {
                var raw = Vector3.Cross(f[1] - f[0], f[2] - f[0]); // area-weighted facet normal
                var centroid = Vector3.zero;
                foreach (var c in f) centroid += c;
                centroid /= f.Length;
                if (Vector3.Dot(raw, centroid) < 0f)
                {
                    Array.Reverse(f);
                    raw = -raw;
                }
                foreach (var c in f)
                {
                    var key = Key(c);
                    smoothAccum.TryGetValue(key, out var acc);
                    smoothAccum[key] = acc + raw;
                }
            }

            // Pass 2: emit duplicated vertices per facet.
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var smooth = new List<Vector3>();
            var tris = new List<int>();

            foreach (var f in facets)
            {
                var n = Vector3.Cross(f[1] - f[0], f[2] - f[0]).normalized;
                var fuv = facetUVs(f, n);
                int baseIdx = verts.Count;
                for (int i = 0; i < f.Length; i++)
                {
                    verts.Add(f[i]);
                    normals.Add(n);
                    uvs.Add(fuv[i]);
                    smooth.Add(smoothAccum[Key(f[i])].normalized);
                }
                for (int i = 2; i < f.Length; i++) // triangle fan (handles tris and quads)
                {
                    tris.Add(baseIdx);
                    tris.Add(baseIdx + i - 1);
                    tris.Add(baseIdx + i);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, smooth); // TEXCOORD1: smoothed normals for SamuraiOutline
            mesh.RecalculateBounds();
        }

        private static Vector2[] PlanarFacetUVs(Vector3[] corners, Vector3 n)
        {
            var an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            var result = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                var p = corners[i];
                Vector2 uv = an.x >= an.y && an.x >= an.z ? new Vector2(p.z, p.y)
                           : an.y >= an.z ? new Vector2(p.x, p.z)
                           : new Vector2(p.x, p.y);
                result[i] = uv + new Vector2(0.5f, 0.5f); // unit face spans [0,1]
            }
            return result;
        }

        /// <summary>
        /// Equirectangular UVs for the planet sphere. Longitude is recomputed per corner relative
        /// to the facet-center longitude (±1 wrap) so facets straddling the date line don't smear
        /// the whole texture backwards across one column; pole corners take the facet-center u.
        /// </summary>
        private static Vector2[] SphericalFacetUVs(Vector3[] corners, Vector3 n)
        {
            var centroid = Vector3.zero;
            foreach (var c in corners) centroid += c;
            centroid /= corners.Length;
            float uc = Mathf.Atan2(centroid.z, centroid.x) / (2f * Mathf.PI) + 0.5f;

            var result = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                var p = corners[i];
                float r = p.magnitude;
                float v = Mathf.Asin(Mathf.Clamp(p.y / r, -1f, 1f)) / Mathf.PI + 0.5f;
                float u;
                if (Mathf.Abs(p.y) > r * 0.9999f)
                {
                    u = uc; // pole: longitude undefined, inherit facet center
                }
                else
                {
                    u = Mathf.Atan2(p.z, p.x) / (2f * Mathf.PI) + 0.5f;
                    if (u - uc > 0.5f) u -= 1f;
                    else if (u - uc < -0.5f) u += 1f;
                }
                result[i] = new Vector2(u, v);
            }
            return result;
        }

        // ---------------- shapes ----------------

        private static readonly (Vector3 axis, Vector3 u, Vector3 v)[] CubeFaces =
        {
            (Vector3.right,   Vector3.up,      Vector3.forward),
            (Vector3.left,    Vector3.up,      Vector3.back),
            (Vector3.up,      Vector3.forward, Vector3.right),
            (Vector3.down,    Vector3.back,    Vector3.right),
            (Vector3.forward, Vector3.up,      Vector3.left),
            (Vector3.back,    Vector3.up,      Vector3.right),
        };

        private static List<Vector3[]> CubeFacets()
        {
            var facets = new List<Vector3[]>();
            foreach (var (axis, u, v) in CubeFaces)
            {
                facets.Add(new[]
                {
                    axis * 0.5f - u * 0.5f - v * 0.5f,
                    axis * 0.5f + u * 0.5f - v * 0.5f,
                    axis * 0.5f + u * 0.5f + v * 0.5f,
                    axis * 0.5f - u * 0.5f + v * 0.5f,
                });
            }
            return facets;
        }

        /// <summary>Unit cube with flat 45° chamfers: 6 inset faces + 12 edge quads + 8 corner tris.</summary>
        private static List<Vector3[]> ChamferCubeFacets(float chamfer)
        {
            const float h = 0.5f;
            float i = h - chamfer;
            var facets = new List<Vector3[]>();

            // 6 inset face squares
            foreach (var (axis, u, v) in CubeFaces)
            {
                facets.Add(new[]
                {
                    axis * h - u * i - v * i,
                    axis * h + u * i - v * i,
                    axis * h + u * i + v * i,
                    axis * h - u * i + v * i,
                });
            }

            // 12 edge quads — one per (axis pair, sign pair)
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            for (int a = 0; a < 3; a++)
            for (int b = a + 1; b < 3; b++)
            {
                int c = 3 - a - b; // remaining axis index
                var A = axes[a]; var B = axes[b]; var C = axes[c];
                foreach (float sa in new[] { -1f, 1f })
                foreach (float sb in new[] { -1f, 1f })
                {
                    facets.Add(new[]
                    {
                        A * (sa * h) + B * (sb * i) - C * i,
                        A * (sa * h) + B * (sb * i) + C * i,
                        A * (sa * i) + B * (sb * h) + C * i,
                        A * (sa * i) + B * (sb * h) - C * i,
                    });
                }
            }

            // 8 corner triangles
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sy in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                facets.Add(new[]
                {
                    new Vector3(sx * h, sy * i, sz * i),
                    new Vector3(sx * i, sy * h, sz * i),
                    new Vector3(sx * i, sy * i, sz * h),
                });
            }

            return facets;
        }

        /// <summary>Icosahedron subdivided <paramref name="subdivisions"/> times, projected to radius.</summary>
        private static List<Vector3[]> IcosphereFacets(float radius, int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * radius;

            var faceIdx = new[,]
            {
                {0,11,5},{0,5,1},{0,1,7},{0,7,10},{0,10,11},
                {1,5,9},{5,11,4},{11,10,2},{10,7,6},{7,1,8},
                {3,9,4},{3,4,2},{3,2,6},{3,6,8},{3,8,9},
                {4,9,5},{2,4,11},{6,2,10},{8,6,7},{9,8,1},
            };

            var facets = new List<Vector3[]>();
            for (int f = 0; f < faceIdx.GetLength(0); f++)
                facets.Add(new[] { v[faceIdx[f, 0]], v[faceIdx[f, 1]], v[faceIdx[f, 2]] });

            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<Vector3[]>(facets.Count * 4);
                foreach (var tri in facets)
                {
                    Vector3 Mid(Vector3 p, Vector3 q) => ((p + q) * 0.5f).normalized * radius;
                    var ab = Mid(tri[0], tri[1]);
                    var bc = Mid(tri[1], tri[2]);
                    var ca = Mid(tri[2], tri[0]);
                    next.Add(new[] { tri[0], ab, ca });
                    next.Add(new[] { tri[1], bc, ab });
                    next.Add(new[] { tri[2], ca, bc });
                    next.Add(new[] { ab, bc, ca });
                }
                facets = next;
            }
            return facets;
        }

        /// <summary>N-sided prism matching Unity's cylinder extents (radius, half-height).</summary>
        private static List<Vector3[]> CylinderFacets(int sides, float radius, float halfHeight)
        {
            var facets = new List<Vector3[]>();
            for (int k = 0; k < sides; k++)
            {
                float a0 = k / (float)sides * 2f * Mathf.PI;
                float a1 = (k + 1) / (float)sides * 2f * Mathf.PI;
                var b0 = new Vector3(Mathf.Cos(a0) * radius, -halfHeight, Mathf.Sin(a0) * radius);
                var b1 = new Vector3(Mathf.Cos(a1) * radius, -halfHeight, Mathf.Sin(a1) * radius);
                var t0 = b0 + Vector3.up * (2f * halfHeight);
                var t1 = b1 + Vector3.up * (2f * halfHeight);
                facets.Add(new[] { b0, b1, t1, t0 });                              // side
                facets.Add(new[] { new Vector3(0, halfHeight, 0), t0, t1 });       // top cap
                facets.Add(new[] { new Vector3(0, -halfHeight, 0), b1, b0 });      // bottom cap
            }
            return facets;
        }

        /// <summary>
        /// N-sided capsule matching Unity's (radius 0.5, total height 2): straight barrel between
        /// y = ±(halfHeight - radius), one 45° latitude ring + pole fan per hemisphere.
        /// </summary>
        private static List<Vector3[]> CapsuleFacets(int sides, float radius, float halfHeight)
        {
            float yBarrel = halfHeight - radius;                    // 0.5
            float rRing = radius * Mathf.Cos(Mathf.PI / 4f);        // 45° latitude ring
            float yRing = yBarrel + radius * Mathf.Sin(Mathf.PI / 4f);

            var facets = new List<Vector3[]>();
            for (int k = 0; k < sides; k++)
            {
                float a0 = k / (float)sides * 2f * Mathf.PI;
                float a1 = (k + 1) / (float)sides * 2f * Mathf.PI;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0);
                float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);

                var bBot0 = new Vector3(c0 * radius, -yBarrel, s0 * radius);
                var bBot1 = new Vector3(c1 * radius, -yBarrel, s1 * radius);
                var bTop0 = new Vector3(c0 * radius, yBarrel, s0 * radius);
                var bTop1 = new Vector3(c1 * radius, yBarrel, s1 * radius);
                var rTop0 = new Vector3(c0 * rRing, yRing, s0 * rRing);
                var rTop1 = new Vector3(c1 * rRing, yRing, s1 * rRing);
                var rBot0 = new Vector3(c0 * rRing, -yRing, s0 * rRing);
                var rBot1 = new Vector3(c1 * rRing, -yRing, s1 * rRing);

                facets.Add(new[] { bBot0, bBot1, bTop1, bTop0 });                  // barrel
                facets.Add(new[] { bTop0, bTop1, rTop1, rTop0 });                  // upper ring
                facets.Add(new[] { new Vector3(0, halfHeight, 0), rTop0, rTop1 }); // upper pole fan
                facets.Add(new[] { bBot0, bBot1, rBot1, rBot0 });                  // lower ring
                facets.Add(new[] { new Vector3(0, -halfHeight, 0), rBot0, rBot1 });// lower pole fan
            }
            return facets;
        }

        // ---------------- folders ----------------

        private static void EnsureAssetFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string[] segments = assetFolderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
