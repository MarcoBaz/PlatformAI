using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PlatformAI.Core.Logic;
using PlatformAI.Core.Services;
using PlatformAI.Core.Settings;
using PlatformAI.Infrastructure;
using PlatformAI.ML.Services;
using PlatformAI.NLP.Models;
using PlatformAI.NLP.Services;
using System;
using System.IO;

namespace PlatformAI.Tests;

public abstract class BaseTest : IDisposable
{
    protected ServiceProvider _serviceProvider = null!;
    protected ApplicationContext _appContext = null!;
    protected MasterContext _masterContext = null!;
    protected IUnitOfWork _uow = null!;
    protected ILogger<TrainingService> _logger = null!;
    protected IAuthService _authService = null!;
    protected ConversationLogic _conversationLogic = null!;
    protected LLMConfig _llmConfig = null!;
    protected IConfiguration _configuration = null!;

    protected string tenantCode = "TENANT-001";

    protected BaseTest()
    {
        // Carica la configurazione da appsettings.json
        _configuration = new ConfigurationBuilder()
            .SetBasePath(GetProjectRootPath())
            .AddJsonFile("PlatformAI/appsettings.json", optional: false)
            .AddJsonFile("PlatformAI/appsettings.Development.json", optional: true)
            .Build();

        // Carica LLMConfig da appsettings.json
        _llmConfig = new LLMConfig();
        _configuration.GetSection("LLMSettings").Bind(_llmConfig);

        // Configurazione DI per i test
        var services = new ServiceCollection();

        // Registra IConfiguration nel container DI (richiesto da TrainingService e altri)
        services.AddSingleton<IConfiguration>(_configuration);

        // Connection strings: presi da variabili d'ambiente (locale: .runsettings, CI: Azure DevOps secret vars)
        // MAI hardcoded in questo file — usa le var d'ambiente o appsettings.Development.json
        string MasterDatabase =
            Environment.GetEnvironmentVariable("TEST_MASTER_DB")
            ?? _configuration.GetConnectionString("MasterDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'TEST_MASTER_DB' (env var) or 'ConnectionStrings:MasterDatabase' (appsettings) is required.");

        string ApplicationDatabase =
            Environment.GetEnvironmentVariable("TEST_APP_DB")
            ?? _configuration.GetConnectionString("ApplicationDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'TEST_APP_DB' (env var) or 'ConnectionStrings:ApplicationDatabase' (appsettings) is required.");

        services.AddDbContext<MasterContext>(options => options.UseSqlServer(MasterDatabase));
        services.AddDbContext<ApplicationContext>(options =>
            options.UseSqlServer(ApplicationDatabase)
                   .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        // Registrazione DbContextResolver con i context iniettati
        services.AddScoped<IDbContextResolver>(sp =>
        {
            var appCtx = sp.GetRequiredService<ApplicationContext>();
            var masterCtx = sp.GetRequiredService<MasterContext>();
            return new DbContextResolver(masterCtx, appCtx);
        });

        // Registrazione UnitOfWork e repository
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

        // Registrazione IHttpContextAccessor (mock per i test)
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new DefaultHttpContext();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext);
        services.AddSingleton<IHttpContextAccessor>(mockHttpContextAccessor.Object);

        // Configurazione JwtSettings
        var jwtSettings = new JwtSettings
        {
            SecretKey = "TestSecretKeyForJwtTokenGeneration123456789!",
            Issuer = "PlatformAI.Test",
            Audience = "PlatformAI.Test",
            ExpirationMinutes = 60
        };
        services.AddSingleton(Options.Create(jwtSettings));

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddTransient<TrainingService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<UserLogic>();
        services.AddScoped<ConversationLogic>();

        _serviceProvider = services.BuildServiceProvider();

        _appContext = _serviceProvider.GetRequiredService<ApplicationContext>();
        _masterContext = _serviceProvider.GetRequiredService<MasterContext>();

        // Applica automaticamente le migrazioni pendenti sul DB di test
        _appContext.Database.Migrate();

        _uow = _serviceProvider.GetRequiredService<IUnitOfWork>();
        _authService = _serviceProvider.GetRequiredService<IAuthService>();
        _conversationLogic = _serviceProvider.GetRequiredService<ConversationLogic>();
        _logger = _serviceProvider.GetRequiredService<ILogger<TrainingService>>();
    }

    protected void SeedData<T>(params T[] entities) where T : Entity
    {
        if (typeof(T).Namespace?.Contains("Master") == true)
        {
            _masterContext.Set<T>().AddRange(entities);
            _masterContext.SaveChanges();
        }
        else
        {
            _appContext.Set<T>().AddRange(entities);
            _appContext.SaveChanges();
        }
    }

    public virtual void Dispose()
    {
        // Let the service provider dispose all DI-managed objects (contexts, UoW, etc.)
        // Do NOT dispose _appContext, _masterContext, or _uow directly — they are owned
        // by the container. Disposing them first causes ObjectDisposedException when the
        // service provider then tries to dispose UnitOfWork (which accesses those contexts).
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// Trova la root del progetto (dove si trova la solution)
    /// </summary>
    protected string GetProjectRootPath()
    {
        var currentDir = Directory.GetCurrentDirectory();

        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir, "PlatformAI.sln")) ||
                Directory.Exists(Path.Combine(currentDir, "PlatformAI")))
            {
                return currentDir;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".."),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".."),
            "/Users/marcobazzoli/tmp/b2a/Claude/PlatformAI"
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(Path.Combine(fullPath, "PlatformAI")))
            {
                return fullPath;
            }
        }

        throw new DirectoryNotFoundException("Cannot find project root directory");
    }
}
