using System.Collections.Generic;
using Ronin7.Core;
using TMPro;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// P5-08: boot-room holo table showing the last run's map (regenerated from its seed) with the walked path lit,
    /// plus a caption line. Reads <c>runlog.jsonl</c> once at Start; never writes. One combined marker mesh = 1 draw.
    /// Rows (depth) run along local +Z, lanes along local X.
    /// </summary>
    public sealed class HoloMapTable : MonoBehaviour
    {
        [SerializeField] private MeshFilter markers;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private float rowStep = 0.055f;
        [SerializeField] private float laneStep = 0.13f;
        [SerializeField] private float markerSize = 0.05f;

        private static readonly Color Dim = new Color(0.02f, 0.05f, 0.09f);
        private static readonly Color Reached = new Color(0.25f, 0.6f, 0.8f);
        private static readonly Color Walked = new Color(1f, 0.85f, 0.35f);

        private void Start()
        {
            bool has = LastRunReader.TryReadLast(RunLog.FilePath, out RunLogEntry e);
            if (markers != null) markers.gameObject.SetActive(has);
            if (caption != null) caption.text = has ? LastRunReader.Caption(e) : "NO RUNS YET";
            if (!has || markers == null) return;
            RunMap map = RunMapGenerator.GenerateGraph(e.seed);
            markers.sharedMesh = BuildMesh(map, LastRunReader.NodeStates(map, e), e.path, rowStep, laneStep, markerSize);
        }

        /// <summary>A backing plate, one upward-facing diamond per node, plus a thin bar along each walked edge.</summary>
        internal static Mesh BuildMesh(RunMap map, int[] states, int[] path, float rowStep, float laneStep, float size)
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            float z0 = -0.5f * rowStep * (RunMapGenerator.TotalNodes - 1);

            Vector3 Pos(int id)
            {
                var n = map.Nodes[id];
                int lanes = 0;
                foreach (var m in map.Nodes) if (m.Depth == n.Depth) lanes++;
                return new Vector3((n.Lane - 0.5f * (lanes - 1)) * laneStep, 0f, z0 + n.Depth * rowStep);
            }

            // Dark holo backing plate under the markers (same mesh, so still 1 draw).
            Quad(verts, colors, tris, Vector3.down * 0.002f, new Vector3(1.8f * laneStep, 0, 0),
                new Vector3(0, 0, 0.5f * rowStep * (RunMapGenerator.TotalNodes + 1)), new Color(0.004f, 0.008f, 0.016f), diamond: false); // linear: x2 tint still reads near-black
            for (int i = 0; i < map.Nodes.Length; i++)
            {
                Color c = states[i] == 2 ? Walked : states[i] == 1 ? Reached : Dim;
                float s = (states[i] == 2 ? 0.6f : 0.45f) * size;
                Quad(verts, colors, tris, Pos(i), new Vector3(s, 0, 0), new Vector3(0, 0, s), c, diamond: true);
            }
            if (path != null)
                for (int k = 0; k + 1 < path.Length; k++)
                {
                    int a = path[k], b = path[k + 1];
                    if (a < 0 || b < 0 || a >= states.Length || b >= states.Length) continue;
                    Vector3 pa = Pos(a), pb = Pos(b), d = pb - pa;
                    Vector3 side = Vector3.Cross(Vector3.up, d.normalized) * (0.12f * size);
                    Quad(verts, colors, tris, (pa + pb) * 0.5f + Vector3.up * 0.001f, side, d * 0.5f, Walked * 0.7f, diamond: false);
                }

            var mesh = new Mesh { name = "HoloMap" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Quad(List<Vector3> v, List<Color> c, List<int> t, Vector3 center, Vector3 a, Vector3 b, Color col, bool diamond)
        {
            int i = v.Count;
            if (diamond) { v.Add(center + a); v.Add(center + b); v.Add(center - a); v.Add(center - b); }
            else { v.Add(center - a - b); v.Add(center - a + b); v.Add(center + a + b); v.Add(center + a - b); }
            for (int k = 0; k < 4; k++) c.Add(col);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
    }
}
