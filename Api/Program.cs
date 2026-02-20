using Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var localCORS = "_localCORS";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: localCORS, builder =>
    {
        builder.WithOrigins("http://localhost:5173") // URL con puertos Vite/Vue
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDBContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(localCORS);

app.UseHttpsRedirection();

app.MapControllers();

app.Run();