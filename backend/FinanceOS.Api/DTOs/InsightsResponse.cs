namespace FinanceOS.Api.DTOs;

public class InsightsResponse
{
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net { get; set; }
    public IEnumerable<CategoryBreakdownItem> ByCategory { get; set; } = Array.Empty<CategoryBreakdownItem>();
    public IEnumerable<MonthlyTrendItem> MonthlyTrend { get; set; } = Array.Empty<MonthlyTrendItem>();
}

public class CategoryBreakdownItem
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class MonthlyTrendItem
{
    public string Month { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net { get; set; }
}
