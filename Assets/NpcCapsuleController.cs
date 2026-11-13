using UnityEngine;

/// <summary>
/// Controla uma cápsula como personagem NPC, com patrulha entre pontos
/// e capacidade de perseguir o jogador quando ele entra no raio de detecção.
/// Requer um componente CharacterController no mesmo GameObject.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class NpcCapsuleController : MonoBehaviour
{
    private enum State
    {
        Patrolling,
        Chasing,
        Talking,
        Idle
    }

    [Header("Referências")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private NpcDialogue dialogue;

    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Patrulha")]
    [SerializeField] private float waypointTolerance = 0.2f;
    [SerializeField] private float waitTimeAtPoint = 1.5f;

    [Header("Detecção do jogador")]
    [SerializeField] private float chaseRadius = 6f;
    [SerializeField] private float stopChaseRadius = 9f;

    [Header("Conversa")]
    [SerializeField] private float talkRadius = 1.8f;
    [SerializeField] private float conversationCooldown = 15f;

    private CharacterController controller;
    private Vector3 velocity;
    private int currentPatrolIndex;
    private float waitTimer;
    private float conversationCooldownTimer;
    private bool hasGreetedThisEncounter;
    private State currentState = State.Patrolling;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (dialogue == null)
            dialogue = GetComponent<NpcDialogue>();
    }

    private void Update()
    {
        ApplyGravity();

        if (conversationCooldownTimer > 0f)
            conversationCooldownTimer -= Time.deltaTime;

        UpdateState();

        switch (currentState)
        {
            case State.Patrolling:
                Patrol();
                break;
            case State.Chasing:
                Chase();
                break;
            case State.Talking:
            case State.Idle:
                break;
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(new Vector3(0f, velocity.y, 0f) * Time.deltaTime);
    }

    private void UpdateState()
    {
        if (player == null)
        {
            currentState = patrolPoints.Length > 0 ? State.Patrolling : State.Idle;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= talkRadius)
        {
            if (currentState != State.Talking)
            {
                FaceTarget(player.position);

                if (!hasGreetedThisEncounter && conversationCooldownTimer <= 0f && dialogue != null)
                {
                    dialogue.StartConversation();
                    hasGreetedThisEncounter = true;
                    conversationCooldownTimer = conversationCooldown;
                }
            }

            currentState = State.Talking;
            return;
        }

        if (distanceToPlayer > talkRadius + 0.5f)
            hasGreetedThisEncounter = false;

        if (currentState == State.Chasing && distanceToPlayer > stopChaseRadius)
        {
            currentState = patrolPoints.Length > 0 ? State.Patrolling : State.Idle;
        }
        else if (currentState != State.Chasing && distanceToPlayer <= chaseRadius)
        {
            currentState = State.Chasing;
        }
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        Transform target = patrolPoints[currentPatrolIndex];
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= waypointTolerance)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTimeAtPoint)
            {
                waitTimer = 0f;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }
            return;
        }

        MoveTowards(target.position);
    }

    private void Chase()
    {
        if (player == null)
            return;

        MoveTowards(player.position);
    }

    private void MoveTowards(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();
        RotateTowards(direction);

        controller.Move(direction * (moveSpeed * Time.deltaTime));
    }

    private void FaceTarget(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();
        RotateTowards(direction);
    }

    private void RotateTowards(Vector3 direction)
    {
        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.y;
        float smoothedAngle = Mathf.LerpAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopChaseRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
