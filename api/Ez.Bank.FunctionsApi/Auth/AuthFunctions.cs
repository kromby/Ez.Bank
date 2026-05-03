using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ez.Bank.FunctionsApi.Models.Requests;
using Ez.Bank.FunctionsApi.Models.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Ez.Bank.FunctionsApi.Auth;

public class AuthFunctions
{
    private static readonly string[] ValidRoles = ["Creditor", "Debtor", "Bank"];

    private readonly ILogger<AuthFunctions> _logger;
    private readonly AuthSettings _authSettings;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthFunctions(
        ILogger<AuthFunctions> logger,
        AuthSettings authSettings,
        IJwtTokenService jwtTokenService)
    {
        _logger = logger;
        _authSettings = authSettings;
        _jwtTokenService = jwtTokenService;
    }

    [Function("AuthToken")]
    public async Task<HttpResponseData> GetToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/token")] HttpRequestData req)
    {
        var requestBody = await req.ReadAsStringAsync();
        if (string.IsNullOrEmpty(requestBody))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "INVALID_REQUEST", "Request body is required.");
        }

        var tokenRequest = JsonSerializer.Deserialize<AuthTokenRequest>(requestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (tokenRequest is null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "INVALID_REQUEST", "Invalid request body.");
        }

        var expectedPasswordBytes = Encoding.UTF8.GetBytes(_authSettings.Password);
        var providedPasswordBytes = Encoding.UTF8.GetBytes(tokenRequest.Password);

        if (!CryptographicOperations.FixedTimeEquals(expectedPasswordBytes, providedPasswordBytes))
        {
            _logger.LogWarning("Invalid authentication attempt for user {UserId}", tokenRequest.UserId);
            return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Invalid password.");
        }

        if (!ValidRoles.Contains(tokenRequest.UserRole))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "INVALID_ROLE",
                $"Invalid role '{tokenRequest.UserRole}'. Must be one of: {string.Join(", ", ValidRoles)}.");
        }

        var token = _jwtTokenService.GenerateToken(tokenRequest.UserId, tokenRequest.BankId, tokenRequest.UserRole);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new AuthTokenResponse
        {
            Token = token,
            ExpiresIn = _authSettings.JwtExpiryMinutes * 60
        });

        return response;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req, HttpStatusCode statusCode, string code, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = code,
                Message = message
            }
        });

        return response;
    }
}
