using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;


namespace PlatformAI.Infrastructure;

public class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterContext>
{
    public MasterContext CreateDbContext(string[] args)
    {
        // 1️⃣ Trova la root della solution (directory che contiene il .sln)
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

        var connectionString = configuration.GetConnectionString("MasterDatabase");

        var optionsBuilder = new DbContextOptionsBuilder<MasterContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new MasterContext(optionsBuilder.Options);
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
