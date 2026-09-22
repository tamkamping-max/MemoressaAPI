namespace Memoressa.Infrastructure.Options;

public class AiOptions
{
    public const string SectionName = "Ai";

    public string? OpenAiApiKey { get; set; }
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string VisionModel { get; set; } = "gpt-4o-mini";
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public bool EnableVisionBatch { get; set; }
    public int MaxBatchSize { get; set; } = 20;
    public int AgentMaxHistoryMessages { get; set; } = 12;
    public int AgentMaxContextMemories { get; set; } = 8;
}
