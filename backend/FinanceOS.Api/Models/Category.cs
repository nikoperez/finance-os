namespace FinanceOS.Api.Models;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#4f46e5";
    public bool IsDefault { get; set; }
}
