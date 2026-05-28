using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using System;

namespace PlatformAI.Core.Logging
{
    public class DbLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IServiceProvider _serviceProvider;

        public DbLogger(string categoryName, IServiceProvider serviceProvider)
        {
            _categoryName = categoryName;
            _serviceProvider = serviceProvider;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel)
        {
            // Logga tutti i livelli da Information in su
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            // Crea un nuovo scope per risolvere le dipendenze scoped come IUnitOfWork
            using (var scope = _serviceProvider.CreateScope())
            {
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var logEntry = new Log
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    Level = logLevel.ToString(),
                    LoggerName = _categoryName,
                    Message = formatter(state, exception),
                    Exception = exception?.Message,
                    StackTrace = exception?.StackTrace,
                    CreateDate = DateTime.UtcNow,
                    UserCreate = "System", // Ottenere l'utente corrente se disponibile
                    LastModifiedDate = DateTime.UtcNow,
                    UserModify = "System" // Ottenere l'utente corrente se disponibile
                };

                var logRepository = uow.Repository<Log>();
                logRepository.AddAsync(logEntry);
                uow.CommitAsync().GetAwaiter().GetResult(); // Salva immediatamente il log
            }
        }
    }
}
