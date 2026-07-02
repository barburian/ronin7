using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Samples a hand's linear and angular velocity from its tracked transform, smoothed
    /// over the last few physics steps. Used for throwing grabbed objects and (in Phase 2)
    /// scaling sword swing damage by blade speed.
    /// </summary>
    public class HandVelocityTracker : MonoBehaviour
    {
        [SerializeField, Range(1, 12)] private int sampleCount = 5;

        private Vector3[] linearSamples;
        private Vector3[] angularSamples;
        private int writeIndex;
        private Vector3 lastPosition;
        private Quaternion lastRotation;

        public Vector3 LinearVelocity => Average(linearSamples);
        public Vector3 AngularVelocity => Average(angularSamples);
        public float Speed => LinearVelocity.magnitude;

        private void Awake()
        {
            linearSamples = new Vector3[sampleCount];
            angularSamples = new Vector3[sampleCount];
            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            linearSamples[writeIndex] = (transform.position - lastPosition) / dt;

            Quaternion delta = transform.rotation * Quaternion.Inverse(lastRotation);
            delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (angleDeg > 180f) angleDeg -= 360f;
            if (!float.IsInfinity(axis.x))
                angularSamples[writeIndex] = axis * (angleDeg * Mathf.Deg2Rad / dt);

            writeIndex = (writeIndex + 1) % sampleCount;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }

        private static Vector3 Average(Vector3[] samples)
        {
            if (samples == null) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < samples.Length; i++) sum += samples[i];
            return sum / samples.Length;
        }
    }
}
