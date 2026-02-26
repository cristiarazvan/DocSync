using GoogDocsLite.Server.Application.Services;
using GoogDocsLite.Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDocumentAccessService, DocumentAccessService>();
builder.Services.AddScoped<IDocumentLockService, DocumentLockService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    db.Database.Migrate();

    var shouldSeedDemoData = builder.Configuration.GetValue("SeedDemoData", true);
    if (shouldSeedDemoData)
    {
        var logger = loggerFactory.CreateLogger("AppDataSeeder");
        await AppDataSeeder.SeedAsync(db, logger);
    }
}

app.Run();
