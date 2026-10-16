using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class PasskeyAuthenticationTests
{
    [Fact]
    public async Task DisabledFeatureDoesNotOfferChallenges()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync("/api/auth/passkeys/options", new { })).StatusCode);
    }

    [Fact]
    public async Task EnrollmentRequiresAuthenticationAndCurrentPassword()
    {
        await using var factory = new FullWorthApiFactory();
        await using var enabled = Enable(factory);
        using var client = enabled.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/account/security/passkeys/options", new { currentPassword = "wrong" })).StatusCode);
        var session = await TestUserAuthentication.RegisterAndLoginAsync(client);
        TestUserAuthentication.Authorize(client, session);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/account/security/passkeys/options", new { currentPassword = "wrong" })).StatusCode);
    }

    [Fact]
    public async Task ValidKeyRegistersSignsInAndCannotReplay()
    {
        await using var factory = new FullWorthApiFactory();
        await using var enabled = Enable(factory);
        using var client = enabled.CreateClient();
        var session = await TestUserAuthentication.RegisterAndLoginAsync(client);
        TestUserAuthentication.Authorize(client, session);
        using var authenticator = new VirtualKey();
        var options = await Begin(client, "/api/account/security/passkeys/options",
            new { currentPassword = "FullWorth!Tests123" });
        var credential = authenticator.Register(options.GetProperty("options"));
        var registered = await client.PostAsJsonAsync("/api/account/security/passkeys/register", new
        {
            ceremonyId = options.GetProperty("ceremonyId").GetString(), credentialJson = credential,
            name = "Test FIDO2 key"
        });
        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        var login = await Begin(client, "/api/auth/passkeys/options", new { });
        var assertion = authenticator.Assert(login.GetProperty("options"));
        var body = new { ceremonyId = login.GetProperty("ceremonyId").GetString(), credentialJson = assertion };
        var success = await client.PostAsJsonAsync("/api/auth/passkeys/login", body);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.False(string.IsNullOrEmpty((await success.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/passkeys/login", body)).StatusCode);

        // The same valid assertion is not valid for a new challenge.
        var next = await Begin(client, "/api/auth/passkeys/options", new { });
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/passkeys/login", new
            {
                ceremonyId = next.GetProperty("ceremonyId").GetString(), credentialJson = assertion
            })).StatusCode);
    }

    [Fact]
    public async Task WrongOriginAndMissingUserVerificationCannotEnroll()
    {
        await using var factory = new FullWorthApiFactory();
        await using var enabled = Enable(factory);
        using var client = enabled.CreateClient();
        var session = await TestUserAuthentication.RegisterAndLoginAsync(client);
        TestUserAuthentication.Authorize(client, session);
        foreach (var badOrigin in new[] { false, true })
        {
            using var key = new VirtualKey();
            var options = await Begin(client, "/api/account/security/passkeys/options",
                new { currentPassword = "FullWorth!Tests123" });
            var credential = key.Register(options.GetProperty("options"),
                origin: badOrigin ? "https://evil.example" : "https://fullworth.org",
                verified: badOrigin);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/account/security/passkeys/register", new
                {
                    ceremonyId = options.GetProperty("ceremonyId").GetString(), credentialJson = credential
                })).StatusCode);
        }
    }

    [Fact]
    public async Task ExistingMfaCannotBeBypassedWithAValidKey()
    {
        await using var factory = new FullWorthApiFactory();
        await using var enabled = Enable(factory);
        using var client = enabled.CreateClient();
        var session = await TestUserAuthentication.RegisterAndLoginAsync(client);
        TestUserAuthentication.Authorize(client, session);
        using var key = new VirtualKey();
        await Enroll(client, key);
        await using (var scope = enabled.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.FindByEmailAsync(session.Email);
            Assert.True((await manager.SetTwoFactorEnabledAsync(user!, true)).Succeeded);
        }
        client.DefaultRequestHeaders.Authorization = null;
        var options = await Begin(client, "/api/auth/passkeys/options", new { });
        var response = await client.PostAsJsonAsync("/api/auth/passkeys/login", new
        {
            ceremonyId = options.GetProperty("ceremonyId").GetString(),
            credentialJson = key.Assert(options.GetProperty("options"))
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task KeysAreOwnerScopedAndRemovalPreventsSignIn()
    {
        await using var factory = new FullWorthApiFactory();
        await using var enabled = Enable(factory);
        using var client = enabled.CreateClient();
        var owner = await TestUserAuthentication.RegisterAndLoginAsync(client);
        TestUserAuthentication.Authorize(client, owner);
        using var key = new VirtualKey();
        await Enroll(client, key);
        var owned = await client.GetFromJsonAsync<JsonElement>("/api/account/security/passkeys");
        var id = owned[0].GetProperty("id").GetString();
        client.DefaultRequestHeaders.Authorization = null;
        var other = await TestUserAuthentication.RegisterAndLoginAsync(client);
        TestUserAuthentication.Authorize(client, other);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>(
            "/api/account/security/passkeys")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            "/api/account/security/passkeys/remove", new
            {
                credentialId = id, currentPassword = "FullWorth!Tests123"
            })).StatusCode);
        TestUserAuthentication.Authorize(client, owner);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/account/security/passkeys/remove", new
            {
                credentialId = id, currentPassword = "FullWorth!Tests123"
            })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        var options = await Begin(client, "/api/auth/passkeys/options", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/auth/passkeys/login", new
            {
                ceremonyId = options.GetProperty("ceremonyId").GetString(),
                credentialJson = key.Assert(options.GetProperty("options"))
            })).StatusCode);
    }

    private static async Task Enroll(HttpClient client, VirtualKey key)
    {
        var options = await Begin(client, "/api/account/security/passkeys/options",
            new { currentPassword = "FullWorth!Tests123" });
        var response = await client.PostAsJsonAsync("/api/account/security/passkeys/register", new
        {
            ceremonyId = options.GetProperty("ceremonyId").GetString(),
            credentialJson = key.Register(options.GetProperty("options")), name = "Test key"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<FullWorth.API.ApiAssemblyMarker>
        Enable(FullWorthApiFactory factory) => factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Passkeys:Enabled"] = "true" })));

    private static async Task<JsonElement> Begin(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // Test-only ES256 authenticator. Produces actual signed WebAuthn responses;
    // no production verification service is mocked.
    private sealed class VirtualKey : IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly byte[] _id = RandomNumberGenerator.GetBytes(32);
        private string _userHandle = "";

        public string Register(JsonElement options, string origin = "https://fullworth.org", bool verified = true)
        {
            _userHandle = options.GetProperty("user").GetProperty("id").GetString()!;
            var clientData = ClientData("webauthn.create", options, origin);
            var p = _key.ExportParameters(false);
            var cose = new byte[] { 0xa5, 1, 2, 3, 0x26, 0x20, 1, 0x21, 0x58, 0x20 }
                .Concat(p.Q.X!).Concat(new byte[] { 0x22, 0x58, 0x20 }).Concat(p.Q.Y!).ToArray();
            var auth = AuthData((byte)(verified ? 0x45 : 0x41), 0)
                .Concat(new byte[16]).Concat(new byte[] { 0, 32 }).Concat(_id).Concat(cose).ToArray();
            using var stream = new MemoryStream();
            stream.WriteByte(0xa3);
            Text(stream, "fmt"); Text(stream, "none");
            Text(stream, "authData"); ByteString(stream, auth);
            Text(stream, "attStmt"); stream.WriteByte(0xa0);
            return JsonSerializer.Serialize(new
            {
                id = B64(_id), rawId = B64(_id), type = "public-key",
                response = new { clientDataJSON = B64(clientData),
                    attestationObject = B64(stream.ToArray()), transports = new[] { "usb" } },
                clientExtensionResults = new { }
            });
        }

        public string Assert(JsonElement options)
        {
            var client = ClientData("webauthn.get", options, "https://fullworth.org");
            var auth = AuthData(5, 1);
            var signature = _key.SignData(auth.Concat(SHA256.HashData(client)).ToArray(),
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            return JsonSerializer.Serialize(new
            {
                id = B64(_id), rawId = B64(_id), type = "public-key",
                response = new { clientDataJSON = B64(client), authenticatorData = B64(auth),
                    signature = B64(signature), userHandle = _userHandle },
                clientExtensionResults = new { }
            });
        }
        private static byte[] ClientData(string type, JsonElement options, string origin) =>
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                type, challenge = options.GetProperty("challenge").GetString(), origin, crossOrigin = false
            });
        private static byte[] AuthData(byte flags, byte count) =>
            SHA256.HashData(Encoding.UTF8.GetBytes("fullworth.org"))
                .Concat(new byte[] { flags, 0, 0, 0, count }).ToArray();
        private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static void Text(Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.WriteByte((byte)(0x60 + bytes.Length)); stream.Write(bytes);
        }
        private static void ByteString(Stream stream, byte[] bytes)
        {
            stream.WriteByte(0x59); stream.WriteByte((byte)(bytes.Length >> 8));
            stream.WriteByte((byte)bytes.Length); stream.Write(bytes);
        }
        public void Dispose() => _key.Dispose();
    }
}
