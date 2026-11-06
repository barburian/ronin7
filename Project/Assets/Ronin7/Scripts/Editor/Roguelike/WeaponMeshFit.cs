using System.Collections.Generic;
using Ronin7.Core;
using Ronin7.Player;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>P9-04 (A15.16): fits the Tripo weapon meshes to real metres from their own vertices (orientation and size
    /// come out of the data, not guesses) and swaps them in for the P9-02 placeholder VISUALS on the rig gear.
    /// Gameplay transforms and colliders are never touched. No colliders are ever added.</summary>
    public static class WeaponMeshFit
    {
        public const string Dir = "Assets/Ronin7/Art/Generated/Weapons/";
        public const string MeshName = "TripoMesh";
        // Real-metre targets (A15.6/A15.7/A15.8): tip 1.4 m ahead of the hand, 0.35 m of rear; shield 0.6 m across, face at z 0.13;
        // handle 0.33 m with the ring at the existing ChainRing (0, 0, 0.22).
        public const float PolearmTip = 1.4f, PolearmRear = 0.35f, ShieldSize = 0.6f, ShieldFaceZ = 0.13f, HandleLength = 0.33f, RingZ = 0.22f;

        public struct Fit { public Vector3 pos; public Quaternion rot; public float scale; }

        public static GameObject Prefab(string kind) => AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Mesh_" + kind + ".prefab");

        /// <summary>Every vertex of the prefab in the frame its root sits in (the root's own authored rotation and scale included).</summary>
        public static List<Vector3> Verts(string kind)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(kind));
            var pts = new List<Vector3>();
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.transform.localToWorldMatrix; // inst has no parent: world = the frame above the prefab root
                foreach (var v in mf.sharedMesh.vertices) pts.Add(m.MultiplyPoint3x4(v));
            }
            Object.DestroyImmediate(inst);
            return pts;
        }

        private static Bounds BoundsOf(List<Vector3> pts)
        {
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) b.Encapsulate(p);
            return b;
        }

        public static Fit Shield()
        {
            var pts = Verts("Shield"); var b = BoundsOf(pts);
            float s = ShieldSize / Mathf.Max(b.size.x, b.size.y);
            return new Fit { rot = Quaternion.identity, scale = s, pos = new Vector3(-b.center.x * s, -b.center.y * s, ShieldFaceZ - b.max.z * s) };
        }

        /// <summary>Head to +z (broad face up), principal axis found by PCA, so a diagonal mesh lands straight on the blade box axis.</summary>
        public static Fit Polearm()
        {
            var pts = Verts("Polearm");
            var c = Vector3.zero; foreach (var p in pts) c += p; c /= pts.Count;
            var m = new float[3, 3];
            foreach (var p in pts) { var d = p - c; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) m[i, j] += d[i] * d[j]; }
            var ax = new Vector3(1f, 0.3f, 0.2f).normalized;
            for (int it = 0; it < 100; it++)
                ax = new Vector3(m[0, 0] * ax.x + m[0, 1] * ax.y + m[0, 2] * ax.z, m[1, 0] * ax.x + m[1, 1] * ax.y + m[1, 2] * ax.z, m[2, 0] * ax.x + m[2, 1] * ax.y + m[2, 2] * ax.z).normalized;
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (var p in pts) { float t = Vector3.Dot(p - c, ax); mn = Mathf.Min(mn, t); mx = Mathf.Max(mx, t); }
            float span = mx - mn, rLo = 0f, rHi = 0f;
            foreach (var p in pts)
            {
                var d = p - c; float t = Vector3.Dot(d, ax), r = (d - ax * t).magnitude;
                if (t < mn + 0.15f * span) rLo = Mathf.Max(rLo, r);
                else if (t > mx - 0.15f * span) rHi = Mathf.Max(rHi, r);
            }
            if (rLo > rHi) ax = -ax; // the broad end is the head
            var rot = Quaternion.Inverse(Quaternion.LookRotation(ax, Vector3.forward));
            float s = (PolearmTip + PolearmRear) / span;
            float zMax = float.MinValue;
            foreach (var p in pts) zMax = Mathf.Max(zMax, (rot * (p * s)).z);
            var o = rot * (c * s);
            return new Fit { rot = rot, scale = s, pos = new Vector3(-o.x, -o.y, PolearmTip - zMax) };
        }

        /// <summary>Ring end (+y in the mesh) forward; the ring's centre lands on the ChainRing.</summary>
        public static Fit WhipHandle(out Vector3 ringCentreMesh)
        {
            var pts = Verts("WhipHandle"); var b = BoundsOf(pts);
            var ring = new List<Vector3>();
            foreach (var p in pts) if (p.y >= b.max.y - 0.15f * b.size.y) ring.Add(p);
            ringCentreMesh = BoundsOf(ring).center;
            float s = HandleLength / b.size.y;
            var rot = Quaternion.Euler(90f, 0f, 0f);
            return new Fit { rot = rot, scale = s, pos = new Vector3(0f, 0f, RingZ) - rot * (ringCentreMesh * s) };
        }

        public static Fit WhipHandle() => WhipHandle(out _);

        /// <summary>A "TripoMesh" wrapper carrying the fit, with the prefab instance inside it keeping its own authored root transform.</summary>
        public static GameObject Place(Transform parent, string kind, Fit f, string name = MeshName)
        {
            var wrap = new GameObject(name).transform;
            wrap.SetParent(parent, false);
            wrap.SetLocalPositionAndRotation(f.pos, f.rot);
            wrap.localScale = Vector3.one * f.scale;
            PrefabUtility.InstantiatePrefab(Prefab(kind), wrap);
            return wrap.gameObject;
        }

        private static bool Swap(Transform root, string[] placeholderPaths, Transform parent, string kind, Fit f)
        {
            if (root == null || parent == null) return false;
            var have = parent.Find(MeshName);
            if (have != null && !PrefabUtility.IsAnyPrefabInstanceRoot(have.gameObject)) return false; // already the wrapped form
            if (have != null) Object.DestroyImmediate(have.gameObject); // earlier unwrapped instance: re-place
            foreach (var path in placeholderPaths)
            {
                var t = root.Find(path);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            Place(parent, kind, f);
            return true;
        }

        public const string BackingName = "Backing", BackingMatPath = "Assets/Ronin7/Art/Materials/HeroVariants/Gritty_15_15_18_s.mat";

        /// <summary>The Tripo shield is a one-sided open dome (single front image): from behind, where the player always looks, it was a
        /// rim with a hole. A shallow dark disc just inside the rim closes it. Visual only: no collider, one renderer. Idempotent.</summary>
        private static bool EnsureShieldBacking(Transform visual)
        {
            if (visual == null || visual.Find(BackingName) != null) return false;
            var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            d.name = BackingName;
            Object.DestroyImmediate(d.GetComponent<Collider>());
            d.transform.SetParent(visual, false);
            d.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, 0.03f), Quaternion.Euler(90f, 0f, 0f));
            d.transform.localScale = new Vector3(0.58f, 0.005f, 0.58f); // 0.58 m across inside the 0.6 m rim, 1 cm thick
            d.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BackingMatPath);
            return true;
        }

        public const string ChainMatPath = "Assets/Ronin7/Art/Materials/HeroVariants/CyberSword_Blade.mat";

        /// <summary>The chain's shared additive soft-dot material, stretched over the ~3 m line, drew a sub-pixel hairline (measured:
        /// 32 px of change at 1.5 m, 5 px at 4 m). The ring's own emissive cyan blade material reads as a solid line instead; one
        /// LineRenderer, same draw count, a touch wider for 4 m on Quest. Idempotent.</summary>
        private static bool EnsureChainLook(Grabbable whip)
        {
            var lr = whip.GetComponentInChildren<LineRenderer>(true);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ChainMatPath);
            if (lr == null || mat == null || lr.sharedMaterial == mat) return false;
            lr.sharedMaterial = mat;
            lr.startWidth = 0.03f;
            lr.endWidth = 0.018f;
            return true;
        }

        /// <summary>Idempotent: replaces the placeholder visuals with the Tripo meshes. True if anything changed.</summary>
        public static bool ApplyTo(PolearmShieldGear gear)
        {
            bool changed = false;
            if (gear.polearm != null)
            {
                var pv = gear.polearm.transform.Find("PolearmVisual");
                changed |= Swap(gear.polearm.transform, new[] { "PolearmVisual/Shaft", "PolearmVisual/Butt", "PolearmVisual/Head" }, pv, "Polearm", Polearm());
            }
            if (gear.shield != null)
            {
                var sv = gear.shield.transform.Find("ShieldVisual");
                changed |= Swap(gear.shield.transform, new[] { "ShieldVisual/Disc", "ShieldVisual/Boss" }, sv, "Shield", Shield());
                changed |= EnsureShieldBacking(sv);
            }
            if (gear.whip != null)
            {
                var hv = gear.whip.transform.Find("WhipVisual");
                changed |= Swap(gear.whip.transform, new[] { "WhipVisual/Handle", "ChainRing/Ring" }, hv, "WhipHandle", WhipHandle());
                changed |= EnsureChainLook(gear.whip);
            }
            return changed;
        }
    }
}
