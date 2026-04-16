using HomeSuite.API.BackgroundServices;
using HomeSuite.Application.Interfaces;
using HomeSuite.Infrastructure.Persistence;
using HomeSuite.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<HomeSuiteDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IMealPlanService, MealPlanService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICatalogPriceCrawlerService, CatalogPriceCrawlerService>();

builder.Services.AddHttpClient<SparPriceCrawlerSource>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-AT,de;q=0.9,en;q=0.8");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36 HomeSuiteCrawler/1.0");
});

builder.Services.AddScoped<IPriceCrawlerSource>(sp =>
    sp.GetRequiredService<BillaPriceCrawlerSource>());

builder.Services.AddScoped<IPriceCrawlerSource>(sp =>
    sp.GetRequiredService<SparPriceCrawlerSource>());

builder.Services.AddHttpClient<BillaPriceCrawlerSource>(client =>
{
    client.BaseAddress = new Uri("https://shop.billa.at/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-AT,de;q=0.9,en;q=0.8");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36 HomeSuiteCrawler/1.0");
});

builder.Services.AddScoped<IPriceCrawlerSource>(sp =>
    sp.GetRequiredService<BillaPriceCrawlerSource>());

builder.Services.AddHostedService<CatalogPriceRefreshWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();
app.Run();
