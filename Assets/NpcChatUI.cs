using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI básica de chat (UI Toolkit) conectada ao NpcDialogue.
/// Requer um componente UIDocument no mesmo GameObject, usando
/// o UXML "NewUXMLTemplate" (ou equivalente com os mesmos nomes de elementos).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class NpcChatUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private NpcDialogue dialogue;
    [SerializeField] private string npcDisplayName = "NPC";

    private UIDocument uiDocument;
    private VisualElement root;
    private VisualElement chatRoot;
    private VisualElement chatLog;
    private ScrollView chatScroll;
    private TextField chatInput;
    private Button sendButton;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        chatRoot = root.Q<VisualElement>("chat-root");
        chatLog = root.Q<VisualElement>("chat-log");
        chatScroll = root.Q<ScrollView>("chat-scroll");
        chatInput = root.Q<TextField>("chat-input");
        sendButton = root.Q<Button>("chat-send-button");

        var nameLabel = root.Q<Label>("npc-name-label");
        if (nameLabel != null)
            nameLabel.text = npcDisplayName;

        sendButton.clicked += OnSendClicked;
        chatInput.RegisterCallback<KeyDownEvent>(OnInputKeyDown);

        CloseChat();
    }

    private void OnDestroy()
    {
        sendButton.clicked -= OnSendClicked;
    }

    private void OnInputKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            OnSendClicked();
    }

    /// <summary>
    /// Abre a janela de chat. Chame isso quando o NPC iniciar uma conversa.
    /// </summary>
    public void OpenChat()
    {
        chatRoot.style.display = DisplayStyle.Flex;
        chatInput.Focus();
    }

    public void CloseChat()
    {
        chatRoot.style.display = DisplayStyle.None;
    }

    private void OnSendClicked()
    {
        string message = chatInput.value;
        if (string.IsNullOrWhiteSpace(message))
            return;

        AddLine(message, isPlayer: true);
        chatInput.value = string.Empty;
        chatInput.Focus();

        dialogue.Ask(message);
    }

    /// <summary>
    /// Conecte este método ao evento onReply do NpcDialogue (UnityEvent&lt;string&gt;).
    /// </summary>
    public void OnNpcReply(string reply)
    {
        AddLine(reply, isPlayer: false);
    }

    private void AddLine(string text, bool isPlayer)
    {
        var label = new Label((isPlayer ? "Você: " : npcDisplayName + ": ") + text);
        label.AddToClassList("chat-line");
        label.AddToClassList(isPlayer ? "chat-line-player" : "chat-line-npc");
        chatLog.Add(label);

        chatScroll.schedule.Execute(() => chatScroll.scrollOffset = new Vector2(0f, float.MaxValue));
    }
}
