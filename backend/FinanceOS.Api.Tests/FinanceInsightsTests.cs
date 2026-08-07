using FinanceOS.Api.Controllers;
using FinanceOS.Api.Data;
using FinanceOS.Api.DTOs;
using FinanceOS.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceOS.Api.Tests;

public class FinanceInsightsTests
{
    [Fact]
    public async Task GetInsights_ReturnsMonthlySummaryAndCategoryBreakdown()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new FinanceDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        context.Transactions.AddRange(
            new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                CategoryId = Guid.NewGuid(),
                Amount = 1000m,
                Type = TransactionType.Income,
                Description = "Salary",
                OccurredAt = new DateTime(2026, 8, 1),
                CreatedAt = new DateTime(2026, 8, 1)
            },
            new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                CategoryId = Guid.NewGuid(),
                Amount = 250m,
                Type = TransactionType.Expense,
                Description = "Groceries",
                OccurredAt = new DateTime(2026, 8, 3),
                CreatedAt = new DateTime(2026, 8, 3)
            });

        await context.SaveChangesAsync();

        var controller = new FinanceController(context);
        var result = await controller.GetInsights(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InsightsResponse>(ok.Value);

        Assert.Equal(1000m, response.Income);
        Assert.Equal(250m, response.Expenses);
        Assert.Equal(750m, response.Net);
        Assert.Single(response.ByCategory);
        Assert.Single(response.MonthlyTrend);
    }
}
