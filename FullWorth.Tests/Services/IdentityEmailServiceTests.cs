using System.Net;
using System.Text;
using System.Text.Json;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Identity;
using Microsoft.Extensions.Options;

namespace FullWorth.Tests.Services;

public sealed class IdentityEmailServiceTests
{
    [Fact]
    public void Validator_AllowsDisabledConfiguration()
    {
        var validator =
            new IdentityEmailOptionsValidator();

        var result =
            validator.Validate(
                null,
                new IdentityEmailOptions
                {
                    Enabled = false
                });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_RejectsEnabledConfigurationWithoutApiKey()
    {
        var validator =
            new IdentityEmailOptionsValidator();

        var result =
            validator.Validate(
                null,
                new IdentityEmailOptions
                {
                    Enabled = true,
                    FromAddress = "security@billbeacon.net",
                    FromName = "FullWorth",
                    PublicWebBaseUrl = "https://billbeacon.net"
                });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validator_AcceptsEnabledSecureConfiguration()
    {
        var validator =
            new IdentityEmailOptionsValidator();

        var result =
            validator.Validate(
                null,
                CreateEnabledOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PasswordResetCode_ProducesWebResetLinkAndBearerRequest()
    {
        var handler =
            new RecordingHandler();

        using var httpClient =
            CreateClient(
                handler);

        var sender =
            new ResendIdentityEmailSender(
                httpClient,
                Options.Create(
                    CreateEnabledOptions()));

        await sender.SendPasswordResetCodeAsync(
            new ApplicationUser(),
            "person@example.com",
            "code+/= value");

        AssertProviderRequest(
            handler);

        var html =
            ReadHtml(
                handler.Body);

        Assert.Contains(
            "https://billbeacon.net/reset-password?email=person%40example.com&amp;code=code%2B%2F%3D%20value",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmationLink_RewritesInternalApiUrlToPublicWebUrl()
    {
        var handler =
            new RecordingHandler();

        using var httpClient =
            CreateClient(
                handler);

        var sender =
            new ResendIdentityEmailSender(
                httpClient,
                Options.Create(
                    CreateEnabledOptions()));

        await sender.SendConfirmationLinkAsync(
            new ApplicationUser(),
            "person@example.com",
            "http://api:8080/api/auth/confirmEmail?userId=user%2F123&code=code%2Bvalue&changedEmail=new%40example.com");

        AssertProviderRequest(
            handler);

        var html =
            ReadHtml(
                handler.Body);

        Assert.Contains(
            "https://billbeacon.net/auth/confirm-email?userId=user%2F123&amp;code=code%2Bvalue&amp;changedEmail=new%40example.com",
            html,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "http://api:8080",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmationLink_HtmlEncodedIdentityUrl_RewritesToPublicWebUrl()
    {
        var handler =
            new RecordingHandler();

        using var httpClient =
            CreateClient(
                handler);

        var sender =
            new ResendIdentityEmailSender(
                httpClient,
                Options.Create(
                    CreateEnabledOptions()));

        await sender.SendConfirmationLinkAsync(
            new ApplicationUser(),
            "person@example.com",
            "https://api.billbeacon.net/api/auth/confirmEmail?userId=user%2F123&amp;code=code%2Bvalue&amp;changedEmail=new%40example.com");

        AssertProviderRequest(
            handler);

        var html =
            ReadHtml(
                handler.Body);

        Assert.Contains(
            "https://billbeacon.net/auth/confirm-email?userId=user%2F123&amp;code=code%2Bvalue&amp;changedEmail=new%40example.com",
            html,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "amp%3Bcode",
            html,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "amp%3BchangedEmail",
            html,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "https://api.billbeacon.net",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmationLink_WithoutChangedEmail_RewritesToPublicWebUrl()
    {
        var handler =
            new RecordingHandler();

        using var httpClient =
            CreateClient(
                handler);

        var sender =
            new ResendIdentityEmailSender(
                httpClient,
                Options.Create(
                    CreateEnabledOptions()));

        await sender.SendConfirmationLinkAsync(
            new ApplicationUser(),
            "person@example.com",
            "http://api:8080/api/auth/confirmEmail?userId=user%2F123&code=code%2Bvalue");

        AssertProviderRequest(
            handler);

        var html =
            ReadHtml(
                handler.Body);

        Assert.Contains(
            "https://billbeacon.net/auth/confirm-email?userId=user%2F123&amp;code=code%2Bvalue",
            html,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "changedEmail=",
            html,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "http://api:8080",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledSender_IsNoOpAndDoesNotUseNetwork()
    {
        var handler =
            new RecordingHandler();

        using var httpClient =
            CreateClient(
                handler);

        var sender =
            new ResendIdentityEmailSender(
                httpClient,
                Options.Create(
                    new IdentityEmailOptions
                    {
                        Enabled = false
                    }));

        await sender.SendPasswordResetCodeAsync(
            new ApplicationUser(),
            "person@example.com",
            "reset-code");

        await sender.SendConfirmationLinkAsync(
            new ApplicationUser(),
            "person@example.com",
            "https://api.billbeacon.net/api/auth/confirmEmail?userId=example&code=example");

        Assert.Null(handler.Request);
    }


    [Theory]
    [InlineData("https://other.invalid/reset-password?code=private-code")]
    [InlineData("https://billbeacon.net.other.invalid/reset-password?code=private-code")]
    [InlineData("https://accounts.billbeacon.net/reset-password?code=private-code")]
    [InlineData("https://billbeacon.net:8443/reset-password?code=private-code")]
    [InlineData("http://billbeacon.net/reset-password?code=private-code")]
    [InlineData("https://user:password@billbeacon.net/reset-password?code=private-code")]
    [InlineData("/reset-password?code=private-code")]
    public async Task DirectResetLink_RejectsUntrustedOriginBeforeNetwork(string link)
    {
        var handler = new RecordingHandler();
        using var client = CreateClient(handler);
        var sender = new ResendIdentityEmailSender(client, Options.Create(CreateEnabledOptions()));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendPasswordResetLinkAsync(new ApplicationUser(), "person@example.com", link));
        Assert.Null(handler.Request);
        Assert.DoesNotContain("private-code", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://billbeacon.net/reset-password?code=code%2Bvalue")]
    [InlineData("https://BILLBEACON.NET:443/reset-password?code=code%2Bvalue")]
    public async Task DirectResetLink_AcceptsEquivalentConfiguredOrigin(string link)
    {
        var handler = new RecordingHandler();
        using var client = CreateClient(handler);
        var sender = new ResendIdentityEmailSender(client, Options.Create(CreateEnabledOptions()));
        await sender.SendPasswordResetLinkAsync(new ApplicationUser(), "person@example.com", link);
        AssertProviderRequest(handler);
        Assert.Contains("code=code%2Bvalue", ReadHtml(handler.Body), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectResetLink_DisabledSenderRemainsNoOp()
    {
        var handler = new RecordingHandler();
        using var client = CreateClient(handler);
        var sender = new ResendIdentityEmailSender(client,
            Options.Create(new IdentityEmailOptions { Enabled = false }));
        await sender.SendPasswordResetLinkAsync(new ApplicationUser(),
            "person@example.com", "https://other.invalid/reset-password");
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task ConfiguredNonDefaultOriginAndBasePath_RetainsResetCodeFlow()
    {
        var handler = new RecordingHandler();
        using var client = CreateClient(handler);
        var options = CreateEnabledOptions();
        options.PublicWebBaseUrl = "https://billbeacon.net:8443/fullworth";
        var sender = new ResendIdentityEmailSender(client, Options.Create(options));
        await sender.SendPasswordResetCodeAsync(new ApplicationUser(), "person@example.com", "code+/=");
        AssertProviderRequest(handler);
        Assert.Contains("https://billbeacon.net:8443/fullworth/reset-password?",
            ReadHtml(handler.Body), StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(
        HttpMessageHandler handler)
    {
        return new HttpClient(
            handler)
        {
            BaseAddress =
                new Uri(
                    "https://api.resend.com/")
        };
    }

    private static void AssertProviderRequest(
        RecordingHandler handler)
    {
        Assert.NotNull(handler.Request);
        Assert.Equal(
            HttpMethod.Post,
            handler.Request!.Method);
        Assert.Equal(
            "https://api.resend.com/emails",
            handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal(
            "Bearer",
            handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal(
            "test-resend-key",
            handler.Request.Headers.Authorization?.Parameter);
    }

    private static string ReadHtml(
        string body)
    {
        using var document =
            JsonDocument.Parse(
                body);

        var html =
            document.RootElement
                .GetProperty(
                    "html")
                .GetString();

        Assert.NotNull(html);

        return html!;
    }

    private static IdentityEmailOptions
        CreateEnabledOptions()
    {
        return new IdentityEmailOptions
        {
            Enabled = true,
            ApiKey = "test-resend-key",
            FromAddress = "security@billbeacon.net",
            FromName = "FullWorth",
            PublicWebBaseUrl = "https://billbeacon.net"
        };
    }

    private sealed class RecordingHandler
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request
        {
            get;
            private set;
        }

        public string Body
        {
            get;
            private set;
        } = string.Empty;

        protected override async Task<
            HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            Request = request;

            if (request.Content is not null)
            {
                Body =
                    await request.Content.ReadAsStringAsync(
                        cancellationToken);
            }

            return new HttpResponseMessage(
                HttpStatusCode.OK)
            {
                Content =
                    new StringContent(
                        "{}",
                        Encoding.UTF8,
                        "application/json")
            };
        }
    }
}
