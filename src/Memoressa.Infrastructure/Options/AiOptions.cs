namespace Memoressa.Infrastructure.Options;

public class AiOptions
{
    public const string SectionName = "Ai";

    public string? OpenAiApiKey { get; set; }
    /// <summary>xAI Grok (default). OpenAI-compatible Chat Completions at this base URL.</summary>
    public string OpenAiBaseUrl { get; set; } = "https://api.x.ai/v1";
    /// <summary>Vision / multimodal batch analysis (e.g. grok-2-vision-1212).</summary>
    public string VisionModel { get; set; } = "grok-2-vision-1212";
    /// <summary>AI Agent chat model (e.g. grok-4-3).</summary>
    public string ChatModel { get; set; } = "grok-4-3";
    public bool EnableVisionBatch { get; set; }
    public int MaxBatchSize { get; set; } = 20;
    public int AgentMaxHistoryMessages { get; set; } = 12;
    public int AgentMaxContextMemories { get; set; } = 8;
}
