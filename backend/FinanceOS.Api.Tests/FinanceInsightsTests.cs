using FinanceOS.Api.Controllers;
using FinanceOS.Api.Data;
using FinanceOS.Api.DTOs;
using FinanceOS.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FinanceOS.Api.Tests;

public class FinanceInsightsTests
{
    [Fact]
    public async Task AddTransaction_CreatesDefaultAccountAndCategory_WhenIdsAreMissing()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new FinanceDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var httpClientFactory = Mock.Of<IHttpClientFactory>();
        var controller = new FinanceController(context, httpClientFactory);
        var result = await controller.AddTransaction(new CreateTransactionRequest
        {
            Amount = 42.50m,
            Type = TransactionType.Expense,
            Description = "Coffee",
            AccountName = "Checking",
            Institution = "Example Bank",
            CategoryName = "Food"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var transaction = Assert.IsType<Transaction>(created.Value);

        Assert.Equal(1, await context.Transactions.CountAsync());
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Equal(1, await context.Categories.CountAsync());
        Assert.Equal("Checking", (await context.Accounts.SingleAsync()).Name);
        Assert.Equal("Food", (await context.Categories.SingleAsync()).Name);
        Assert.Equal(42.50m, transaction.Amount);
    }

    [Fact]
    public async Task GetAccounts_ReturnsCreatedAccounts()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new FinanceDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        context.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            Name = "Checking",
            Institution = "Example Bank",
            Type = AccountType.Checking,
            Balance = 100m,
            Currency = "USD"
        });
        await context.SaveChangesAsync();

        var httpClientFactory = Mock.Of<IHttpClientFactory>();
        var controller = new FinanceController(context, httpClientFactory);
        var result = await controller.GetAccounts();

        var ok = Assert.IsType<OkObjectResult>(result);
        var accounts = Assert.IsAssignableFrom<IEnumerable<Account>>(ok.Value);
        Assert.Single(accounts);
    }

    [Fact]
    public async Task ExchangePublicToken_PersistsConnection_WhenDemoModeIsUsed()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new FinanceDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var httpClientFactory = Mock.Of<IHttpClientFactory>();
        var controller = new FinanceController(context, httpClientFactory);
        var result = await controller.ExchangePublicToken(new ExchangePublicTokenRequest
        {
            PublicToken = string.Empty,
            InstitutionName = "Demo Bank"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<object>(ok.Value);

        Assert.Equal(1, await context.BankConnections.CountAsync());
        var connection = await context.BankConnections.SingleAsync();
        Assert.Equal("Demo Bank", connection.InstitutionName);
        Assert.Equal("Connected", connection.Status);
    }

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

        var httpClientFactory = Mock.Of<IHttpClientFactory>();
        var controller = new FinanceController(context, httpClientFactory);
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
