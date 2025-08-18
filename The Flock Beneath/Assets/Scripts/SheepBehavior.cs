using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private float shepherdDisengageDistance = 15f;
    [SerializeField] private float shepherdStopPadding = 1.5f; // New serialized variable

    [Header("Corral Settings")]
    [SerializeField] private float timeToSettleInCorral = 1.5f;

    [Header("Collision Settings")]
    [SerializeField] private float sheepDetectionRadius = 1.2f;
    [SerializeField] private float sheepCollisionDistance = 1.8f;
    [SerializeField] private float collisionCheckInterval = 0.1f;

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
    private bool isStuckOnFence = false;
    private float stuckTimer = 0f;
    private const float maxStuckTime = 2f;
    private float timeEnteredCorral = -1f;

    private float lastCollisionCheck = 0f;
    private bool hasCollisionThisFrame = false;
    private float shepherdFollowDistanceSqr;
    private float sheepCollisionDistanceSqr;
    private float sheepDetectionRadiusSqr;
    private float shepherdDisengageDistanceSqr;
    private Vector2 lastValidWanderTarget;
    private int pathfindingAttempts = 5;
    private GameManager gameManager;

    private static readonly Collider2D[] tempColliderArray = new Collider2D[10];
    private static bool showGizmos = false;
    [HideInInspector] public bool isCorralled = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sheepCollider = GetComponent<Collider2D>();
        mainCam = Camera.main;

        if (shepherd == null)
            shepherd = GameObject.FindGameObjectWithTag("Player")?.transform;

        shepherdFollowDistanceSqr = shepherdFollowDistance * shepherdFollowDistance;
        sheepCollisionDistanceSqr = sheepCollisionDistance * sheepCollisionDistance;
        sheepDetectionRadiusSqr = sheepDetectionRadius * sheepDetectionRadius;
        shepherdDisengageDistanceSqr = shepherdDisengageDistance * shepherdDisengageDistance;
    }

    private void Start()
    {
        EnterGrazingState();
        lastValidWanderTarget = rb.position;
    }

    void Update()
    {
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            showGizmos = !showGizmos;
        }

        CheckCorralStatus();
    }

    private void FixedUpdate()
    {
        hasCollisionThisFrame = false;
        if (Time.time - lastCollisionCheck > collisionCheckInterval)
        {
            hasCollisionThisFrame = IsCollidingWithOtherSheep();
            lastCollisionCheck = Time.time;
        }

        if (hasCollisionThisFrame)
        {
            HandleSheepCollision();
            rb.linearVelocity = Vector2.zero;
            return;
        }

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
        CheckIfStuck();
    }

    public void SetGameManager(GameManager gm)
    {
        gameManager = gm;
    }

    private void CheckCorralStatus()
    {
        if (corralZone == null) return;

        bool isCurrentlyInCorral = corralZone.bounds.Contains(GetComponent<Collider2D>().bounds.min) &&
                                   corralZone.bounds.Contains(GetComponent<Collider2D>().bounds.max);

        // If sheep enters 
        if (isCurrentlyInCorral && !isCorralled)
        {
            isCorralled = true;
            if (gameManager != null)
            {
                gameManager.SheepEnteredCorral(this);
                Debug.Log("Sheep is fully inside the corral. Counted!");
            }
        }
        // If sheep leaves corral
        else if (!isCurrentlyInCorral && isCorralled)
        {
            isCorralled = false;
            if (gameManager != null)
            {
                gameManager.SheepLeftCorral(this);
                Debug.Log("Sheep has left the corral. Un-counted.");
            }
        }
    }

    private void EnterGrazingState()
    {
        currentState = SheepState.Grazing;
        stateTimer = Random.Range(minGrazingTime, maxGrazingTime);
        rb.linearVelocity = Vector2.zero;
        isStuckOnFence = false;
        stuckTimer = 0f;
    }

    private void HandleGrazing()
    {
        stateTimer -= Time.fixedDeltaTime;

        if (IsShepherdInRange() && !isCorralled)
        {
            EnterFollowingState();
            return;
        }

        if (stateTimer <= 0f)
        {
            if (isCorralled)
            {
                wanderTarget = GetRandomPointInCorral();
            }
            else
            {
                wanderTarget = GetValidWanderTarget();
            }
            currentState = SheepState.Wandering;
        }
    }

    private void HandleWandering()
    {
        if (IsShepherdInRange() && !isCorralled)
        {
            EnterFollowingState();
            return;
        }

        if (isCorralled)
        {
            MoveTowardInCorral(wanderTarget, wanderSpeed);
        }
        else
        {
            MoveToward(wanderTarget, wanderSpeed);
        }

        RotateTowardMovement();

        if ((rb.position - wanderTarget).sqrMagnitude < 0.04f)
        {
            EnterGrazingState();
        }
    }

    private void EnterFollowingState()
    {
        currentState = SheepState.Following;
        followTimer = 0f;
        disengageCheckTimer = disengageCheckInterval;
        isStuckOnFence = false;
        stuckTimer = 0f;
        timeEnteredCorral = -1f;
    }

    private void HandleFollowing()
    {
        if (isCorralled)
        {
            EnterSettlingInCorralState();
            return;
        }

        float distanceToShepherdSqr = (rb.position - (Vector2)shepherd.position).sqrMagnitude;
        if (distanceToShepherdSqr > shepherdDisengageDistanceSqr)
        {
            EnterDisengagingState();
            return;
        }

        followTimer += Time.fixedDeltaTime;
        disengageCheckTimer -= Time.fixedDeltaTime;

        if (followTimer >= minFollowTime && disengageCheckTimer <= 0f)
        {
            disengageCheckTimer = disengageCheckInterval;
            if (Random.value < disengageChancePerCheck)
            {
                EnterDisengagingState();
                return;
            }
        }

        Vector2 directionToShepherd = (shepherd.position - transform.position).normalized;
        float distanceToShepherd = distanceToShepherdSqr;
        if (distanceToShepherd > 0)
        {
            distanceToShepherd = Mathf.Sqrt(distanceToShepherd);
        }
        
        Vector2 followTarget = (Vector2)shepherd.position;
        if (distanceToShepherd > shepherdStopPadding)
        {
            // Don't hit shepherd
            followTarget = (Vector2)shepherd.position - directionToShepherd * shepherdStopPadding;
            MoveToward(followTarget, followSpeed);
        }
        else
        {
            // Stop moving if within the padding distance
            rb.linearVelocity = Vector2.zero;
        }

        RotateToward(shepherd.position);
    }

    private void EnterSettlingInCorralState()
    {
        currentState = SheepState.SettlingInCorral;
        stateTimer = timeToSettleInCorral;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        isStuckOnFence = false;
        stuckTimer = 0f;
    }

    private void HandleSettlingInCorral()
    {
        stateTimer -= Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (stateTimer <= 0f)
        {
            currentState = SheepState.Corralled;
        }
    }

    private void HandleCorralled()
    {
        if (!IsFullyInsideCorral() && isCorralled)
        {
            isCorralled = false;
            gameManager.SheepLeftCorral(this);
            return;
        }

        HandleGrazing();

        if (currentState == SheepState.Wandering)
        {
            if (!corralZone.bounds.Contains(wanderTarget))
            {
                wanderTarget = GetRandomPointInCorral();
            }

            MoveTowardInCorral(wanderTarget, wanderSpeed);
            RotateTowardMovement();

            if ((rb.position - wanderTarget).sqrMagnitude < 0.04f)
            {
                EnterGrazingState();
            }
        }
    }

    private void EnterDisengagingState()
    {
        currentState = SheepState.Disengaging;
        wanderTarget = GetValidWanderTarget();
        stateTimer = Random.Range(1f, 2f);
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

    private void CheckIfStuck()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.0001f &&
            (currentState == SheepState.Wandering || currentState == SheepState.Disengaging))
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > maxStuckTime)
            {
                Vector2 awayFromFence = GetDirectionAwayFromFence();
                if (awayFromFence != Vector2.zero)
                {
                    wanderTarget = rb.position + awayFromFence * wanderMaxDistance;
                    lastValidWanderTarget = wanderTarget;
                    stuckTimer = 0f;
                }
                else
                {
                    EnterGrazingState();
                }

                isStuckOnFence = false;
            }
        }
        else if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            stuckTimer = 0f;
            isStuckOnFence = false;
        }
    }

    private bool IsShepherdInRange()
    {
        if (shepherd == null) return false;
        return (rb.position - (Vector2)shepherd.position).sqrMagnitude <= shepherdFollowDistanceSqr;
    }

    private bool IsFullyInsideCorral()
    {
        if (corralZone == null) return false;
        Bounds corralBounds = corralZone.bounds;
        Bounds sheepBounds = sheepCollider.bounds;
        return corralBounds.Contains(sheepBounds.min) && corralBounds.Contains(sheepBounds.max);
    }

    private Vector2 GetValidWanderTarget()
    {
        for (int i = 0; i < pathfindingAttempts; i++)
        {
            Vector2 target = GetRandomWanderTarget();
            if (!IsFenceBetweenSimple(rb.position, target))
            {
                lastValidWanderTarget = target;
                return target;
            }
        }

        if (!IsFenceBetweenSimple(rb.position, lastValidWanderTarget))
        {
            return lastValidWanderTarget;
        }

        return rb.position;
    }

    private Vector2 GetRandomWanderTarget()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(wanderMinDistance, wanderMaxDistance);
        return rb.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    private Vector2 GetRandomPointInCorral()
    {
        if (corralZone == null) return rb.position;

        Bounds bounds = corralZone.bounds;
        float margin = 0.5f;
        return new Vector2(
            Random.Range(bounds.min.x + margin, bounds.max.x - margin),
            Random.Range(bounds.min.y + margin, bounds.max.y - margin)
        );
    }

    private void MoveToward(Vector2 target, float speed)
    {
        if (IsFenceBetweenSimple(rb.position, target))
        {
            Vector2 avoidanceDirection = GetDirectionAwayFromFence();
            if (avoidanceDirection != Vector2.zero)
            {
                Vector2 alternativeTarget = rb.position + avoidanceDirection * wanderMinDistance;

                if (!IsFenceBetweenSimple(rb.position, alternativeTarget))
                {
                    wanderTarget = alternativeTarget;
                    target = alternativeTarget;
                }
                else
                {
                    EnterGrazingState();
                    return;
                }
            }
            else
            {
                EnterGrazingState();
                return;
            }
        }

        Vector2 direction = (target - rb.position).normalized;
        Vector2 desiredVelocity = direction * speed;
        rb.linearVelocity = Vector2.SmoothDamp(rb.linearVelocity, desiredVelocity, ref velocityRef, 0.2f);
    }

    private void MoveTowardInCorral(Vector2 target, float speed)
    {
        if (!corralZone.bounds.Contains(target))
        {
            target = GetRandomPointInCorral();
        }

        Vector2 direction = (target - rb.position).normalized;
        Vector2 desiredVelocity = direction * speed;
        Vector2 newVelocity = Vector2.SmoothDamp(rb.linearVelocity, desiredVelocity, ref velocityRef, 0.2f);

        Vector2 futurePosition = rb.position + newVelocity * Time.fixedDeltaTime;
        if (corralZone.bounds.Contains(futurePosition))
        {
            rb.linearVelocity = newVelocity;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            wanderTarget = GetRandomPointInCorral();
        }
    }

    private void RotateToward(Vector2 target)
    {
        Vector2 direction = target - rb.position;
        if (direction.sqrMagnitude < 0.000001f) return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetAngle, 0.1f));
    }

    private void RotateTowardMovement()
    {
        Vector2 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.0001f) return;

        float targetAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetAngle, 0.1f));
    }

    private bool IsFenceBetweenSimple(Vector2 from, Vector2 to)
    {
        Vector2 direction = (to - from).normalized;
        float distance = Vector2.Distance(from, to);
        float[] angles = { -20f, 0f, 20f };

        foreach (float angle in angles)
        {
            Vector2 rotatedDirection = Quaternion.Euler(0, 0, angle) * direction;
            RaycastHit2D hit = Physics2D.Raycast(from, rotatedDirection, distance, LayerMask.GetMask("fence"));
            if (hit.collider != null && hit.collider.CompareTag("fence"))
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 GetDirectionAwayFromFence()
    {
        Vector2[] directions = {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
            new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized
        };

        foreach (Vector2 dir in directions)
        {
            Vector2 testPoint = rb.position + dir * (wanderMinDistance * 1.5f);
            if (!IsFenceBetweenSimple(rb.position, testPoint))
            {
                return dir;
            }
        }

        return Vector2.zero;
    }

    private float GetTimeInCorral()
    {
        if (timeEnteredCorral < 0f)
        {
            if (IsFullyInsideCorral())
            {
                timeEnteredCorral = Time.time;
                return 0f;
            }
            else
            {
                return 0f;
            }
        }

        return Time.time - timeEnteredCorral;
    }

    private bool IsCollidingWithOtherSheep()
    {
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(rb.position, sheepDetectionRadius, LayerMask.GetMask("Sheep"));

        foreach (Collider2D col in nearbyColliders)
        {
            if (col == sheepCollider) continue;

            // Check distance to other sheep
            float distanceSqr = (rb.position - (Vector2)col.transform.position).sqrMagnitude;
            if (distanceSqr < sheepCollisionDistanceSqr)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleSheepCollision()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        EnterGrazingState();
        stateTimer = Random.Range(maxGrazingTime, maxGrazingTime * 1.5f);
    }

    private void CheckScreenBounds()
    {
        Vector3 viewportPoint = mainCam.WorldToViewportPoint(transform.position);

        if (viewportPoint.x < -0.1f || viewportPoint.x > 1.1f || viewportPoint.y < -0.1f || viewportPoint.y > 1.1f)
        {
            if (gameManager != null)
            {
                gameManager.SheepLost(this);
            }
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(wanderTarget, 0.15f);

        Gizmos.color = Color.cyan;
        Vector2 forward = rb.linearVelocity.normalized;
        Gizmos.DrawLine(rb.position, rb.position + forward);

        Gizmos.color = Color.red;
        Vector2 direction = (wanderTarget - rb.position).normalized;
        float distance = Vector2.Distance(rb.position, wanderTarget);
        float[] angles = { -20f, 0f, 20f };

        foreach (float angle in angles)
        {
            Vector2 rotated = Quaternion.Euler(0, 0, angle) * direction;
            Gizmos.DrawRay(rb.position, rotated * distance);
        }
    }
}