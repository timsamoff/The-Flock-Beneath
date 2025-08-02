using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SheepBehavior : MonoBehaviour
{
    private enum SheepState
    {
        Grazing,
        Wandering,
        Following,
        Disengaging,
        Corralled,
        SettlingInCorral
    }

    [Header("Wandering Settings")]
    [SerializeField] private float wanderMinDistance = 2f;
    [SerializeField] private float wanderMaxDistance = 8f;
    [SerializeField] private float wanderSpeed = 1.2f;

    [Header("Grazing Settings")]
    [SerializeField] private float minGrazingTime = 2f;
    [SerializeField] private float maxGrazingTime = 5f;

    [Header("Following Settings")]
    [SerializeField] private float followSpeed = 2f;
    [SerializeField] private float shepherdFollowDistance = 2.5f;
    [SerializeField] private float minFollowTime = 4f;
    [SerializeField] private float disengageCheckInterval = 2f;
    [SerializeField] private float disengageChancePerCheck = 0.02f;

    [Header("Corral Settings")]
    [SerializeField] private float timeToSettleInCorral = 1.5f;

    [Header("References")]
    [SerializeField] private Transform shepherd;
    [SerializeField] private Collider2D corralZone;

    private Rigidbody2D rb;
    private Collider2D sheepCollider;
    private Camera mainCam;

    private SheepState currentState;
    private Vector2 wanderTarget;
    private Vector2 velocityRef;
    private float stateTimer;
    private float followTimer;
    private float disengageCheckTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sheepCollider = GetComponent<Collider2D>();
        mainCam = Camera.main;

        if (shepherd == null)
            shepherd = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void Start()
    {
        EnterGrazingState();
    }

    private void FixedUpdate()
    {
        switch (currentState)
        {
            case SheepState.Grazing:
                HandleGrazing();
                break;
            case SheepState.Wandering:
                HandleWandering();
                break;
            case SheepState.Following:
                HandleFollowing();
                break;
            case SheepState.Disengaging:
                HandleDisengaging();
                break;
            case SheepState.SettlingInCorral:
                HandleSettlingInCorral();
                break;
            case SheepState.Corralled:
                HandleCorralled();
                break;
        }

        CheckScreenBounds();
    }

    private void EnterGrazingState()
    {
        currentState = SheepState.Grazing;
        stateTimer = Random.Range(minGrazingTime, maxGrazingTime);
        rb.linearVelocity = Vector2.zero;
    }

    private void HandleGrazing()
    {
        stateTimer -= Time.fixedDeltaTime;

        if (IsShepherdInRange())
        {
            EnterFollowingState();
            return;
        }

        if (stateTimer <= 0f)
        {
            wanderTarget = GetRandomWanderTarget();
            currentState = SheepState.Wandering;
        }
    }

    private void HandleWandering()
    {
        if (IsShepherdInRange())
        {
            EnterFollowingState();
            return;
        }

        MoveToward(wanderTarget, wanderSpeed);
        RotateTowardMovement();

        if (Vector2.Distance(rb.position, wanderTarget) < 0.2f)
        {
            EnterGrazingState();
        }
    }

    private void EnterFollowingState()
    {
        currentState = SheepState.Following;
        followTimer = 0f;
        disengageCheckTimer = disengageCheckInterval;
    }

    private void HandleFollowing()
    {
        followTimer += Time.fixedDeltaTime;
        disengageCheckTimer -= Time.fixedDeltaTime;

        if (IsFullyInsideCorral())
        {
            EnterSettlingInCorralState();
            return;
        }

        if (followTimer >= minFollowTime && disengageCheckTimer <= 0f)
        {
            disengageCheckTimer = disengageCheckInterval;
            if (Random.value < disengageChancePerCheck)
            {
                wanderTarget = GetRandomWanderTarget();
                stateTimer = Random.Range(1f, 2f);
                currentState = SheepState.Disengaging;
                return;
            }
        }

        MoveToward(shepherd.position, followSpeed);
        RotateToward(shepherd.position);
    }

    private void EnterSettlingInCorralState()
    {
        currentState = SheepState.SettlingInCorral;
        stateTimer = timeToSettleInCorral;
        rb.linearVelocity = Vector2.zero;
    }

    private void HandleSettlingInCorral()
    {
        stateTimer -= Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.zero;

        if (stateTimer <= 0f)
        {
            currentState = SheepState.Corralled;
            EnterGrazingState();
        }
    }

    private void HandleCorralled()
    {
        if (currentState != SheepState.Grazing && currentState != SheepState.Wandering)
            return;

        if (!corralZone.bounds.Contains(rb.position))
        {
            // If sheep somehow leaves corral, disengage
            wanderTarget = GetRandomWanderTarget();
            currentState = SheepState.Disengaging;
            return;
        }

        if (currentState == SheepState.Grazing && stateTimer <= 0f)
        {
            wanderTarget = GetRandomPointInCorral();
            currentState = SheepState.Wandering;
        }

        if (currentState == SheepState.Wandering)
        {
            MoveToward(wanderTarget, wanderSpeed);
            RotateTowardMovement();

            if (Vector2.Distance(rb.position, wanderTarget) < 0.2f)
            {
                EnterGrazingState();
            }
        }
    }

    private void HandleDisengaging()
    {
        MoveToward(wanderTarget, wanderSpeed);
        RotateTowardMovement();

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f)
        {
            EnterGrazingState();
        }
    }

    private bool IsShepherdInRange()
    {
        if (shepherd == null) return false;
        return Vector2.Distance(rb.position, shepherd.position) <= shepherdFollowDistance;
    }

    private bool IsFullyInsideCorral()
    {
        // Check if the entire sheep collider is inside corral bounds
        Bounds corralBounds = corralZone.bounds;
        Bounds sheepBounds = sheepCollider.bounds;

        return corralBounds.Contains(sheepBounds.min) && corralBounds.Contains(sheepBounds.max);
    }

    private Vector2 GetRandomWanderTarget()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(wanderMinDistance, wanderMaxDistance);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

        Vector2 targetPos = rb.position + offset;

        // Clamp target to screen viewport so sheep mostly stay on-screen when wandering
        Vector3 viewportPos = mainCam.WorldToViewportPoint(targetPos);
        viewportPos.x = Mathf.Clamp01(viewportPos.x);
        viewportPos.y = Mathf.Clamp01(viewportPos.y);
        Vector2 clampedWorldPos = mainCam.ViewportToWorldPoint(viewportPos);
        return clampedWorldPos;
    }

    private Vector2 GetRandomPointInCorral()
    {
        int maxAttempts = 10;
        for (int i = 0; i < maxAttempts; i++)
        {
            Bounds bounds = corralZone.bounds;
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float y = Random.Range(bounds.min.y, bounds.max.y);
            Vector2 candidate = new Vector2(x, y);

            Vector3 screenPoint = mainCam.WorldToViewportPoint(candidate);
            if (screenPoint.x >= 0f && screenPoint.x <= 1f && screenPoint.y >= 0f && screenPoint.y <= 1f)
            {
                return candidate;
            }
        }

        // Fallback: center of corral
        return corralZone.bounds.center;
    }

    private void MoveToward(Vector2 target, float speed)
    {
        Vector2 direction = (target - rb.position).normalized;
        Vector2 desiredVelocity = direction * speed;
        rb.linearVelocity = Vector2.SmoothDamp(rb.linearVelocity, desiredVelocity, ref velocityRef, 0.2f);
    }

    private void RotateToward(Vector2 target)
    {
        Vector2 direction = target - rb.position;
        if (direction.sqrMagnitude < 0.001f) return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float currentAngle = rb.rotation;
        float smoothAngle = Mathf.LerpAngle(currentAngle, targetAngle, 0.1f);

        rb.MoveRotation(smoothAngle);
    }

    private void RotateTowardMovement()
    {
        Vector2 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.01f) return;

        float targetAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
        float currentAngle = rb.rotation;
        float smoothAngle = Mathf.LerpAngle(currentAngle, targetAngle, 0.1f);

        rb.MoveRotation(smoothAngle);
    }

    private void CheckScreenBounds()
    {
        Vector3 screenPoint = mainCam.WorldToViewportPoint(transform.position);
        if (screenPoint.x < 0f || screenPoint.x > 1f || screenPoint.y < 0f || screenPoint.y > 1f)
        {
            Destroy(gameObject);
            // TODO: Notify GameManager about lost sheep
        }
    }
}