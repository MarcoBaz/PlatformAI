using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace PlatformAI.Core.Logging
{
    [ProviderAlias("Database")]
    public class DbLoggerProvider : ILoggerProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, DbLogger> _loggers = new ConcurrentDictionary<string, DbLogger>();

        public DbLoggerProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new DbLogger(name, _serviceProvider));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
