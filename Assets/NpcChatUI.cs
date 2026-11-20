using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI de chat (UI Toolkit) conectada ao NpcDialogue, com feedback visual da intencao:
/// um label "intent-label" (criado automaticamente se nao existir no UXML) mostra
/// icone + texto, e a borda do chat e a cor de fundo da camera mudam conforme a intencao.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class NpcChatUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private NpcDialogue dialogue;
    [SerializeField] private string npcDisplayName = "Ian";
    [SerializeField] private Camera targetCamera;

    [Header("Cores por intencao")]
    [SerializeField] private Color neutroColor = new(0.30f, 0.65f, 0.35f);
    [SerializeField] private Color alertaColor = new(0.85f, 0.20f, 0.20f);
    [SerializeField] private Color missaoColor = new(0.95f, 0.70f, 0.15f);
    [SerializeField] private Color offlineColor = new(0.5f, 0.5f, 0.5f);

    private UIDocument uiDocument;
    private VisualElement root, chatRoot, chatLog;
    private ScrollView chatScroll;
    private TextField chatInput;
    private Button sendButton;
    private Label intentLabel;
    private Label typingLabel;
    private Color originalCameraColor;

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
        if (nameLabel != null) nameLabel.text = npcDisplayName;

        intentLabel = root.Q<Label>("intent-label");
        if (intentLabel == null)
        {
            intentLabel = new Label { name = "intent-label" };
            intentLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            intentLabel.style.paddingLeft = 6;
            intentLabel.style.paddingRight = 6;
            intentLabel.style.paddingTop = 2;
            intentLabel.style.paddingBottom = 2;
            intentLabel.style.marginBottom = 4;
            intentLabel.style.color = Color.black;
            (chatRoot ?? root).Insert(0, intentLabel);
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null) originalCameraColor = targetCamera.backgroundColor;

        if (dialogue != null)
        {
            dialogue.OnReply.AddListener(OnNpcReply);
            dialogue.OnIntentChanged.AddListener(OnIntentChanged);
        }

        if (sendButton != null) sendButton.clicked += OnSendClicked;
        chatInput?.RegisterCallback<KeyDownEvent>(OnInputKeyDown);

        OnIntentChanged(NpcIntent.Neutro, "desconfiado");
        CloseChat();
    }

    private void OnDestroy()
    {
        if (sendButton != null) sendButton.clicked -= OnSendClicked;
        if (dialogue != null)
        {
            dialogue.OnReply.RemoveListener(OnNpcReply);
            dialogue.OnIntentChanged.RemoveListener(OnIntentChanged);
        }
    }

    private void OnInputKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            OnSendClicked();
    }

    public void OpenChat()
    {
        if (chatRoot == null) return;
        chatRoot.style.display = DisplayStyle.Flex;
        chatInput?.Focus();
    }

    public void CloseChat()
    {
        if (chatRoot != null) chatRoot.style.display = DisplayStyle.None;
    }

    private void OnSendClicked()
    {
        if (chatInput == null || dialogue == null) return;
        string message = chatInput.value;
        if (string.IsNullOrWhiteSpace(message) || dialogue.IsBusy) return;

        AddLine(message, isPlayer: true);
        chatInput.value = string.Empty;
        chatInput.Focus();
        ShowTyping(true);

        dialogue.Ask(message);
    }

    public void OnNpcReply(string reply)
    {
        ShowTyping(false);
        OpenChat();
        AddLine(reply, isPlayer: false);
    }

    private void OnIntentChanged(NpcIntent intent, string humor)
    {
        Color c; string icon, text;
        bool offline = humor == "offline";
        switch (intent)
        {
            case NpcIntent.Alerta: c = alertaColor; icon = "\u26A0"; text = "ALERTA - em guarda"; break;
            case NpcIntent.Missao: c = missaoColor; icon = "\u2691"; text = "MISSAO - guiando voce"; break;
            default:               c = neutroColor; icon = "\u25CF"; text = "Neutro - seguindo"; break;
        }
        // Offline: mantem a intencao atual (ultima valida) e apenas avisa, com cor cinza misturada.
        if (offline) c = Color.Lerp(c, offlineColor, 0.6f);

        intentLabel.text = icon + "  " + text + (offline ? "  (offline - ultima intencao valida)" : "  (humor: " + humor + ")");
        intentLabel.style.backgroundColor = c;

        if (chatRoot != null)
        {
            chatRoot.style.borderTopColor = c;
            chatRoot.style.borderBottomColor = c;
            chatRoot.style.borderLeftColor = c;
            chatRoot.style.borderRightColor = c;
            chatRoot.style.borderTopWidth = 3;
            chatRoot.style.borderBottomWidth = 3;
            chatRoot.style.borderLeftWidth = 3;
            chatRoot.style.borderRightWidth = 3;
        }

        if (targetCamera != null)
            targetCamera.backgroundColor = intent == NpcIntent.Alerta
                ? Color.Lerp(originalCameraColor, alertaColor, 0.5f)
                : originalCameraColor;
    }

    private void ShowTyping(bool show)
    {
        if (chatLog == null) return;
        if (show)
        {
            typingLabel ??= new Label(npcDisplayName + " esta pensando...");
            typingLabel.AddToClassList("chat-line");
            if (typingLabel.parent == null) chatLog.Add(typingLabel);
        }
        else typingLabel?.RemoveFromHierarchy();
    }

    private void AddLine(string text, bool isPlayer)
    {
        if (chatLog == null) return;
        var label = new Label((isPlayer ? "Voce: " : npcDisplayName + ": ") + text);
        label.AddToClassList("chat-line");
        label.AddToClassList(isPlayer ? "chat-line-player" : "chat-line-npc");
        chatLog.Add(label);
        chatScroll?.schedule.Execute(() => chatScroll.scrollOffset = new Vector2(0f, float.MaxValue));
    }
}
