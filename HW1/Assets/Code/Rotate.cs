using UnityEngine;

public class Rotator : MonoBehaviour
{
    [Header("Speed Bounds")]
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 3f;

    private float finalSpeed;
    private Vector3 finalAxis;

    void Start()
    {
        // random speed within range
        finalSpeed = Random.Range(minSpeed, maxSpeed);

        // random direction
        // insideUnitSphere gives a random point inside a radius of 1,
        // which creates a random X, Y, and Z vector automatically.
        finalAxis = Random.insideUnitSphere.normalized;
    }

    void Update()
    {
        // rotate around constant random axis at constant random speed
        transform.Rotate(finalAxis * finalSpeed * Time.deltaTime);
    }
}