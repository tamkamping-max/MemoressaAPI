namespace Memoressa.Infrastructure.Options;

public class AiOptions
{
    public const string SectionName = "Ai";

    public string? GrokApiKey { get; set; }
    public string GrokBaseUrl { get; set; } = "https://api.x.ai/v1";
    /// <summary>AI Agent chat (default grok-4.7).</summary>
    public string ChatModel { get; set; } = "grok-4.7";
    /// <summary>Batch photo vision / tagging (default grok-4.7).</summary>
    public string VisionModel { get; set; } = "grok-4.7";
    public bool EnableVisionBatch { get; set; }
    public int MaxBatchSize { get; set; } = 20;
    public int AgentMaxHistoryMessages { get; set; } = 6;
    public int AgentMaxContextMemories { get; set; } = 4;

    /// <summary>Use Grok to expand Agent search keywords across locales before DB substring search (adds one Grok call per chat).</summary>
    public bool AgentGrokSearchExpansion { get; set; } = false;

    public int AgentMaxSearchKeywords { get; set; } = 16;

    /// <summary>Max synonym/keyword strings per DB search (first term is always the user query).</summary>
    public int AgentMaxSearchTermsForDb { get; set; } = 4;

    /// <summary>Cap Grok completion tokens for Agent chat JSON.</summary>
    public int AgentMaxCompletionTokens { get; set; } = 320;
}
