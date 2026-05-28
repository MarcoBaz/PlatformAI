using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ML;
using Microsoft.IdentityModel.Tokens;
using PlatformAI.Analytics.Models;
using PlatformAI.Analytics.Services;
using PlatformAI.Core.Logic;
using PlatformAI.Core.Services;
using PlatformAI.Core.Settings;
using PlatformAI.Infrastructure;
using PlatformAI.NLP.Models;
using PlatformAI.NLP.Services;
using PlatformAI.ML.Services;
using PlatformAI.Core.Services;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// 0. SERILOG — console + file con rolling giornaliero
// -----------------------------
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// -----------------------------
// 1. CONFIGURAZIONE JWT SETTINGS
// -----------------------------
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings")
);

// -----------------------------
// 2. AGGIUNTA CONTROLLERS
// -----------------------------
builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer(); 

// -----------------------------
// 3. CORS PER ANGULAR
// -----------------------------
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// -----------------------------
// 4. AUTENTICAZIONE JWT
// -----------------------------
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("JwtSettings");
        var secretKey = jwtSection.GetValue<string>("SecretKey");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSection.GetValue<string>("Issuer"),
            ValidAudience = jwtSection.GetValue<string>("Audience"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

// -----------------------------
// 5. REGISTRAZIONE DEI SERVIZI
// -----------------------------
builder.Services.AddDbContext<MasterContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("MasterDatabase")));
builder.Services.AddDbContext<ApplicationContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("ApplicationDatabase")));

// HttpContextAccessor per accedere ai claims JWT nei servizi
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IDbContextResolver, DbContextResolver>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<UserLogic>();
builder.Services.AddScoped<ConversationLogic>();

// -----------------------------
// 5.1 LLM STREAMING SERVICE (Azure OpenAI)
// -----------------------------
// Carica la configurazione LLM da appsettings.json sezione "LLMSettings"
builder.Services.Configure<LLMConfig>(builder.Configuration.GetSection("LLMSettings"));

// Log della configurazione (senza API key per sicurezza)
var llmSection = builder.Configuration.GetSection("LLMSettings");
Console.WriteLine($"[LLM Config] Provider: {llmSection["Provider"]}");
Console.WriteLine($"[LLM Config] Endpoint: {llmSection["Endpoint"]}");
Console.WriteLine($"[LLM Config] Deployment: {llmSection["DeploymentName"]}");
Console.WriteLine($"[LLM Config] ApiVersion: {llmSection["ApiVersion"]}");
if (llmSection["Provider"] == "AzureAIFoundryAgent")
{
    Console.WriteLine($"[LLM Config] AgentId: {llmSection["AgentId"] ?? "(will create new)"}");
    Console.WriteLine($"[LLM Config] AgentName: {llmSection["AgentName"]}");
}

// Registra HttpClient per LLM con timeout esteso
builder.Services.AddHttpClient<LLMStreamingService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddScoped<LLMStreamingService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<LLMAnalyticsService>();

// -----------------------------
// 6. SWAGGER (NSwag)
// -----------------------------
builder.Services.AddOpenApiDocument(o =>
{
    o.Title = "PlatformAI API";
    o.Version = "v1";
});

var app = builder.Build();

// -----------------------------
// 7. MIDDLEWARE
// -----------------------------
app.UseCors("AllowAngular");

// Static files devono stare prima del routing
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Swagger UI
app.UseOpenApi();
app.UseSwaggerUi();

// API controllers
app.MapControllers();

// Fallback: qualsiasi route non-API restituisce index.html (Angular router)
app.MapFallbackToFile("index.html");

app.Run();
