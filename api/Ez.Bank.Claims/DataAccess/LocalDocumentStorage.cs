using Ez.Bank.Claims.UseCases.Ports;

namespace Ez.Bank.Claims.DataAccess;

public class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _basePath;

    public LocalDocumentStorage(string basePath = "./local-storage/claim-documents")
    {
        _basePath = basePath;
    }

    public async Task<string> UploadAsync(string claimId, string fileName, Stream content)
    {
        var path = $"claims/{claimId}/{Guid.NewGuid()}-{fileName}";
        var fullPath = Path.Combine(_basePath, path);

        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream);

        return path;
    }

    public async Task<Stream> DownloadAsync(string storagePath)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        return await Task.FromResult<Stream>(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
    }

    public async Task DeleteAsync(string storagePath)
    {
        var fullPath = Path.Combine(_basePath, storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        await Task.CompletedTask;
    }
}
