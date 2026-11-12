using Wayhunter.Api.Contracts.Responses;

var builder = WebApplication.CreateBuilder(args);

// Starter only: M1 must implement real authentication and Admin RBAC.
builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);

// Restrict browser access to explicitly configured localhost origins in development.
if (builder.Environment.IsDevelopment())
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options => options.AddPolicy("LocalDevelopment", p =>
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.UseCors("LocalDevelopment");
app.MapControllers();
app.Run();

// Exposed for future integration tests; not a claim that any BRD flow works.
public partial class Program { }
