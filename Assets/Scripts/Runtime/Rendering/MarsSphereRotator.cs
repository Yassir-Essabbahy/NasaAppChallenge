using UnityEngine;

namespace Outpost.Runtime.Rendering
{
    /// <summary>
    /// Slowly rotates the Mars planet sphere backdrop at a configurable angular speed.
    /// </summary>
    public class MarsSphereRotator : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 2.0f;
        [SerializeField] private Vector3 localAxis = new Vector3(0.08f, 1.0f, 0.0f);

        private void Update()
        {
            Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
            transform.Rotate(axis, degreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }

        public void Configure(float speedDegPerSec)
        {
            degreesPerSecond = speedDegPerSec;
        }
    }
}
