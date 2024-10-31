using UnityEngine;

namespace PitGL
{
    public class OrbitalCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float degreesPerSecond = 10f;

        private void Update()
        {
            transform.RotateAround(target.position, Vector3.up, degreesPerSecond * Time.deltaTime);
        }
    }
}
