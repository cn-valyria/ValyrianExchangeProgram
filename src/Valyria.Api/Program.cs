using Valyria.Api;
using Valyria.Api.Auth;
using Valyria.Api.Extensions;
using Valyria.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (includes telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Valyria API", Version = "v1" });
});

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add Infrastructure (repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add Authentication services
builder.Services.AddAuthenticationServices();

// Register authorization filter
builder.Services.AddScoped<ApiAuthorizeFilter>();

// Add CORS if needed
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Map Aspire default endpoints (health checks, etc.)
app.MapDefaultEndpoints();

// Enable Swagger in all environments when running through Aspire
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Valyria API v1");
    c.RoutePrefix = "swagger";
});

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
