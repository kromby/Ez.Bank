using System.Net;
using Ez.Bank.Core.Exceptions;
using Ez.Bank.FunctionsApi.Models.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Ez.Bank.FunctionsApi.Middleware;

public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var httpReqData = await context.GetHttpRequestDataAsync();
            if (httpReqData == null)
            {
                throw;
            }

            var (statusCode, errorCode, message) = ex switch
            {
                BusinessRuleException bre => (HttpStatusCode.BadRequest, "BAD_REQUEST", bre.Message),
                NotFoundException nfe => (HttpStatusCode.NotFound, "NOT_FOUND", nfe.Message),
                ConflictException ce => (HttpStatusCode.Conflict, "CONFLICT", ce.Message),
                UnauthorizedAccessException uae => (HttpStatusCode.Forbidden, "FORBIDDEN", uae.Message),
                _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.")
            };

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(ex, "Unhandled exception occurred while processing request {Url}", httpReqData.Url);
            }
            else
            {
                _logger.LogWarning(ex, "Handled exception {ExceptionType} while processing request {Url}", ex.GetType().Name, httpReqData.Url);
            }

            var response = httpReqData.CreateResponse();
            response.StatusCode = statusCode;

            var errorResponse = new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = errorCode,
                    Message = message
                }
            };

            await response.WriteAsJsonAsync(errorResponse);
            context.GetInvocationResult().Value = response;
        }
    }
}
