global using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register DbContext - use SQLite with connection string from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<Omni.Web.Data.AppDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

var app = builder.Build();

// Apply EF Core migrations on startup (dev/demo). This will create the DB if it doesn't exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Omni.Web.Data.AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // In Development, keeping HTTP avoids 307/308 redirects for local clients (e.g. Angular dev server).
    app.UseHttpsRedirection();
}

app.UseCors("LocalAngular");

app.UseAuthorization();

app.MapControllers();

app.Run();
