using System.IO;
using System.Text;
using System.Threading.Tasks;
using cdeApi;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>Drives <see cref="TokenGate.InvokeAsync"/> with a <see cref="DefaultHttpContext"/>.</summary>
[TestFixture]
public class TokenGateTests
{
    private const string Token = "secret-token";

    private static async Task<(int Status, bool NextCalled, string Body)> RunAsync(
        string method, string tokenHeader = null, string authorization = null)
    {
        var body = new MemoryStream();
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Response.Body = body;
        if (tokenHeader != null) ctx.Request.Headers[TokenGate.HeaderName] = tokenHeader;
        if (authorization != null) ctx.Request.Headers.Authorization = authorization;

        var nextCalled = false;
        await TokenGate.InvokeAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, Token);

        return (ctx.Response.StatusCode, nextCalled, Encoding.UTF8.GetString(body.ToArray()));
    }

    [Test]
    public async Task Token_header_passes()
    {
        var (status, nextCalled, _) = await RunAsync("GET", tokenHeader: Token);

        Assert.That(nextCalled, Is.True);
        Assert.That(status, Is.EqualTo(200));
    }

    [Test]
    public async Task Bearer_token_passes()
    {
        var (_, nextCalled, _) = await RunAsync("POST", authorization: "Bearer " + Token);

        Assert.That(nextCalled, Is.True);
    }

    [TestCase(null, null, TestName = "no token")]
    [TestCase("", null, TestName = "empty token header")]
    [TestCase("wrong", null, TestName = "wrong token header")]
    [TestCase(Token + "x", null, TestName = "token header with extra characters")]
    [TestCase(null, "Bearer wrong", TestName = "wrong bearer token")]
    [TestCase(null, "Basic " + Token, TestName = "token under another scheme")]
    [TestCase(null, Token, TestName = "token with no scheme")]
    public async Task Missing_or_wrong_token_is_401(string tokenHeader, string authorization)
    {
        var (status, nextCalled, body) = await RunAsync("GET", tokenHeader, authorization);

        Assert.That(status, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(body, Is.EqualTo("unauthorized"));
        Assert.That(nextCalled, Is.False);
    }

    // The X-CDE-Token header wins when present: a wrong header is not rescued by a correct bearer token.
    [Test]
    public async Task Token_header_is_checked_before_the_bearer_token()
    {
        var (status, nextCalled, _) = await RunAsync("GET", tokenHeader: "wrong", authorization: "Bearer " + Token);

        Assert.That(status, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(nextCalled, Is.False);
    }

    [Test]
    public async Task Cors_preflight_passes_without_a_token()
    {
        var (_, nextCalled, _) = await RunAsync("OPTIONS");

        Assert.That(nextCalled, Is.True);
    }
}
