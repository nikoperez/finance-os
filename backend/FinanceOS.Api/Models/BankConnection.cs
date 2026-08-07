namespace FinanceOS.Api.Models;

public class BankConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "Plaid";
    public string InstitutionName { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string Status { get; set; } = "Connected";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
