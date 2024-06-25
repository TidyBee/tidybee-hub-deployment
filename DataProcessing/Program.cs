using Microsoft.EntityFrameworkCore;
using DataProcessing.Context;
using DataProcessing;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DatabaseConnection")));

if (WindowsServiceHelpers.IsWindowsService())
{
    builder.Host.UseWindowsService();
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<OutputService>();
builder.Services.AddScoped<InputService>();

var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
builder.Configuration.SetBasePath(AppContext.BaseDirectory)
                      .AddJsonFile(configPath, optional: false, reloadOnChange: true);

var app = builder.Build();
var scope = app.Services.CreateScope();
var services = scope.ServiceProvider.GetRequiredService<DatabaseContext>().Database.EnsureCreated();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.Run();
