using UnityEngine;

public class SmoothRotateToMouse : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private float rotationSpeed = 360f; // degrees/sec
    [SerializeField] private float angleOffset = -90f;   // adjust for your sprite/light orientation

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null) return;

        // Ray from mouse into world
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Plane at this object's Z (for 2D XY gameplay)
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 mouseWorld = ray.GetPoint(enter);
            Vector2 direction = (Vector2)(mouseWorld - transform.position);

            if (direction.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + angleOffset;
                Quaternion target = Quaternion.Euler(0f, 0f, angle);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    target,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }
}