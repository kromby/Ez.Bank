using System.Net;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.FunctionsApi.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Ez.Bank.FunctionsApi.Claims;

public class DocumentFunctions
{
    private readonly DocumentInteractor _interactor;

    public DocumentFunctions(DocumentInteractor interactor)
    {
        _interactor = interactor;
    }

    [Function("UploadDocument")]
    public async Task<HttpResponseData> UploadDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "claims/{claimId}/documents")] HttpRequestData req,
        FunctionContext context,
        string claimId)
    {
        var caller = context.GetCallerContext();

        var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault() ?? string.Empty;
        if (!contentType.Contains("multipart/form-data"))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Content-Type must be multipart/form-data" });
            return badRequest;
        }

        var boundary = GetBoundary(contentType);
        var reader = new Microsoft.AspNetCore.WebUtilities.MultipartReader(boundary, req.Body);
        var section = await reader.ReadNextSectionAsync();

        if (section == null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "No file provided" });
            return badRequest;
        }

        var fileName = GetFileName(section.ContentDisposition);
        var fileContentType = section.ContentType ?? "application/octet-stream";

        using var memoryStream = new MemoryStream();
        await section.Body.CopyToAsync(memoryStream);
        var fileSizeBytes = memoryStream.Length;
        memoryStream.Position = 0;

        var document = await _interactor.UploadAsync(claimId, fileName, fileContentType, fileSizeBytes, memoryStream, caller);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(document);
        return response;
    }

    [Function("GetDocuments")]
    public async Task<HttpResponseData> GetDocuments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "claims/{claimId}/documents")] HttpRequestData req,
        FunctionContext context,
        string claimId)
    {
        var caller = context.GetCallerContext();
        var documents = await _interactor.GetByClaimIdAsync(claimId, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(documents);
        return response;
    }

    [Function("DownloadDocument")]
    public async Task<HttpResponseData> DownloadDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "claims/{claimId}/documents/{documentId}")] HttpRequestData req,
        FunctionContext context,
        string claimId,
        string documentId)
    {
        var caller = context.GetCallerContext();
        var stream = await _interactor.DownloadAsync(claimId, documentId, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/pdf");
        await stream.CopyToAsync(response.Body);
        return response;
    }

    [Function("DeleteDocument")]
    public async Task<HttpResponseData> DeleteDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "claims/{claimId}/documents/{documentId}")] HttpRequestData req,
        FunctionContext context,
        string claimId,
        string documentId)
    {
        var caller = context.GetCallerContext();
        await _interactor.DeleteAsync(claimId, documentId, caller);

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    private static string GetBoundary(string contentType)
    {
        var elements = contentType.Split(';', StringSplitOptions.TrimEntries);
        var boundaryElement = elements.FirstOrDefault(e => e.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase));
        return boundaryElement?.Split('=', 2)[1].Trim('"') ?? string.Empty;
    }

    private static string GetFileName(string? contentDisposition)
    {
        if (string.IsNullOrEmpty(contentDisposition)) return "unknown";
        var fileNameParam = contentDisposition
            .Split(';', StringSplitOptions.TrimEntries)
            .FirstOrDefault(p => p.StartsWith("filename=", StringComparison.OrdinalIgnoreCase));
        return fileNameParam?.Split('=', 2)[1].Trim('"') ?? "unknown";
    }
}
