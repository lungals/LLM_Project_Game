using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Movimento DETERMINISTICO do NPC via NavMeshAgent (funciona offline).
/// O LLM so escolhe a intencao; este script decide como agir:
///  - Neutro : segue o jogador.
///  - Alerta : para, fica em guarda olhando para o jogador.
///  - Missao : guia o jogador ate guideTarget (inverte quem segue quem).
/// Nunca usa transform.position para mover: tudo passa pelo NavMesh (sem atravessar paredes).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class NpcCapsuleController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform guideTarget;
    [SerializeField] private NpcDialogue dialogue;

    [Header("Seguir")]
    [SerializeField] private float followDistance = 2.2f;
    [SerializeField] private float followSpeed = 3.5f;

    [Header("Guiar (missao)")]
    [SerializeField] private float guideSpeed = 2.8f;
    [Tooltip("Se o jogador ficar mais longe que isso, o NPC espera por ele.")]
    [SerializeField] private float waitForPlayerDistance = 6f;
    [SerializeField] private float arrivalTolerance = 1.2f;

    [Header("Guarda (alerta)")]
    [SerializeField] private float alertDuration = 12f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Conversa")]
    [SerializeField] private float talkRadius = 2.5f;
    [SerializeField] private float conversationCooldown = 15f;

    private NavMeshAgent agent;
    private NpcIntent currentIntent = NpcIntent.Neutro;
    private float alertTimer;
    private float cooldownTimer;
    private bool greetedThisEncounter;

    public NpcIntent CurrentIntent => currentIntent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (dialogue == null) dialogue = GetComponent<NpcDialogue>();
    }

    /// <summary>Chamado pelo NpcDialogue somente com uma resposta JA VALIDADA.</summary>
    public void SetIntent(NpcIntent intent)
    {
        currentIntent = intent;
        if (intent == NpcIntent.Alerta) alertTimer = alertDuration;
        if (intent == NpcIntent.Missao && guideTarget == null)
        {
            Debug.Log("[NPC] Missao sem guideTarget configurado; mantendo neutro.", this);
            currentIntent = NpcIntent.Neutro;
        }
    }

    /// <summary>Estado padrao (usado quando o LLM falha).</summary>
    public void ReturnToDefault() => currentIntent = NpcIntent.Neutro;

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        if (player == null || !agent.isOnNavMesh) return;

        HandleGreeting();

        switch (currentIntent)
        {
            case NpcIntent.Neutro: Follow(); break;
            case NpcIntent.Alerta: Guard(); break;
            case NpcIntent.Missao: Guide(); break;
        }
    }

    private void HandleGreeting()
    {
        float d = Vector3.Distance(transform.position, player.position);
        if (d <= talkRadius)
        {
            if (!greetedThisEncounter && cooldownTimer <= 0f && dialogue != null)
            {
                dialogue.StartConversation();
                greetedThisEncounter = true;
                cooldownTimer = conversationCooldown;
            }
        }
        else if (d > talkRadius + 1f)
        {
            greetedThisEncounter = false;
        }
    }

    private void Follow()
    {
        agent.speed = followSpeed;
        agent.stoppingDistance = followDistance;
        agent.isStopped = false;
        agent.SetDestination(player.position);
        if (agent.remainingDistance <= followDistance) FaceTarget(player.position);
    }

    private void Guard()
    {
        agent.isStopped = true;
        agent.ResetPath();
        FaceTarget(player.position);

        alertTimer -= Time.deltaTime;
        if (alertTimer <= 0f) currentIntent = NpcIntent.Neutro;
    }

    private void Guide()
    {
        agent.speed = guideSpeed;
        agent.stoppingDistance = arrivalTolerance;

        float playerDist = Vector3.Distance(transform.position, player.position);
        if (playerDist > waitForPlayerDistance)
        {
            agent.isStopped = true;           // espera o jogador alcancar
            FaceTarget(player.position);
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(guideTarget.position);

        if (!agent.pathPending && agent.remainingDistance <= arrivalTolerance)
        {
            Debug.Log("[NPC] Chegamos ao destino da missao.", this);
            currentIntent = NpcIntent.Neutro;
        }
    }

    private void FaceTarget(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, talkRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, followDistance);
        if (guideTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, guideTarget.position);
            Gizmos.DrawWireSphere(guideTarget.position, arrivalTolerance);
        }
    }
}
