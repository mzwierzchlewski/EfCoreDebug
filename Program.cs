using EFCoreDebug;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.SqlServer.Design.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices(
                   services =>
                   {
                       services.AddLogging();
                       services.AddDbContext<ApplicationDbContext>(
                           options =>
                               options.UseSqlServer("Server=ServerAddress;Database=Database;User ID=User;Password=Password;MultipleActiveResultSets=true"));
                       services.AddHostedService<Worker>();
                   }).ConfigureLogging(
                   log =>
                   {
                       log.AddFilter("Microsoft", level => level >= LogLevel.Warning);
                       log.AddConsole();
                   })
               .Build();

await host.RunAsync();

public class Worker : BackgroundService
{
    private readonly IHost _host;

    private readonly ILogger<Worker> _logger;

    private readonly IServiceScopeFactory _serviceScopeFactory;

    public Worker(IServiceScopeFactory serviceScopeFactory, IHost host, ILogger<Worker> logger)
    {
        _host = host;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        ApplyMigration(dbContext);
        GenerateMigration(dbContext);
        
        return _host.StopAsync(stoppingToken);
    }
    
    private void ApplyMigration(DbContext dbContext)
    {
        dbContext.Database.Migrate();
    }
    
    private void GenerateMigration(DbContext dbContext)
    {
        var dbSpecificDesignTimeServices = new SqlServerDesignTimeServices();
        var designTimeServiceCollection = new ServiceCollection().AddEntityFrameworkDesignTimeServices()
                                                                 .AddDbContextDesignTimeServices(dbContext);
        dbSpecificDesignTimeServices.ConfigureDesignTimeServices(designTimeServiceCollection);
        var designTimeServiceProvider = designTimeServiceCollection.BuildServiceProvider();
    
        var scaffolder = designTimeServiceProvider.GetRequiredService<IMigrationsScaffolder>();
        var migration = scaffolder.ScaffoldMigration("Migration", "Migration.Debug");
    
        _logger.LogInformation("{DbContextModelSnapshot}", migration.SnapshotCode);
    }
}
