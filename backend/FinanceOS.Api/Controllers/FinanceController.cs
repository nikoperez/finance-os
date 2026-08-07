using FinanceOS.Api.Data;
using FinanceOS.Api.DTOs;
using FinanceOS.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinanceController : ControllerBase
{
    private readonly FinanceDbContext _context;

    public FinanceController(FinanceDbContext context)
    {
        _context = context;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var income = await _context.Transactions
            .Where(t => t.Type == TransactionType.Income)
            .SumAsync(t => t.Amount);
        var expenses = await _context.Transactions
            .Where(t => t.Type == TransactionType.Expense)
            .SumAsync(t => t.Amount);
        var net = income - expenses;

        return Ok(new
        {
            income,
            expenses,
            net,
            transactionCount = await _context.Transactions.CountAsync(),
            accountCount = await _context.Accounts.CountAsync()
        });
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> AddTransaction(CreateTransactionRequest request)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Empty,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            Type = request.Type,
            Description = request.Description,
            Merchant = request.Merchant,
            Notes = request.Notes,
            Currency = request.Currency,
            OccurredAt = request.OccurredAt,
            CreatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSummary), new { id = transaction.Id }, transaction);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions()
    {
        var transactions = await _context.Transactions.OrderByDescending(t => t.OccurredAt).ToListAsync();
        return Ok(transactions);
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> AddAccount(Account account)
    {
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSummary), new { id = account.Id }, account);
    }

    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var from = startDate ?? DateTime.UtcNow.AddMonths(-3).Date;
        var to = endDate ?? DateTime.UtcNow.Date;

        var transactions = await _context.Transactions
            .Where(t => t.OccurredAt >= from && t.OccurredAt <= to)
            .ToListAsync();

        var income = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        var net = income - expenses;

        var byCategory = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategoryBreakdownItem
            {
                Category = g.First().CategoryId.ToString(),
                Amount = g.Sum(t => t.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var monthlyTrend = transactions
            .GroupBy(t => new { t.OccurredAt.Year, t.OccurredAt.Month })
            .Select(g => new MonthlyTrendItem
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM"),
                Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
                Net = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount) - g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
            })
            .OrderBy(x => x.Month)
            .ToList();

        return Ok(new InsightsResponse
        {
            Income = income,
            Expenses = expenses,
            Net = net,
            ByCategory = byCategory,
            MonthlyTrend = monthlyTrend
        });
    }
}
