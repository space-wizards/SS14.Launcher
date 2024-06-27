using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Serilog;
using SS14.Launcher.Models;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Api;

public sealed class AuthApi
{
    private readonly HttpClient _httpClient;

    public AuthApi(HttpClient http)
    {
        _httpClient = http;
    }

    public async Task<AuthenticateResult> AuthenticateAsync(AuthenticateRequest request)
    {
        try
        {
            var authUrl = ConfigConstants.AuthUrl + "api/auth/authenticate";

            using var resp = await _httpClient.PostAsJsonAsync(authUrl, request);

            if (resp.IsSuccessStatusCode)
            {
                var respJson = await resp.Content.AsJson<AuthenticateResponse>();
                var token = new LoginToken(respJson.Token, respJson.ExpireTime);
                return new AuthenticateResult(new LoginInfo
                {
                    UserId = respJson.UserId,
                    Token = token,
                    Username = respJson.Username
                });
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Login failure.
                var respJson = await resp.Content.AsJson<AuthenticateDenyResponse>();
                return new AuthenticateResult(respJson.Errors, respJson.Code);
            }

            Log.Error("Server returned unexpected HTTP status code: {responseCode}", resp.StatusCode);
            Log.Debug("Response for error:\n{response}\n{content}", resp, await resp.Content.ReadAsStringAsync());
            // Unknown error? uh oh.
            return new AuthenticateResult(
                new[] { "Server returned unknown error" },
                AuthenticateDenyResponseCode.UnknownError);
        }
        catch (JsonException e)
        {
            Log.Error(e, "JsonException in AuthenticateAsync");
            return new AuthenticateResult(
                new[] { "Server sent invalid response" },
                AuthenticateDenyResponseCode.UnknownError);
        }
        catch (HttpRequestException httpE)
        {
            Log.Error(httpE, "HttpRequestException in AuthenticateAsync");
            HttpSelfTest.StartSelfTest();
            return new AuthenticateResult(
                new[] { $"Connection error to authentication server: {httpE.Message}" },
                AuthenticateDenyResponseCode.UnknownError);
        }
    }

    public async Task<RegisterResult> RegisterAsync(string username, string email, string password)
    {
        try
        {
            var request = new RegisterRequest(username, email, password);

            var authUrl = ConfigConstants.AuthUrl + "api/auth/register";

            using var resp = await _httpClient.PostAsJsonAsync(authUrl, request);

            if (resp.IsSuccessStatusCode)
            {
                var respJson = await resp.Content.AsJson<RegisterResponse>();
                return new RegisterResult(respJson.Status);
            }

            if (resp.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                // Register failure.
                var respJson = await resp.Content.AsJson<RegisterResponseError>();
                return new RegisterResult(respJson.Errors);
            }

            Log.Error("Server returned unexpected HTTP status code: {responseCode}", resp.StatusCode);
            Log.Debug("Response for error:\n{response}\n{content}", resp, await resp.Content.ReadAsStringAsync());
            // Unknown error? uh oh.
            return new RegisterResult(new[] { "Server returned unknown error" });
        }
        catch (JsonException e)
        {
            Log.Error(e, "JsonException in RegisterAsync");
            return new RegisterResult(new[] { "Server sent invalid response" });
        }
        catch (HttpRequestException httpE)
        {
            Log.Error(httpE, "HttpRequestException in RegisterAsync");
            HttpSelfTest.StartSelfTest();
            return new RegisterResult(new[] { $"Connection error to authentication server: {httpE.Message}" });
        }
    }

    /// <returns>Any errors that occured</returns>
    public async Task<string[]?> ForgotPasswordAsync(string email)
    {
        try
        {
            var request = new ResetPasswordRequest(email);

            var authUrl = ConfigConstants.AuthUrl + "api/auth/resetPassword";

            using var resp = await _httpClient.PostAsJsonAsync(authUrl, request);

            if (resp.IsSuccessStatusCode)
            {
                return null;
            }

            // Unknown error? uh oh.
            Log.Error("Server returned unexpected HTTP status code: {responseCode}", resp.StatusCode);
            Log.Debug("Response for error:\n{response}\n{content}", resp, await resp.Content.ReadAsStringAsync());
            return new[] { "Server returned unknown error" };
        }
        catch (HttpRequestException httpE)
        {
            Log.Error(httpE, "HttpRequestException in ForgotPasswordAsync");
            return new[] { $"Connection error to authentication server: {httpE.Message}" };
        }
    }

    public async Task<string[]?> ResendConfirmationAsync(string email)
    {
        try
        {
            var request = new ResendConfirmationRequest(email);

            var authUrl = ConfigConstants.AuthUrl + "api/auth/resendConfirmation";

            using var resp = await _httpClient.PostAsJsonAsync(authUrl, request);

            if (resp.IsSuccessStatusCode)
            {
                return null;
            }

            // Unknown error? uh oh.
            Log.Error("Server returned unexpected HTTP status code: {responseCode}", resp.StatusCode);
            Log.Debug("Response for error:\n{response}\n{content}", resp, await resp.Content.ReadAsStringAsync());
            return new[] { "Server returned unknown error" };
        }
        catch (HttpRequestException httpE)
        {
            Log.Error(httpE, "HttpRequestException in ResendConfirmationAsync");
            HttpSelfTest.StartSelfTest();
            return new[] { $"Connection error to authentication server: {httpE.Message}" };
        }
    }

    /// <returns>Null if the server refused to refresh the token (it expired).</returns>
    /// <exception cref="AuthApiException">
    ///     Thrown if an unexpected error occured.
    /// </exception>
    public async Task<LoginToken?> RefreshTokenAsync(string token)
    {
        try
        {
            var request = new RefreshRequest(token);

            var authUrl = ConfigConstants.AuthUrl + "api/auth/refresh";

            using var resp = await _httpClient.PostAsJsonAsync(authUrl, request);

            if (resp.IsSuccessStatusCode)
            {
                var response = await resp.Content.AsJson<RefreshResponse>();

                return new LoginToken(response.NewToken, response.ExpireTime);
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("Got unauthorized while trying to refresh token. Guess it expired.");

                return null;
            }

            // Unknown error? uh oh.
            Log.Error("Server returned unexpected HTTP status code: {responseCode}", resp.StatusCode);
            Log.Debug("Response for error:\n{response}\n{content}", resp, await resp.Content.ReadAsStringAsync());

            throw new AuthApiException($"Server returned unexpected HTTP status code: {resp.StatusCode}");
        }
        catch (HttpRequestException httpE)
        {
            Log.Error(httpE, "HttpRequestException in ResendConfirmationAsync");
            HttpSelfTest.StartSelfTest();
            throw new AuthApiException("HttpRequestException thrown", httpE);
        }
        catch (JsonException jsonE)
        {
            Log.Error(jsonE, "JsonException in ResendConfirmationAsync");
            throw new AuthApiException("JsonException thrown", jsonE);
        }
    }

    public async Task LogoutTokenAsync(string token)
    {
        try
        {
            var request = new LogoutRequest(token);

            var authUrl = ConfigConstants.AuthUrl + "api/auth/logout";

            using var resp = await _httpClient.PostAsJsonAsync(authUrl, request);

            if (resp.IsSuccessStatusCode)
            {
                return;
            }

            // Unknown error? uh oh.
            Log.Error("Server returned unexpected HTTP status code: {responseCode}", resp.StatusCode);
            Log.Debug("Response for error:\n{response}\n{content}", resp, await resp.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException httpE)
        {
            // Does it make sense to just... swallow this exception? The token will stay "active" until it expires.
            Log.Error(httpE, "HttpRequestException in LogoutTokenAsync");
            HttpSelfTest.StartSelfTest();
        }
    }

    /// <summary>
    ///     Check if a token is still valid.
    /// </summary>
    /// <returns>True if the token is still valid.</returns>
    /// <exception cref="AuthApiException">
    ///     Thrown if an unexpected error occured.
    /// </exception>
    public async Task<bool> CheckTokenAsync(string token)
    {
        try
        {
            var authUrl = ConfigConstants.AuthUrl + "api/auth/ping";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, authUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("SS14Auth", token);
            using var resp = await _httpClient.SendAsync(requestMessage);

            if (resp.IsSuccessStatusCode)
            {
                return true;
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                return false;
            }

            // Unknown error? uh oh.
            Log.Error("Server returned unexpected HTTP status code: {responseCode}", resp.StatusCode);
            Log.Debug("Response for error:\n{response}\n{content}", resp, await resp.Content.ReadAsStringAsync());
            throw new AuthApiException($"Server returned unexpected HTTP status code: {resp.StatusCode}");
        }
        catch (HttpRequestException httpE)
        {
            // Does it make sense to just... swallow this exception? The token will stay "active" until it expires.
            Log.Error(httpE, "HttpRequestException in CheckTokenAsync");
            HttpSelfTest.StartSelfTest();
            throw new AuthApiException("HttpRequestException thrown", httpE);
        }
    }

    public sealed record AuthenticateRequest(string? Username, Guid? UserId, string Password, string? TfaCode = null)
    {
        public AuthenticateRequest(string username, string password) : this(username, null, password)
        {

        }

        public AuthenticateRequest(Guid userId, string password) : this(null, userId, password)
        {

        }
    }

    public sealed record AuthenticateResponse(string Token, string Username, Guid UserId, DateTimeOffset ExpireTime);

    public sealed record AuthenticateDenyResponse(string[] Errors, AuthenticateDenyResponseCode Code);

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AuthenticateDenyResponseCode
    {
        // @formatter:off
        None               =  0,
        InvalidCredentials =  1,
        AccountUnconfirmed =  2,
        TfaRequired        =  3,
        TfaInvalid         =  4,
        AccountLocked      =  5,
        EmailChangeNeeded  =  6,
        PasswdChangeNeeded =  7,

        // Not actually from the API, but used internally.
        UnknownError       = -1,
        // @formatter:on
    }

    public sealed record RegisterRequest(string Username, string Email, string Password);

    public sealed record RegisterResponse(RegisterResponseStatus Status);

    public sealed record RegisterResponseError(string[] Errors);

    public sealed record ResetPasswordRequest(string Email);

    public sealed record ResendConfirmationRequest(string Email);

    public sealed record LogoutRequest(string Token);

    public sealed record RefreshRequest(string Token);

    public sealed record RefreshResponse(DateTimeOffset ExpireTime, string NewToken);
}

public readonly struct AuthenticateResult
{
    private readonly LoginInfo? _loginInfo;
    private readonly string[]? _errors;
    public AuthApi.AuthenticateDenyResponseCode Code { get; }

    public AuthenticateResult(LoginInfo loginInfo)
    {
        _loginInfo = loginInfo;
        _errors = null;
        Code = default;
    }

    public AuthenticateResult(string[] errors, AuthApi.AuthenticateDenyResponseCode code)
    {
        _loginInfo = null;
        _errors = errors;
        Code = code;
    }

    public bool IsSuccess => _loginInfo != null;

    public LoginInfo LoginInfo => _loginInfo
                                  ?? throw new InvalidOperationException(
                                      "This AuthenticateResult is not a success.");

    public string[] Errors => _errors
                              ?? throw new InvalidOperationException("This AuthenticateResult is not a failure.");
}

public readonly struct RegisterResult
{
    private readonly RegisterResponseStatus? _status;
    private readonly string[]? _errors;

    public RegisterResult(RegisterResponseStatus status)
    {
        _status = status;
        _errors = null;
    }

    public RegisterResult(string[] errors)
    {
        _status = null;
        _errors = errors;
    }

    public bool IsSuccess => _status != null;

    public RegisterResponseStatus Status => _status
                                            ?? throw new InvalidOperationException(
                                                "This RegisterResult is not a success.");

    public string[] Errors => _errors
                              ?? throw new InvalidOperationException("This RegisterResult is not a failure.");
}

public enum RegisterResponseStatus
{
    Registered,
    RegisteredNeedConfirmation
}

[Serializable]
public class AuthApiException : Exception
{
    public AuthApiException()
    {
    }

    public AuthApiException(string message) : base(message)
    {
    }

    public AuthApiException(string message, Exception inner) : base(message, inner)
    {
    }
}
