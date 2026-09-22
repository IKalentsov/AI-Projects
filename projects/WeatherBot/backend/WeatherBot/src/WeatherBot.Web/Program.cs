using Scalar.AspNetCore;
using WeatherBot.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProgramDependencies(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "WeatherBot is running!");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// MVC контроллеры
app.MapControllers();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi(); // /openapi/v1.json
    app.MapScalarApiReference(); // /scalar/v1
}

await app.RunAsync();
