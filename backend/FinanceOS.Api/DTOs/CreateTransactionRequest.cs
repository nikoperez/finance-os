using FinanceOS.Api.Models;

namespace FinanceOS.Api.DTOs;

public class CreateTransactionRequest
{
    public Guid AccountId { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public TransactionType Type { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public string AccountName { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
}
