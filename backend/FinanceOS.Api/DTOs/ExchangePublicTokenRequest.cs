namespace FinanceOS.Api.DTOs;

public class ExchangePublicTokenRequest
{
    public string PublicToken { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
}
