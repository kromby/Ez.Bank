namespace Ez.Bank.Claims.UseCases.Ports;

public interface IDocumentStorage
{
    Task<string> UploadAsync(string claimId, string fileName, Stream content);
    Task<Stream> DownloadAsync(string storagePath);
    Task DeleteAsync(string storagePath);
}
