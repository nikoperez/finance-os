using FinanceOS.Api.Data;
using FinanceOS.Api.DTOs;
using FinanceOS.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinanceOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinanceController : ControllerBase
{
    private readonly FinanceDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public FinanceController(FinanceDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
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
        var accountId = request.AccountId;
        if (accountId == Guid.Empty)
        {
            var accountName = string.IsNullOrWhiteSpace(request.AccountName) ? "Checking" : request.AccountName.Trim();
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Name == accountName && a.Institution == request.Institution);

            if (account == null)
            {
                account = new Account
                {
                    Id = Guid.NewGuid(),
                    Name = accountName,
                    Institution = request.Institution ?? string.Empty,
                    Type = AccountType.Checking,
                    Balance = 0m,
                    Currency = request.Currency,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
            }

            accountId = account.Id;
        }

        var categoryId = request.CategoryId;
        if (categoryId == Guid.Empty)
        {
            var categoryName = string.IsNullOrWhiteSpace(request.CategoryName) ? "Uncategorized" : request.CategoryName.Trim();
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name == categoryName);

            if (category == null)
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = categoryName,
                    Color = "#4f46e5",
                    IsDefault = true
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }

            categoryId = category.Id;
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Empty,
            AccountId = accountId,
            CategoryId = categoryId,
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

    [HttpDelete("data")]
    public async Task<IActionResult> ClearData()
    {
        _context.Transactions.RemoveRange(_context.Transactions);
        _context.Accounts.RemoveRange(_context.Accounts);
        _context.BankConnections.RemoveRange(_context.BankConnections);
        _context.Categories.RemoveRange(_context.Categories);
        _context.Budgets.RemoveRange(_context.Budgets);
        _context.Users.RemoveRange(_context.Users);
        await _context.SaveChangesAsync();

        return Ok(new { ok = true, message = "All finance data cleared." });
    }

    [HttpDelete("database")]
    public async Task<IActionResult> ResetDatabase()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        return Ok(new { ok = true, message = "Database reset complete." });
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var accounts = await _context.Accounts
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(accounts);
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> AddAccount([FromBody] Account account)
    {
        var newAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Empty,
            Name = string.IsNullOrWhiteSpace(account.Name) ? "Checking" : account.Name.Trim(),
            Institution = account.Institution ?? string.Empty,
            Type = account.Type,
            Balance = account.Balance,
            Currency = string.IsNullOrWhiteSpace(account.Currency) ? "USD" : account.Currency.Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(newAccount);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSummary), new { id = newAccount.Id }, newAccount);
    }

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections()
    {
        var connections = await _context.BankConnections
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(connections);
    }

    [HttpPost("connections/link-token")]
    public async Task<IActionResult> CreateLinkToken()
    {
        var clientId = Environment.GetEnvironmentVariable("PLAID_CLIENT_ID") ?? Environment.GetEnvironmentVariable("PLAUD_CLIENT_ID");
        var secret = Environment.GetEnvironmentVariable("PLAID_SECRET");
        var isConfigured = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(secret);

        if (!isConfigured)
        {
            return Ok(new
            {
                linkToken = Guid.NewGuid().ToString("N"),
                mode = "sandbox",
                note = "Plaid credentials are not configured yet. This demo token lets you test the UI flow locally."
            });
        }

        var requestBody = new
        {
            client_id = clientId,
            secret,
            client_name = "FinanceOS",
            products = new[] { "transactions" },
            country_codes = new[] { "US" },
            language = "en",
            user = new { client_user_id = Guid.NewGuid().ToString("N") },
            webhook = "https://example.com/plaid/webhook"
        };

        var httpClient = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://sandbox.plaid.com/link/token/create");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(502, new { error = payload });
        }

        using var doc = JsonDocument.Parse(payload);
        var linkToken = doc.RootElement.GetProperty("link_token").GetString() ?? string.Empty;

        return Ok(new
        {
            linkToken,
            mode = "sandbox",
            note = "Plaid Link token created successfully."
        });
    }

    [HttpPost("connections/exchange")]
    public async Task<IActionResult> ExchangePublicToken([FromBody] ExchangePublicTokenRequest request)
    {
        var clientId = Environment.GetEnvironmentVariable("PLAID_CLIENT_ID") ?? Environment.GetEnvironmentVariable("PLAUD_CLIENT_ID");
        var secret = Environment.GetEnvironmentVariable("PLAID_SECRET");
        var isConfigured = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(secret);

        if (!isConfigured || string.IsNullOrWhiteSpace(request.PublicToken))
        {
            var demoConnection = new BankConnection
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Empty,
                Provider = "Plaid",
                InstitutionName = string.IsNullOrWhiteSpace(request.InstitutionName) ? "Demo Bank" : request.InstitutionName,
                ItemId = Guid.NewGuid().ToString("N"),
                AccessToken = request.PublicToken,
                Status = "Connected",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.BankConnections.Add(demoConnection);
            await _context.SaveChangesAsync();

            return Ok(new { ok = true, mode = "demo", connection = demoConnection });
        }

        var body = new
        {
            client_id = clientId,
            secret,
            public_token = request.PublicToken
        };

        var httpClient = _httpClientFactory.CreateClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://sandbox.plaid.com/item/public_token/exchange");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(httpRequest);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(502, new { error = payload });
        }

        using var doc = JsonDocument.Parse(payload);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        var itemId = doc.RootElement.GetProperty("item_id").GetString() ?? string.Empty;

        var connection = new BankConnection
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Empty,
            Provider = "Plaid",
            InstitutionName = string.IsNullOrWhiteSpace(request.InstitutionName) ? "Connected Bank" : request.InstitutionName,
            ItemId = itemId,
            AccessToken = accessToken,
            Status = "Connected",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BankConnections.Add(connection);
        await _context.SaveChangesAsync();

        return Ok(new { ok = true, mode = "live", connection });
    }

    [HttpPost("connections")]
    public async Task<IActionResult> AddConnection([FromBody] BankConnection connection)
    {
        var newConnection = new BankConnection
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Empty,
            Provider = string.IsNullOrWhiteSpace(connection.Provider) ? "Plaid" : connection.Provider,
            InstitutionName = connection.InstitutionName,
            ItemId = connection.ItemId,
            AccessToken = connection.AccessToken,
            Status = string.IsNullOrWhiteSpace(connection.Status) ? "Connected" : connection.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BankConnections.Add(newConnection);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetConnections), new { id = newConnection.Id }, newConnection);
    }

    [HttpPost("connections/{connectionId:guid}/sync")]
    public async Task<IActionResult> SyncTransactions([FromRoute] Guid connectionId)
    {
        var connection = await _context.BankConnections.FirstOrDefaultAsync(c => c.Id == connectionId);
        if (connection == null)
        {
            return NotFound(new { error = "Connection not found." });
        }

        if (string.IsNullOrWhiteSpace(connection.AccessToken))
        {
            return BadRequest(new { error = "No Plaid access token is stored for this connection." });
        }

        var clientId = Environment.GetEnvironmentVariable("PLAID_CLIENT_ID") ?? Environment.GetEnvironmentVariable("PLAUD_CLIENT_ID");
        var secret = Environment.GetEnvironmentVariable("PLAID_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
        {
            return BadRequest(new { error = "Plaid credentials are not configured." });
        }

        var requestBody = new
        {
            client_id = clientId,
            secret,
            access_token = connection.AccessToken,
            cursor = string.Empty,
            count = 100
        };

        var httpClient = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://sandbox.plaid.com/transactions/sync");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(502, new { error = payload });
        }

        using var doc = JsonDocument.Parse(payload);
        var added = doc.RootElement.GetProperty("added");
        var importedCount = 0;

        foreach (var item in added.EnumerateArray())
        {
            var externalId = item.GetProperty("transaction_id").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(externalId))
            {
                continue;
            }

            var exists = await _context.Transactions.AnyAsync(t => t.ExternalId == externalId);
            if (exists)
            {
                continue;
            }

            var accountName = string.IsNullOrWhiteSpace(connection.InstitutionName)
                ? "Plaid Account"
                : connection.InstitutionName;

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Institution == connection.InstitutionName && a.Name == accountName);
            if (account == null)
            {
                account = new Account
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Empty,
                    Name = accountName,
                    Institution = connection.InstitutionName,
                    Type = AccountType.Checking,
                    Balance = 0m,
                    Currency = "USD",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
            }

            var categoryName = item.TryGetProperty("category", out var categoryProp) && categoryProp.ValueKind == JsonValueKind.Array && categoryProp.GetArrayLength() > 0
                ? categoryProp[0].GetString() ?? "Imported"
                : "Imported";

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == categoryName);
            if (category == null)
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = categoryName,
                    Color = "#4f46e5",
                    IsDefault = true
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }

            var amount = item.TryGetProperty("amount", out var amountProp) ? amountProp.GetDecimal() : 0m;
            var transactionType = amount < 0 ? TransactionType.Income : TransactionType.Expense;
            var description = item.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                ? nameProp.GetString()!
                : (item.TryGetProperty("merchant_name", out var merchantProp) ? merchantProp.GetString() ?? "Imported transaction" : "Imported transaction");
            var merchant = item.TryGetProperty("merchant_name", out var merchantNameProp) ? merchantNameProp.GetString() ?? string.Empty : string.Empty;
            var occurredAt = item.TryGetProperty("date", out var dateProp) && DateTime.TryParse(dateProp.GetString(), out var parsedDate)
                ? parsedDate
                : DateTime.UtcNow;

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Empty,
                AccountId = account.Id,
                CategoryId = category.Id,
                ExternalId = externalId,
                Source = "Plaid",
                Amount = Math.Abs(amount),
                Type = transactionType,
                Description = description,
                Merchant = merchant,
                Notes = "Imported from Plaid",
                Currency = "USD",
                OccurredAt = occurredAt,
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            importedCount++;
        }

        if (importedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new { importedCount, connectionId });
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
