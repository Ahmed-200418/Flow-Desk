namespace FlowDesk.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "JwtOptions";

    public string Secret { get; set; } = "SuperSecretKeyForFlowDeskJWTTokenGeneration2026!";
    public string Issuer { get; set; } = "FlowDesk.API";
    public string Audience { get; set; } = "FlowDesk.Clients";
    public int ExpiryMinutes { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
