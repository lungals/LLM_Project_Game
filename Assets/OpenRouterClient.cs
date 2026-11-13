using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class ChatMessage
{
    public string role;
    public string content;

    public ChatMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }
}

[Serializable] internal sealed class OpenRouterReasoningOptions
{
    public bool exclude = true;
    public string effort = "low";
}

[Serializable] internal sealed class OpenRouterRequest
{
    public string model;
    public ChatMessage[] messages;
    public int max_tokens = 120;
    public float temperature = 0.7f;
    public OpenRouterReasoningOptions reasoning = new OpenRouterReasoningOptions();
}

[Serializable] internal sealed class OpenRouterChoice
{
    public ChatMessage message;
}

[Serializable] internal sealed class OpenRouterResponse
{
    public OpenRouterChoice[] choices;
}

public sealed class OpenRouterClient : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
    [SerializeField] private string apiKey = "";
    [SerializeField] private string model = "meta-llama/llama-3.1-8b-instruct";
    [SerializeField] private int maxTokens = 300;
    [SerializeField, Range(0f, 1f)] private float temperature = 0.7f;

    public IEnumerator Chat(ChatMessage[] messages, Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("API key vazia: preencha no Inspector (apenas para teste em sala).");
            yield break;
        }

        OpenRouterRequest payload = new OpenRouterRequest
        {
            model = model,
            messages = messages,
            max_tokens = maxTokens,
            temperature = temperature,
        };

        string jsonPayload = JsonUtility.ToJson(payload);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);

        using UnityWebRequest web = new UnityWebRequest(apiUrl, "POST");
        web.SetRequestHeader("Content-Type", "application/json");
        web.SetRequestHeader("Authorization", "Bearer " + apiKey);
        web.uploadHandler = new UploadHandlerRaw(jsonBytes);
        web.downloadHandler = new DownloadHandlerBuffer();
        web.timeout = 30;

        yield return web.SendWebRequest();

        if (web.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(web.responseCode + ": " + web.error);
            yield break;
        }

        OpenRouterResponse response = JsonUtility.FromJson<OpenRouterResponse>(web.downloadHandler.text);
        if (response?.choices == null || response.choices.Length == 0)
        {
            onError?.Invoke("Resposta sem choices.");
            yield break;
        }

        string content = response.choices[0].message?.content;
        if (string.IsNullOrEmpty(content))
        {
            onError?.Invoke("Resposta vazia (possivelmente cortada por max_tokens ou modelo so retornou reasoning).");
            yield break;
        }

        onSuccess?.Invoke(content);
    }
}
