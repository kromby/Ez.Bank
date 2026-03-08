using Azure.Storage.Blobs;
using Ez.Bank.Claims.UseCases.Ports;

namespace Ez.Bank.Claims.DataAccess;

public class BlobDocumentStorage : IDocumentStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobDocumentStorage(BlobServiceClient blobServiceClient, string containerName = "claim-documents")
    {
        _blobServiceClient = blobServiceClient;
        _containerName = containerName;
    }

    public async Task<string> UploadAsync(string claimId, string fileName, Stream content)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

        var path = $"claims/{claimId}/{Guid.NewGuid()}-{fileName}";
        var blobClient = containerClient.GetBlobClient(path);
        await blobClient.UploadAsync(content, overwrite: true);

        return path;
    }

    public async Task<Stream> DownloadAsync(string storagePath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(storagePath);
        var response = await blobClient.DownloadStreamingAsync();

        return response.Value.Content;
    }

    public async Task DeleteAsync(string storagePath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(storagePath);
        await blobClient.DeleteIfExistsAsync();
    }
}
