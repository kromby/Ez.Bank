using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Ez.Bank.FunctionsApi;

public class HealthFunctions
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly ILogger<HealthFunctions> _logger;

    public HealthFunctions(TableServiceClient tableServiceClient, ILogger<HealthFunctions> logger)
    {
        _tableServiceClient = tableServiceClient;
        _logger = logger;
    }

    [Function("Health")]
    public async Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("SystemConfig");
            await tableClient.QueryAsync<TableEntity>(maxPerPage: 1).GetAsyncEnumerator().MoveNextAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                checks = new { tableStorage = "connected" }
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");

            var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await response.WriteAsJsonAsync(new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                checks = new { tableStorage = "disconnected" },
                error = ex.Message
            });
            return response;
        }
    }
}
