using System.Text;

namespace CryptoBook.AI;

/// <summary>Text-only Gemma 4 turns. The native template API cannot render its Jinja template.</summary>
public static class AiChatFormat
{
    public static IReadOnlyList<AiTurn> WithImagesInLatestTurn(IReadOnlyList<AiTurn> conversation)
    {
        // Re-send image snapshots with the current question; the visible chat history stays intact.
        var images = conversation.SelectMany(turn => turn.Attachments ?? []).Where(file => file.IsImage).ToArray();
        int latest = -1;
        for(int i = 0; i < conversation.Count; i++) if(conversation[i].Role == "user") latest = i;
        if(images.Length == 0 || latest < 0) return conversation;
        return conversation.Select((turn, index) => turn with
        {
            Attachments = index == latest ? [.. turn.Attachments?.Where(file => !file.IsImage) ?? [], .. images]
                : turn.Attachments?.Where(file => !file.IsImage).ToArray()
        }).ToArray();
    }

    public static string Content(AiTurn turn, string mediaMarker = "<media>")
    {
        var result = new StringBuilder(turn.Content);
        foreach(var file in turn.Attachments ?? [])
        {
            result.Append("\n\n[Attached file: ").Append(file.Name).Append("]\n");
            result.Append(file.IsImage ? mediaMarker : file.Text);
        }
        return result.ToString();
    }

    public static string Gemma4(AiModelSettings settings, IReadOnlyList<AiTurn> conversation, string mediaMarker = "<media>")
    {
        var prompt = new StringBuilder();
        if(settings.UseReasoning || !string.IsNullOrWhiteSpace(settings.SystemPrompt))
        {
            prompt.Append("<|turn>system\n");
            if(settings.UseReasoning) prompt.Append("<|think|>\n");
            prompt.Append(settings.SystemPrompt.Trim()).Append("<turn|>\n");
        }
        foreach(var turn in conversation)
            prompt.Append("<|turn>").Append(turn.Role == "assistant" ? "model" : turn.Role)
                .Append('\n').Append(Content(turn, mediaMarker).Trim()).Append("<turn|>\n");
        prompt.Append("<|turn>model\n");
        if(!settings.UseReasoning) prompt.Append("<|channel>thought\n<channel|>");
        return prompt.ToString();
    }
}
