using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Kitsuneo.API.Controllers;
using Kitsuneo.API.Infrastructure.Auth;
using Kitsuneo.Application.Auth;
using Kitsuneo.Application.Phrases;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kitsuneo.API.Tests;

[Collection(ApiCollection.Name)]
public class AuthFlowTests(ApiFactory factory)
{
    private const string Password = "correct horse battery";

    // The API writes enums as names
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateHttpsClient();

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/phrases");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_IsPublic()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ReturnsAccessTokenAndSetsSecureRefreshCookie()
    {
        var email = NewEmail();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.User.Email.Should().Be(email);
        body.AccessToken.Should().NotBeNullOrEmpty();

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(RefreshTokenCookie.Name));
        cookie.Should().ContainAll("httponly", "secure", "samesite=strict", "path=/api/v1/auth");
        (await response.Content.ReadAsStringAsync()).Should().NotContain(RefreshCookieValue(response));

        var me = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", body.AccessToken);
        (await me.Content.ReadFromJsonAsync<UserResponse>())!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Register_WithTakenEmail_Returns409()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email.ToUpperInvariant(), Password));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(NewEmail(), "short"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownEmail_Returns401WithSameMessage()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "wrong password"));
        var unknownEmail = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(NewEmail(), Password));

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail
            .Should().Be((await unknownEmail.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task Login_WithRightPassword_ReturnsWorkingToken()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        (await SendAsync(HttpMethod.Get, "/api/v1/phrases", body!.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_RotatesCookieAndRejectsReuseOfOldOne()
    {
        var (_, firstCookie) = await RegisterAsync(NewEmail());

        var refreshed = await RefreshAsync(firstCookie);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondCookie = RefreshCookieValue(refreshed);
        secondCookie.Should().NotBe(firstCookie);
        (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken.Should().NotBeNullOrEmpty();

        // The old cookie again means it leaked: refused, and the whole session with it
        (await RefreshAsync(firstCookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await RefreshAsync(secondCookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var response = await _client.PostAsync("/api/v1/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_BlacklistsAccessTokenAndRevokesRefreshToken()
    {
        var (accessToken, cookie) = await RegisterAsync(NewEmail());

        var logout = await SendAsync(HttpMethod.Post, "/api/v1/auth/logout", accessToken, cookie);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        logout.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith($"{RefreshTokenCookie.Name}=;"));
        (await SendAsync(HttpMethod.Get, "/api/v1/auth/me", accessToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await RefreshAsync(cookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_LeavesOtherSessionsOfSameUserWorking()
    {
        var email = NewEmail();
        var (firstToken, firstCookie) = await RegisterAsync(email);
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var secondToken = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        await SendAsync(HttpMethod.Post, "/api/v1/auth/logout", firstToken, firstCookie);

        (await SendAsync(HttpMethod.Get, "/api/v1/auth/me", secondToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await RefreshAsync(RefreshCookieValue(login))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Phrases_AreVisibleOnlyToTheirOwner()
    {
        var (ownerToken, _) = await RegisterAsync(NewEmail());
        var (otherToken, _) = await RegisterAsync(NewEmail());
        var created = await SendAsync(HttpMethod.Post, "/api/v1/phrases", ownerToken,
            body: new CreatePhraseRequest(Core.Enums.Language.English, "Mine only"));
        var phrase = await created.Content.ReadFromJsonAsync<PhraseResponse>(Json);

        (await SendAsync(HttpMethod.Get, $"/api/v1/phrases/{phrase!.Id}", ownerToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/v1/phrases/{phrase.Id}", otherToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"/api/v1/phrases/{phrase.Id}", otherToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_OverRateLimit_Returns429()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("Auth:RateLimitPerMinute", "2"));
        using var client = limited.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var request = new LoginRequest(NewEmail(), Password);

        await client.PostAsJsonAsync("/api/v1/auth/login", request);
        await client.PostAsJsonAsync("/api/v1/auth/login", request);
        var third = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<(string AccessToken, string RefreshCookie)> RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (body!.AccessToken, RefreshCookieValue(response));
    }

    private Task<HttpResponseMessage> RefreshAsync(string cookie) =>
        SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", accessToken: null, cookie);

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string? accessToken,
        string? refreshCookie = null,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (refreshCookie is not null)
            request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={refreshCookie}");
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        return _client.SendAsync(request);
    }

    private static string RefreshCookieValue(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{RefreshTokenCookie.Name}="));
        return cookie[(RefreshTokenCookie.Name.Length + 1)..cookie.IndexOf(';')];
    }
}
