using FinanceOS.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<FinanceDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=finance.db"));

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
