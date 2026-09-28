namespace Memoressa.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Memoressa";
    public string Audience { get; set; } = "MemoressaApp";
    public string SecretKey { get; set; } = "CHANGE_ME_TO_A_LONG_SECRET_KEY_FOR_PRODUCTION";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}
