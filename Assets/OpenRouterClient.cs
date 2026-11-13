using System;
using System.Collections;
using System.IO;
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

[Serializable] internal sealed class OpenRouterResponseFormat
{
    public string type = "json_object";
}

[Serializable] internal sealed class OpenRouterRequest
{
    public string model;
    public ChatMessage[] messages;
    public int max_tokens = 120;
    public float temperature = 0.7f;
    public OpenRouterReasoningOptions reasoning = new OpenRouterReasoningOptions();
    public OpenRouterResponseFormat response_format = new OpenRouterResponseFormat();
}

[Serializable] internal sealed class OpenRouterChoice { public ChatMessage message; }
[Serializable] internal sealed class OpenRouterResponse { public OpenRouterChoice[] choices; }

/// <summary>
/// Cliente da OpenRouter via UnityWebRequest (coroutine, nunca bloqueia o frame).
///
/// SEGURANCA DA CHAVE:
///  - Cole a chave no Inspector SOMENTE durante o Play (valores alterados em Play nao sao salvos na cena).
///  - Alternativas: variavel de ambiente OPENROUTER_API_KEY ou arquivo "openrouter_key.txt"
///    na raiz do projeto (fora de Assets/, listado no .gitignore).
///  - Fora do Play, o campo e limpo automaticamente (OnValidate) para nunca ir para a cena/commit.
/// </summary>
public sealed class OpenRouterClient : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
    [Tooltip("Cole apenas durante o Play. Nunca commite.")]
    [SerializeField] private string apiKey = "";
    [SerializeField] private string model = "meta-llama/llama-3.1-8b-instruct";
    [SerializeField] private int maxTokens = 200;
    [SerializeField, Range(0f, 1f)] private float temperature = 0.7f;
    [SerializeField] private int timeoutSeconds = 20;
    [Tooltip("Pede ao modelo saida em JSON (response_format). Desligue se o modelo nao suportar.")]
    [SerializeField] private bool forceJsonFormat = true;

    private const string EnvVar = "OPENROUTER_API_KEY";
    private const string KeyFile = "openrouter_key.txt";

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Impede que a chave seja salva na cena/prefab fora do Play.
        if (!Application.isPlaying && !string.IsNullOrEmpty(apiKey))
        {
            apiKey = "";
            Debug.Log("[OpenRouterClient] A chave so pode ser preenchida durante o Play (protecao contra commit).");
        }
    }
#endif

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(apiKey)) return apiKey.Trim();

        string env = Environment.GetEnvironmentVariable(EnvVar);
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

        try
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, KeyFile);
            if (File.Exists(path)) return File.ReadAllText(path).Trim();
        }
        catch (Exception) { /* sem arquivo: segue sem chave */ }

        return null;
    }

    public IEnumerator Chat(ChatMessage[] messages, Action<string> onSuccess, Action<string> onError)
    {
        string key = ResolveApiKey();
        if (string.IsNullOrEmpty(key))
        {
            onError?.Invoke("API key ausente (Inspector em Play, env OPENROUTER_API_KEY ou openrouter_key.txt).");
            yield break;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            onError?.Invoke("Sem conexao com a internet.");
            yield break;
        }

        var payload = new OpenRouterRequest
        {
            model = model,
            messages = messages,
            max_tokens = maxTokens,
            temperature = temperature,
            response_format = new OpenRouterResponseFormat { type = forceJsonFormat ? "json_object" : "" },
        };

        string jsonPayload = JsonUtility.ToJson(payload);
        if (!forceJsonFormat)
            jsonPayload = jsonPayload.Replace(",\"response_format\":{\"type\":\"\"}", "");

        using var web = new UnityWebRequest(apiUrl, "POST");
        web.SetRequestHeader("Content-Type", "application/json");
        web.SetRequestHeader("Authorization", "Bearer " + key);
        web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
        web.downloadHandler = new DownloadHandlerBuffer();
        web.timeout = timeoutSeconds;

        yield return web.SendWebRequest();

        if (web.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(web.responseCode + ": " + web.error);
            yield break;
        }

        OpenRouterResponse response;
        try { response = JsonUtility.FromJson<OpenRouterResponse>(web.downloadHandler.text); }
        catch (Exception e) { onError?.Invoke("Resposta ilegivel: " + e.Message); yield break; }

        if (response?.choices == null || response.choices.Length == 0)
        {
            onError?.Invoke("Resposta sem choices.");
            yield break;
        }

        string content = response.choices[0].message?.content;
        if (string.IsNullOrWhiteSpace(content))
        {
            onError?.Invoke("Resposta vazia.");
            yield break;
        }

        onSuccess?.Invoke(content);
    }
}
