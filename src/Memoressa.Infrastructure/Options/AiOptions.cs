namespace Memoressa.Infrastructure.Options;

public class AiOptions
{
    public const string SectionName = "Ai";

    public string? GrokApiKey { get; set; }
    public string GrokBaseUrl { get; set; } = "https://api.x.ai/v1";
    /// <summary>AI Agent chat (default grok-4-7).</summary>
    public string ChatModel { get; set; } = "grok-4-7";
    /// <summary>Batch photo vision / tagging (default grok-4-7).</summary>
    public string VisionModel { get; set; } = "grok-4-7";
    public bool EnableVisionBatch { get; set; }
    public int MaxBatchSize { get; set; } = 20;
    public int AgentMaxHistoryMessages { get; set; } = 12;
    public int AgentMaxContextMemories { get; set; } = 8;

    /// <summary>Vision input USD per 1M tokens (default grok-4.7 under 200k prompt tier).</summary>
    public decimal VisionInputUsdPerMillionTokens { get; set; } = 2.0m;

    /// <summary>Vision output USD per 1M tokens (default grok-4.7 under 200k prompt tier).</summary>
    public decimal VisionOutputUsdPerMillionTokens { get; set; } = 6.0m;
}
