using System;
using UnityEngine;

/// <summary>Intencoes que o LLM pode escolher. O codigo do jogo decide o que cada uma faz.</summary>
public enum NpcIntent
{
    Neutro,   // segue o jogador (padrao)
    Alerta,   // para de seguir e fica em guarda
    Missao    // guia o jogador ate um ponto (inverte quem segue quem)
}

/// <summary>Formato JSON exigido do LLM. Campos sem acento por causa do JsonUtility.</summary>
[Serializable]
public sealed class NpcReplyJson
{
    public string fala;
    public string intencao;
    public string humor;
}

/// <summary>Resposta ja validada, pronta para ser aplicada no jogo.</summary>
public readonly struct NpcReply
{
    public readonly string Fala;
    public readonly NpcIntent Intent;
    public readonly string Humor;

    public NpcReply(string fala, NpcIntent intent, string humor)
    {
        Fala = fala; Intent = intent; Humor = humor;
    }
}

public static class NpcReplyParser
{
    public static readonly string[] ValidHumors = { "calmo", "desconfiado", "irritado", "tenso", "amigavel" };

    /// <summary>
    /// Valida a resposta do LLM. Retorna false (e nao altera nada) se o JSON estiver
    /// quebrado, faltar campo ou houver valor fora da lista permitida.
    /// </summary>
    public static bool TryParse(string raw, out NpcReply reply, out string error)
    {
        reply = default;
        error = null;

        if (string.IsNullOrWhiteSpace(raw)) { error = "resposta vazia"; return false; }

        // Alguns modelos embrulham o JSON em ```json ... ``` ou texto: pega so o objeto.
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) { error = "sem objeto JSON"; return false; }
        string json = raw.Substring(start, end - start + 1);

        NpcReplyJson data;
        try { data = JsonUtility.FromJson<NpcReplyJson>(json); }
        catch (Exception e) { error = "JSON invalido: " + e.Message; return false; }

        if (data == null) { error = "JSON nulo"; return false; }
        if (string.IsNullOrWhiteSpace(data.fala)) { error = "campo 'fala' ausente"; return false; }
        if (data.fala.Length > 400) { error = "fala longa demais"; return false; }

        if (!TryParseIntent(data.intencao, out NpcIntent intent))
        { error = "intencao invalida: " + data.intencao; return false; }

        string humor = (data.humor ?? "").Trim().ToLowerInvariant();
        if (Array.IndexOf(ValidHumors, humor) < 0)
        { error = "humor invalido: " + data.humor; return false; }

        reply = new NpcReply(data.fala.Trim(), intent, humor);
        return true;
    }

    private static bool TryParseIntent(string value, out NpcIntent intent)
    {
        intent = NpcIntent.Neutro;
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "neutro": intent = NpcIntent.Neutro; return true;
            case "alerta": intent = NpcIntent.Alerta; return true;
            case "missao":
            case "missão": intent = NpcIntent.Missao; return true;
            default: return false;
        }
    }
}
