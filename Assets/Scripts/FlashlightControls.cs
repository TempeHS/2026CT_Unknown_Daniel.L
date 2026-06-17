using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class SmoothRotateToMouse : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Transform player;
    [SerializeField] private float followDistance = 0.6f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float angleOffset = -90f;
    [SerializeField] private int toggleMouseButton = 0;

    [Header("Horror Flicker")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField] private float flickerChance = 0.08f;  
    [SerializeField] private float longBlackoutChance = 0.02f;  
    [SerializeField] private Vector2 flickerOffTime = new Vector2(0.03f, 0.1f); 
    [SerializeField] private Vector2 blackoutOffTime = new Vector2(0.4f, 1.2f);  
    [SerializeField] private Vector2 flickerInterval = new Vector2(0.05f, 0.2f); 

    private Light2D light2D;
    private bool userLightOn = true; 
    private bool flickerOn = true;   
    private float nextFlickerCheck;
    private float flickerOffUntil;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        light2D = GetComponent<Light2D>();
        ScheduleNextCheck();
    }

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

    private void TriggerFlickerOff(Vector2 offTimeRange)
    {
        flickerOn = false;
        light2D.enabled = false;
        flickerOffUntil = Time.time + Random.Range(offTimeRange.x, offTimeRange.y);
    }

    private void ScheduleNextCheck()
    {
        nextFlickerCheck = Time.time + Random.Range(flickerInterval.x, flickerInterval.y);
    }

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