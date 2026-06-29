using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Rotates a 2D flashlight toward the mouse cursor, keeps it positioned in front of the player, supports mouse-button toggling, and optionally applies horror-style flicker.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class SmoothRotateToMouse : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Transform player;
    [SerializeField] private float followDistance = 0.6f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float angleOffset = -90f;
    [SerializeField] private int toggleMouseButton = 0;
    [SerializeField] private ParticleSystem flashlightParticles;

    [Header("Horror Flicker")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField] private float flickerChance = 0.08f;
    [SerializeField] private float longBlackoutChance = 0.02f;
    [SerializeField] private Vector2 flickerOffTime = new Vector2(0.03f, 0.1f);
    [SerializeField] private Vector2 blackoutOffTime = new Vector2(0.4f, 1.2f);
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
        SyncParticlePosition();
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
            SetFlashlightState(userLightOn);
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

        SyncParticlePosition();

    }

    /// <summary>
    /// Runs timed/random flicker logic. Can trigger short flickers or longer blackouts.
    /// </summary>
    private void UpdateHorrorFlicker()
    {
        if (!flickerOn && Time.time >= flickerOffUntil)
        {
            flickerOn = true;
            SetFlashlightState(true);
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
        SetFlashlightState(false);
        flickerOffUntil = Time.time + Random.Range(offTimeRange.x, offTimeRange.y);
    }

    private void SetFlashlightState(bool isOn)
    {
        light2D.enabled = isOn;

        if (flashlightParticles == null) return;

        if (isOn)
        {
            if (!flashlightParticles.isPlaying)
                flashlightParticles.Play();
        }
        else
        {
            if (flashlightParticles.isPlaying)
                flashlightParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void SyncParticlePosition()
    {
        if (flashlightParticles == null) return;

        flashlightParticles.transform.position = transform.position;
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