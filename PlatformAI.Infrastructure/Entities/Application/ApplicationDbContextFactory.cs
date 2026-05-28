using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PlatformAI.Infrastructure;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationContext>
{
    public ApplicationContext CreateDbContext(string[] args)
    {
        var solutionRoot = FindSolutionRoot();

        // 2️⃣ Da lì entra nella cartella del progetto dove si trova appsettings.json
        var basePath = Path.Combine(solutionRoot, "PlatformAI");

        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
            throw new FileNotFoundException("appsettings.json non trovato in " + basePath);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();


        var connectionString = configuration.GetConnectionString("ApplicationDatabase");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationContext(optionsBuilder.Options);
    }
      private static string FindSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();

        while (dir != null)
        {
            if (Directory.EnumerateFiles(dir, "*.sln").Any())
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new Exception("Solution root non trovata.");
    }
}