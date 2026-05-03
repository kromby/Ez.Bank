using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Ez.Bank.Claims.DataAccess;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.FunctionsApi.Auth;
using Ez.Bank.FunctionsApi.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker =>
    {
        worker.UseMiddleware<ExceptionHandlingMiddleware>();
        worker.UseMiddleware<AuthenticationMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        // Auth settings
        var authSettings = new AuthSettings
        {
            Password = config["AUTH_PASSWORD"] ?? throw new InvalidOperationException("AUTH_PASSWORD is required"),
            JwtSecret = config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is required"),
            JwtExpiryMinutes = int.TryParse(config["JWT_EXPIRY_MINUTES"], out var expiry) ? expiry : 60
        };
        services.AddSingleton(authSettings);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Azure Storage
        var storageConnection = config["AZURE_STORAGE_CONNECTION"] ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION is required");
        var tableServiceClient = new TableServiceClient(storageConnection);
        services.AddSingleton(tableServiceClient);

        var blobServiceClient = new BlobServiceClient(storageConnection);
        services.AddSingleton(blobServiceClient);

        // Data Access (Singleton - TableClient is thread-safe)
        services.AddSingleton<IBankDataAccess, BankTableAccess>();
        services.AddSingleton<ICurrencyDataAccess, CurrencyTableAccess>();
        services.AddSingleton<ISystemConfigDataAccess, SystemConfigTableAccess>();
        services.AddSingleton<ICreditorDataAccess, CreditorTableAccess>();
        services.AddSingleton<IDebtorDataAccess, DebtorTableAccess>();
        services.AddSingleton<IClaimDataAccess, ClaimTableAccess>();
        services.AddSingleton<IPaymentDataAccess, PaymentTableAccess>();
        services.AddSingleton<IClaimDocumentDataAccess, ClaimDocumentTableAccess>();

        // Document storage
        var useLocalStorage = bool.TryParse(config["USE_LOCAL_STORAGE"], out var local) && local;
        if (useLocalStorage)
        {
            var localPath = config["LOCAL_STORAGE_PATH"] ?? "./local-storage/claim-documents";
            services.AddSingleton<IDocumentStorage>(new LocalDocumentStorage(localPath));
        }
        else
        {
            var containerName = config["BLOB_CONTAINER_NAME"] ?? "claim-documents";
            services.AddSingleton<IDocumentStorage>(sp => new BlobDocumentStorage(sp.GetRequiredService<BlobServiceClient>(), containerName));
        }

        // Interactors (Scoped)
        services.AddScoped<ClaimInteractor>();
        services.AddScoped<PaymentInteractor>();
        services.AddScoped<DocumentInteractor>();
        services.AddScoped<DueCostInteractor>();

        // Cache
        services.AddMemoryCache();
    })
    .Build();

// Seed reference data
using (var scope = host.Services.CreateScope())
{
    var currencies = scope.ServiceProvider.GetRequiredService<ICurrencyDataAccess>();
    var systemConfig = scope.ServiceProvider.GetRequiredService<ISystemConfigDataAccess>();
    await DataSeeder.SeedAsync(currencies, systemConfig);
}

host.Run();
