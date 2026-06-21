using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Rotates a 2D flashlight toward the mouse cursor, keeps it positioned in front of the player, supports mouse-button toggling, and optionally applies horror-style flicker.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class SmoothRotateToMouse : MonoBehaviour
{
    [Tooltip("Camera used to convert mouse screen position to world position. Defaults to Camera.main if empty.")]
    [SerializeField] private Camera cam;

    [Tooltip("Player transform used as the flashlight anchor point.")]
    [SerializeField] private Transform player;

    [Tooltip("Distance in world units to keep the flashlight in front of the player.")]
    [SerializeField] private float followDistance = 0.6f;

    [Tooltip("Max rotation speed (degrees per second) when turning toward the mouse.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Tooltip("Angle offset in degrees to align sprite/light orientation (e.g., -90 for up-facing assets).")]
    [SerializeField] private float angleOffset = -90f;

    [Tooltip("Mouse button index used to toggle flashlight on/off (0=Left, 1=Right, 2=Middle).")]
    [SerializeField] private int toggleMouseButton = 0;

    [Header("Horror Flicker")]
    [Tooltip("Enables random light flicker/blackout behavior while the user light is on.")]
    [SerializeField] private bool enableFlicker = true;

    [Tooltip("Chance for a short flicker event at each flicker check.")]
    [SerializeField] private float flickerChance = 0.08f;

    [Tooltip("Chance for a long blackout event at each flicker check.")]
    [SerializeField] private float longBlackoutChance = 0.02f;

    [Tooltip("Random duration range (seconds) for short flicker off time.")]
    [SerializeField] private Vector2 flickerOffTime = new Vector2(0.03f, 0.1f);

    [Tooltip("Random duration range (seconds) for long blackout off time.")]
    [SerializeField] private Vector2 blackoutOffTime = new Vector2(0.4f, 1.2f);

    [Tooltip("Random interval range (seconds) between flicker checks.")]
    [SerializeField] private Vector2 flickerInterval = new Vector2(0.05f, 0.2f);

    /// <summary>Cached Light2D component attached to this object.</summary>
    private Light2D light2D;

    private bool userLightOn = true;
    private bool flickerOn = true;

    private float nextFlickerCheck;
    private float flickerOffUntil;

    /// <summary>
    /// Initializes references and schedules the first flicker check.
    /// </summary>
    void Awake()
    {
        if (cam == null) cam = Camera.main;
        light2D = GetComponent<Light2D>();
        ScheduleNextCheck();
    }

    /// <summary>
    /// Handles input toggling, flicker updates, rotation toward mouse, and follow positioning.
    /// </summary>
    void Update()
    {
        if (Input.GetMouseButtonDown(toggleMouseButton))
        {
            userLightOn = !userLightOn;
            light2D.enabled = userLightOn;
        }

        if (userLightOn && enableFlicker)
            UpdateHorrorFlicker();

        if (cam == null || player == null) return;

        RotateToMouse();

        Vector3 facing = transform.up;
        transform.position = new Vector3(
            player.position.x + facing.x * followDistance,
            player.position.y + facing.y * followDistance,
            player.position.z
        );
    }

    /// <summary>
    /// Runs timed/random flicker logic. Can trigger short flickers or longer blackouts.
    /// </summary>
    private void UpdateHorrorFlicker()
    {
        if (!flickerOn && Time.time >= flickerOffUntil)
        {
            flickerOn = true;
            light2D.enabled = true;
            ScheduleNextCheck();
        }

        if (flickerOn && Time.time >= nextFlickerCheck)
        {
            float roll = Random.value;

            if (roll < longBlackoutChance)
            {
                TriggerFlickerOff(blackoutOffTime);
            }
            else if (roll < longBlackoutChance + flickerChance)
            {
                TriggerFlickerOff(flickerOffTime);
            }
            else
            {
                ScheduleNextCheck();
            }
        }
    }

    /// <summary>
    /// Turns the light off for a random duration selected from the given range.
    /// </summary>
    /// <param name="offTimeRange">Min/max off duration in seconds.</param>
    private void TriggerFlickerOff(Vector2 offTimeRange)
    {
        flickerOn = false;
        light2D.enabled = false; 
        flickerOffUntil = Time.time + Random.Range(offTimeRange.x, offTimeRange.y);
    }

    /// <summary>
    /// Schedules the next flicker probability check.
    /// </summary>
    private void ScheduleNextCheck()
    {
        nextFlickerCheck = Time.time + Random.Range(flickerInterval.x, flickerInterval.y);
    }

    /// <summary>
    /// Rotates this object smoothly toward the mouse cursor.
    /// </summary>
    private void RotateToMouse()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, player.position.z));

        if (!plane.Raycast(ray, out float enter)) return;

        Vector3 mouseWorld = ray.GetPoint(enter);
        Vector2 direction = (Vector2)(mouseWorld - player.position);

        if (direction.sqrMagnitude <= 0.0001f) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + angleOffset;
        Quaternion target = Quaternion.Euler(0f, 0f, angle);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            target,
            rotationSpeed * Time.deltaTime
        );
    }
}