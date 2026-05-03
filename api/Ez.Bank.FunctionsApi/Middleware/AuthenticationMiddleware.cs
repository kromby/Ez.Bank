using System.Net;
using Ez.Bank.Core;
using Ez.Bank.FunctionsApi.Auth;
using Ez.Bank.FunctionsApi.Models.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Ez.Bank.FunctionsApi.Middleware;

public class AuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    public AuthenticationMiddleware(IJwtTokenService jwtTokenService, ILogger<AuthenticationMiddleware> logger)
    {
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpReqData = await context.GetHttpRequestDataAsync();
        if (httpReqData == null)
        {
            await next(context);
            return;
        }

        var url = httpReqData.Url.AbsolutePath.ToLower();
        if (url.Contains("/auth/token") || url.Contains("/health"))
        {
            await next(context);
            return;
        }

        var authHeader = httpReqData.Headers
            .FirstOrDefault(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            .Value?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Missing or invalid Authorization header for request {Url}", httpReqData.Url);
            await WriteUnauthorizedResponse(context, httpReqData, "Missing or invalid Authorization header.");
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        var callerContext = _jwtTokenService.ValidateToken(token);
        if (callerContext == null)
        {
            _logger.LogWarning("Invalid JWT token for request {Url}", httpReqData.Url);
            await WriteUnauthorizedResponse(context, httpReqData, "Invalid or expired token.");
            return;
        }

        context.Items["CallerContext"] = callerContext;
        await next(context);
    }

    private static async Task WriteUnauthorizedResponse(FunctionContext context, Microsoft.Azure.Functions.Worker.Http.HttpRequestData httpReqData, string message)
    {
        var response = httpReqData.CreateResponse();
        response.StatusCode = HttpStatusCode.Unauthorized;

        var errorResponse = new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "UNAUTHORIZED",
                Message = message
            }
        };

        await response.WriteAsJsonAsync(errorResponse);
        context.GetInvocationResult().Value = response;
    }
}
