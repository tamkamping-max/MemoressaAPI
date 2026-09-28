namespace Memoressa.Infrastructure.Options;

public class InternalApiOptions
{
    public const string SectionName = "InternalApi";

    public string ApiKey { get; set; } = "dev-internal-api-key";
    public int CommandPollBatchSize { get; set; } = 50;
}
