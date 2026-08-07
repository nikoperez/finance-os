using FinanceOS.Api.Models;

namespace FinanceOS.Api.Models;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Guid CategoryId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Source { get; set; } = "Manual";
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}