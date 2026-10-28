using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Moisson")
    ?? throw new InvalidOperationException(
        "La chaîne de connexion 'Moisson' est absente. Définissez ConnectionStrings__Moisson " +
        "ou utilisez 'dotnet user-secrets' (voir README).");

builder.Services.AddDbContext<MoissonDbContext>(options => options.UseNpgsql(connectionString));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
