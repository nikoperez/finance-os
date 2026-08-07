using FinanceOS.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var envFilePath = Path.Combine(builder.Environment.ContentRootPath, ".env");
if (File.Exists(envFilePath))
{
    foreach (var line in File.ReadAllLines(envFilePath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
        {
            continue;
        }

        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = trimmed[..separatorIndex].Trim();
        var value = trimmed[(separatorIndex + 1)..].Trim();

        if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            value = value[1..^1];
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}

var dbPath = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(dbPath))
{
    var appDbPath = Path.Combine(builder.Environment.ContentRootPath, "finance.db");
    dbPath = $"Data Source={appDbPath}";
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddDbContext<FinanceDbContext>(options =>
    options.UseSqlite(dbPath));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");

app.MapGet("/", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(webRoot, "index.html"));
});

app.MapGet("/summary/", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(webRoot, "summary", "index.html"));
});

app.MapGet("/transactions/", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(webRoot, "transactions", "index.html"));
});

app.MapGet("/insights/", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(webRoot, "insights", "index.html"));
});

app.Run();
