using Azure;
using Azure.Data.Tables;

namespace Ez.Bank.Claims.DataAccess;

public abstract class TableAccessBase
{
    protected readonly TableClient Table;

    protected TableAccessBase(TableServiceClient serviceClient, string tableName)
    {
        Table = serviceClient.GetTableClient(tableName);
        Table.CreateIfNotExists();
    }

    protected async Task<T?> GetEntityAsync<T>(string partitionKey, string rowKey)
        where T : class, ITableEntity, new()
    {
        try
        {
            var response = await Table.GetEntityAsync<T>(partitionKey, rowKey);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
