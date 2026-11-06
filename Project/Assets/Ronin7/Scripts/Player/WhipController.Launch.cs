using Ronin7.Combat;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// R11: launch, steer and aim marker. A flick of the hand into the front throws the chain (no buttons); the velocity comes from the lasso's
    /// <see cref="LassoGesture"/>, in rig space, so walking and snap turns do not launch and a spin, which turns too fast, does not either. The tip steers to the
    /// marked enemy, the one a cyan ring shows, picked at <see cref="MarkInterval"/>. All numbers are first guesses, tune in the headset. Nothing here runs under a lasso.
    /// </summary>
    public partial class WhipController
    {
        /// <summary>Hand speed (m/s) along the launch axis (handle plus eyes) that launches. A wrist-only snap moves the ring at about 3.</summary>
        internal const float LaunchHandSpeed = 2.5f;
        /// <summary>Real seconds before the next launch or steer: once per flick.</summary>
        internal const float LaunchRearmSeconds = 0.45f;
        /// <summary>A hand path turning faster than this is a spin, not a flick. A 1.5 rev/s lasso spin turns 540.</summary>
        internal const float LaunchMaxTurnDegreesPerSecond = 450f;
        /// <summary>A lasso spin running this long (s) blocks launching, even turning slower than the limit above.</summary>
        internal const float LaunchMaxSpinSeconds = 0.15f;
        /// <summary>The launch never aims lower than this (y of its unit direction): flat flicks stay off the floor.</summary>
        internal const float LaunchMinRise = 0.05f;
        /// <summary>Real seconds between target picks (12 Hz).</summary>
        internal const float MarkInterval = 1f / 12f;
        /// <summary>Flat range from the head: chain reach plus an arm.</summary>
        internal const float MarkRange = VerletChain.TotalLength + 0.5f;
        internal const float MarkHalfAngle = LassoOrbit.AimHalfAngle;
        /// <summary>What keeps the marker from flickering between neighbours and at the edges: the marked enemy ranks this many degrees nearer, and its limits grow by these.</summary>
        internal const float MarkStickDegrees = 8f, MarkStickRange = 0.3f, MarkStickHalfAngle = 5f;
        /// <summary>Share of the body's height the ring sits at: upper body. Without a body renderer: <see cref="MarkFallbackHeight"/> m.</summary>
        internal const float MarkHeightShare = 0.72f, MarkFallbackHeight = 1.4f;
        /// <summary>Metres from the body's centre toward the eye, so the ring is in front of the mesh.</summary>
        internal const float MarkForward = 0.4f;
        internal const float MarkRadius = 0.16f, MarkWidth = 0.03f;
        private const int MarkSegments = 24;

        private static Mesh markerMesh; // one for the process; the null check in BuildMarker covers an unload

        private GameObject marker;
        private Health marked, steered;
        private float markLeft, markHeight, steerHeight, launchRearmLeft;

        internal Health MarkedTarget => marked;
        internal bool MarkerVisible => marker != null && marker.activeSelf;
        internal Vector3 MarkerPosition => marker.transform.position;

        private void ResetAim()
        {
            marked = steered = null;
            markLeft = launchRearmLeft = 0f;
            marker.SetActive(false);
        }

        /// <summary>Once per Whip: a child of the handle, so it hides with it and exists on no other weapon. No collider. The chain's material.</summary>
        private void BuildMarker()
        {
            if (markerMesh == null) markerMesh = BuildRing();
            marker = new GameObject("WhipAimMarker", typeof(MeshFilter), typeof(MeshRenderer));
            marker.transform.SetParent(transform, false);
            marker.GetComponent<MeshFilter>().sharedMesh = markerMesh;
            var r = marker.GetComponent<MeshRenderer>();
            r.sharedMaterial = chainLine.sharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            marker.SetActive(false);
        }

        // Faces both ways: the handle's mirroring can flip the winding, and the additive material culls back faces.
        private Mesh BuildRing()
        {
            var verts = new Vector3[MarkSegments * 2];
            var colors = new Color[verts.Length];
            var tris = new int[MarkSegments * 12];
            for (int i = 0; i < MarkSegments; i++)
            {
                float a = i * 2f * Mathf.PI / MarkSegments;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts[i * 2] = dir * MarkRadius;
                verts[i * 2 + 1] = dir * (MarkRadius + MarkWidth);
                colors[i * 2] = colors[i * 2 + 1] = chainLine.startColor;
                int next = (i + 1) % MarkSegments, t = i * 12;
                int a0 = i * 2, a1 = a0 + 1, b0 = next * 2, b1 = b0 + 1;
                tris[t] = a0; tris[t + 1] = b0; tris[t + 2] = a1;
                tris[t + 3] = a1; tris[t + 4] = b0; tris[t + 5] = b1;
                tris[t + 6] = a0; tris[t + 7] = a1; tris[t + 8] = b0;
                tris[t + 9] = a1; tris[t + 10] = b1; tris[t + 11] = b0;
            }
            var mesh = new Mesh { name = "WhipAimRing", vertices = verts, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Picks at <see cref="MarkInterval"/>, drops a dead target at once, places the ring every frame.</summary>
        private void UpdateMark(float realDt)
        {
            Transform rig = transform.root;
            Transform head = HeadOf(rig);
            if (head == null || LassoBusy || Time.timeScale <= 0f)
            {
                marked = null;
                markLeft = 0f;
            }
            else
            {
                markLeft -= realDt;
                if (markLeft <= 0f)
                {
                    markLeft = MarkInterval;
                    if (rigHealth == null) rig.TryGetComponent(out rigHealth);
                    Vector3 aim = head.forward + chainRing.forward;
                    marked = rigHealth != null && !rigHealth.IsAlive ? null : PickMark(rig, head.position, aim.sqrMagnitude > 1e-3f ? aim : head.forward);
                }
                else if (marked != null && !marked.IsAlive) marked = null;
            }

            bool show = marked != null;
            if (marker.activeSelf != show) marker.SetActive(show);
            if (!show) return;
            Vector3 point = MarkPoint(marked, markHeight);
            Vector3 toEye = head.position - point;
            float eye = toEye.magnitude;
            if (eye < 1e-3f) return;
            toEye /= eye;
            marker.transform.SetPositionAndRotation(point + toEye * MarkForward, Quaternion.LookRotation(-toEye));
        }

        /// <summary>The live enemy nearest <paramref name="aim"/>, in range and cone. The current one keeps some margin: see <see cref="MarkStickDegrees"/>.</summary>
        private Health PickMark(Transform rig, Vector3 origin, Vector3 aim)
        {
            Health best = null;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < Health.Active.Count; i++)
            {
                Health h = Health.Active[i];
                if (h == null || !h.IsAlive || h.transform.root == rig || !h.TryGetComponent(out Enemy _)) continue;
                bool current = h == marked;
                float range = current ? MarkRange + MarkStickRange : MarkRange;
                float half = current ? MarkHalfAngle + MarkStickHalfAngle : MarkHalfAngle;
                if (!LassoOrbit.InAim(origin, aim, h.transform.position, range, half, out float angle)) continue;
                if (current) angle -= MarkStickDegrees;
                if (angle >= bestAngle) continue;
                best = h;
                bestAngle = angle;
            }
            if (best != null && best != marked) markHeight = HeightOf(best);
            return best;
        }

        private static float HeightOf(Health h)
        {
            if (!h.TryGetComponent(out Enemy e) || e.BodyRenderer == null) return MarkFallbackHeight;
            return (e.BodyRenderer.bounds.max.y - h.transform.position.y) * MarkHeightShare;
        }

        private static Vector3 MarkPoint(Health h, float height) => h.transform.position + Vector3.up * height;

        /// <summary>Launches on a flick; a fast tip without one (a hard slash) still steers. One or the other per <see cref="LaunchRearmSeconds"/>.</summary>
        private void TickLaunch(float realDt)
        {
            launchRearmLeft = Mathf.Max(0f, launchRearmLeft - realDt);
            if (chain.Steering)
            {
                if (steered != null && steered.IsAlive) chain.UpdateSteerTarget(MarkPoint(steered, steerHeight));
                else chain.EndSteer();
            }
            if (LassoBusy || launchRearmLeft > 0f || Time.timeScale <= 0f) return;
            if (guard != null && !guard.enabled) return; // tracking loss: the hand pose jumps

            Transform rig = transform.root;
            Transform head = HeadOf(rig);
            if (head == null) return;
            Vector3 hand = rig.TransformDirection(lassoGesture.Velocity);
            Vector3 handle = chainRing.forward;
            if (IsFlick(hand, handle, head.forward))
            {
                Vector3 dir = hand.normalized + handle;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : head.forward;
                if (dir.y < LaunchMinRise) dir = new Vector3(dir.x, LaunchMinRise, dir.z).normalized;
                launchRearmLeft = LaunchRearmSeconds;
                BeginSteerState();
                chain.Launch(dir, marked != null, marked != null ? MarkPoint(marked, markHeight) : Vector3.zero);
                return;
            }
            if (marked == null || chain.Steering) return;
            if (chain.Velocity(chain.Count - 1).sqrMagnitude < CrackSpeed * CrackSpeed) return;
            launchRearmLeft = LaunchRearmSeconds;
            BeginSteerState();
            chain.SteerTo(MarkPoint(marked, markHeight));
        }

        private void BeginSteerState()
        {
            steered = marked;
            steerHeight = markHeight;
        }

        private bool IsFlick(Vector3 hand, Vector3 handle, Vector3 headForward)
        {
            if (!lassoGesture.TurnKnown || lassoGesture.Armed || lassoGesture.SpinSeconds > LaunchMaxSpinSeconds) return false;
            float maxTurn = LaunchMaxTurnDegreesPerSecond * Mathf.Deg2Rad;
            if (lassoGesture.RotationRate.sqrMagnitude > maxTurn * maxTurn) return false;
            Vector3 axis = handle + headForward;
            if (axis.sqrMagnitude < 1e-4f) axis = headForward;
            return Vector3.Dot(hand, axis.normalized) >= LaunchHandSpeed;
        }
    }
}
