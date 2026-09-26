namespace Memoressa.Application.Common;

public class OAuthSettings
{
    public const string SectionName = "OAuth";

    public GoogleOAuthSettings Google { get; set; } = new();
    public FacebookOAuthSettings Facebook { get; set; } = new();
    public AppleOAuthSettings Apple { get; set; } = new();
}

public class GoogleOAuthSettings
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

public class FacebookOAuthSettings
{
    public string? AppId { get; set; }
    public string? AppSecret { get; set; }
}

public class AppleOAuthSettings
{
    /// <summary>iOS bundle id or Services ID (JWT aud).</summary>
    public string? ClientId { get; set; }
}
