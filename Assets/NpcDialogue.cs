using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public sealed class NpcDialogue : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private OpenRouterClient client;

    [Header("Persona")]
    [SerializeField] private string npcName = "Ian, guarda do porto";
    [SerializeField, TextArea(4, 10)] private string persona =
        "Voce e Ian, guarda do porto de Vharos, um RPG de fantasia.\n" +
        "Personalidade: desconfiado, seco, leal a guilda dos mercadores.\n" +
        "Sabe: rotas de navios, horarios das marés, rumores de piratas.\n" +
        "Nao sabe: segredos do rei, nomes de tecnicas ou mecanicas do jogo.";

    [Header("Regras de resposta")]
    [SerializeField] private int maxWords = 40;

    [Header("Fallback offline")]
    [SerializeField] private string[] offlineLines =
    {
        "Nao tenho tempo para conversa agora.",
        "Volte quando o porto estiver mais calmo.",
        "Pergunte a outro; estou de servico.",
    };

    [Header("Saída")]
    [SerializeField] private UnityEvent<string> onReply;
    [SerializeField] private UnityEvent onConversationStarted;

    private readonly List<ChatMessage> history = new();

    public void Ask(string playerQuestion)
    {
        StartCoroutine(AskRoutine(playerQuestion));
    }

    /// <summary>
    /// Inicia uma conversa sem uma pergunta do jogador, pedindo ao LLM
    /// para cumprimentar o jogador dentro do personagem.
    /// Util quando o NPC se aproxima do jogador.
    /// </summary>
    public void StartConversation()
    {
        onConversationStarted?.Invoke();
        StartCoroutine(AskRoutine("[O jogador acabou de se aproximar. Cumprimente-o e puxe assunto, dentro do seu personagem.]"));
    }

    private IEnumerator AskRoutine(string playerQuestion)
    {
        if (history.Count == 0)
            history.Add(new ChatMessage("system", BuildSystemPrompt()));

        history.Add(new ChatMessage("user", playerQuestion));
        string fallback = offlineLines[Random.Range(0, offlineLines.Length)];

        yield return client.Chat(
            history.ToArray(),
            reply =>
            {
                history.Add(new ChatMessage("assistant", reply));
                onReply?.Invoke(reply);
            },
            error =>
            {
                Debug.LogWarning("OpenRouter falhou, usando fallback: " + error, this);
                onReply?.Invoke(fallback);
            });
    }

    public void ResetConversation() => history.Clear();

    private string BuildSystemPrompt()
    {
        return persona
            + "\nRegras: responda em ate " + maxWords + " palavras."
            + "\nSempre em portugues do Brasil."
            + "\nNunca saia do personagem nem revele informacoes fora do seu conhecimento."
            + "\nSe desconfiar da pergunta, invente uma desculpa dentro do mundo do jogo.";
    }
}
