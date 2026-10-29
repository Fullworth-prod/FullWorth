using System.Net;
using System.Security.Claims;
using FullWorth.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class AdminBffWriteProxySessionSecurityTests
{
    [Fact]
    public async Task PasswordChange_Success_SignsOutWebSession()
    {
        var authentication =
            new RecordingAuthenticationService();

        var httpContext =
            CreateHttpContext(authentication);

        var proxy =
            CreateProxy(
                HttpStatusCode.NoContent);

        _ =
            await proxy.ForwardJsonAsync(
                httpContext,
                HttpMethod.Post,
                "/api/account/security/password",
                new
                {
                    currentPassword =
                        "current-password",

                    newPassword =
                        "replacement-password",

                    twoFactorCode =
                        (string?)null
                });

        Assert.Equal(
            1,
            authentication.SignOutCount);

        Assert.Equal(
            CookieAuthenticationDefaults
                .AuthenticationScheme,
            authentication.LastSignOutScheme);
    }

    [Fact]
    public async Task PasswordChange_Failure_PreservesWebSession()
    {
        var authentication =
            new RecordingAuthenticationService();

        var httpContext =
            CreateHttpContext(authentication);

        var proxy =
            CreateProxy(
                HttpStatusCode.BadRequest);

        _ =
            await proxy.ForwardJsonAsync(
                httpContext,
                HttpMethod.Post,
                "/api/account/security/password",
                new
                {
                    currentPassword =
                        "wrong-password",

                    newPassword =
                        "replacement-password",

                    twoFactorCode =
                        (string?)null
                });

        Assert.Equal(
            0,
            authentication.SignOutCount);
    }

    [Fact]
    public async Task OrdinaryAccountSecurityWrite_DoesNotSignOutWebSession()
    {
        var authentication =
            new RecordingAuthenticationService();

        var httpContext =
            CreateHttpContext(authentication);

        var proxy =
            CreateProxy(
                HttpStatusCode.NoContent);

        _ =
            await proxy.ForwardJsonAsync(
                httpContext,
                HttpMethod.Post,
                "/api/account/security/profile",
                new
                {
                    displayName =
                        "Security Test"
                });

        Assert.Equal(
            0,
            authentication.SignOutCount);
    }

    [Theory]
    [InlineData("/api/subscription-reviews", 200)]
    [InlineData("/api/subscription-reviews/11111111-1111-1111-1111-111111111111/evidence", 200)]
    [InlineData("/api/subscription-reviews", 409)]
    [InlineData("/api/subscription-reviews/11111111-1111-1111-1111-111111111111/evidence", 503)]
    public async Task SubscriptionWrites_ForwardOnlyTheExpectedPost_WithoutReplayingFailures(
        string path, int status)
    {
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler((HttpStatusCode)status);
        var proxy = CreateProxy((HttpStatusCode)status, handler);
        var result = await proxy.ForwardJsonAsync(
            CreateHttpContext(authentication), HttpMethod.Post, path, new { synthetic = true });
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(path, handler.LastPath);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal(0, authentication.SignOutCount);
    }

    [Theory]
    [InlineData("PUT", "/api/subscription-reviews")]
    [InlineData("PUT", "/api/subscription-reviews/11111111-1111-1111-1111-111111111111/evidence")]
    [InlineData("DELETE", "/api/subscription-reviews")]
    [InlineData("POST", "/api/subscription-reviews/00000000-0000-0000-0000-000000000000/evidence")]
    [InlineData("POST", "/api/subscription-reviews/not-a-guid/evidence")]
    [InlineData("POST", "/api/subscription-reviews/evidence")]
    [InlineData("POST", "/api/subscription-reviews//evidence")]
    [InlineData("POST", "/api/subscription-reviews/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/subscription-reviews/11111111-1111-1111-1111-111111111111/evidence/extra")]
    [InlineData("POST", "/api/subscription-reviews/../account")]
    [InlineData("POST", "/api/subscription-reviews?next=/api/admin/users")]
    [InlineData("POST", "https://example.invalid/api/subscription-reviews")]
    [InlineData("POST", "//example.invalid/api/subscription-reviews")]
    public async Task SubscriptionWriteLookalikes_AreRejectedBeforeForwarding(
        string method, string path)
    {
        var handler = new StaticResponseHandler(HttpStatusCode.OK);
        var proxy = CreateProxy(HttpStatusCode.OK, handler);
        var result = await proxy.ForwardJsonAsync(
            CreateHttpContext(new RecordingAuthenticationService()), new HttpMethod(method), path, new { });
        Assert.Equal(StatusCodes.Status400BadRequest,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("/api/account/security/password", 200, false)]
    [InlineData("/api/account/security/password", 201, false)]
    [InlineData("/api/account/security/password", 202, false)]
    [InlineData("/api/account/security/password", 205, false)]
    [InlineData("/api/account/security/password", 200, true)]
    [InlineData("/api/account/security/password", 202, true)]
    [InlineData("/api/account/security/sessions/revoke-all", 200, true)]
    [InlineData("/api/account/security/sessions/revoke-all", 202, true)]
    [InlineData("/api/account/security/two-factor/disable", 200, true)]
    [InlineData("/api/account/security/two-factor/disable", 202, true)]
    public async Task UnconfirmedSecuritySuccess_PreservesSessionAndRejectsBody(
        string path, int status, bool explicitSignOut)
    {
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler((HttpStatusCode)status, "<html>PRIVATE-ACCOUNT-CANARY</html>", "text/html");
        var proxy = CreateProxy((HttpStatusCode)status, handler);
        var context = CreateHttpContext(authentication);
        var result = explicitSignOut
            ? await proxy.ForwardJsonAndSignOutOnSuccessAsync(context, HttpMethod.Post, path, new { synthetic = true })
            : await proxy.ForwardJsonAsync(context, HttpMethod.Post, path, new { synthetic = true });
        Assert.Equal(StatusCodes.Status502BadGateway,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, authentication.SignOutCount);
        Assert.Equal(1, handler.RequestCount);
        var problem = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        var body = System.Text.Json.JsonSerializer.Serialize(problem);
        Assert.DoesNotContain("PRIVATE-ACCOUNT-CANARY", body);
        Assert.Contains("could not confirm", body);
    }

    [Theory]
    [InlineData("/api/account/security/password", false)]
    [InlineData("/api/account/security/password", true)]
    [InlineData("/api/account/security/sessions/revoke-all", true)]
    [InlineData("/api/account/security/two-factor/disable", true)]
    public async Task ContractedNoContent_SignsOutOnce(string path, bool explicitSignOut)
    {
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler(HttpStatusCode.NoContent);
        var proxy = CreateProxy(HttpStatusCode.NoContent, handler);
        var context = CreateHttpContext(authentication);
        var result = explicitSignOut
            ? await proxy.ForwardJsonAndSignOutOnSuccessAsync(context, HttpMethod.Post, path, new { synthetic = true })
            : await proxy.ForwardJsonAsync(context, HttpMethod.Post, path, new { synthetic = true });
        Assert.Equal(StatusCodes.Status204NoContent,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(1, authentication.SignOutCount);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(202)]
    [InlineData(204)]
    public async Task AccountDeletion_ConfirmedOrCleanupPending_SignsOutOnce(int status)
    {
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler((HttpStatusCode)status,
            status == 202 ? "{\"erasurePending\":true}" : null);
        var result = await CreateProxy((HttpStatusCode)status, handler).ForwardJsonAsync(
            CreateHttpContext(authentication), HttpMethod.Delete, "/api/account", new { synthetic = true });
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(1, authentication.SignOutCount);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(205)]
    public async Task AccountDeletion_UnexpectedSuccessDoesNotConfirmDeletion(int status)
    {
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler((HttpStatusCode)status, "{\"accepted\":true}");
        var result = await CreateProxy((HttpStatusCode)status, handler).ForwardJsonAsync(
            CreateHttpContext(authentication), HttpMethod.Delete, "/api/account", new { synthetic = true });
        Assert.Equal(StatusCodes.Status502BadGateway,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, authentication.SignOutCount);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task RejectedSecurityUpdate_PreservesStatusAndSession(int status)
    {
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler((HttpStatusCode)status);
        var result = await CreateProxy((HttpStatusCode)status, handler).ForwardJsonAndSignOutOnSuccessAsync(
            CreateHttpContext(authentication), HttpMethod.Post, "/api/account/security/password", new { synthetic = true });
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, authentication.SignOutCount);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(200, "<html>PRIVATE-EXPORT-CANARY</html>", "text/html")]
    [InlineData(202, "{\"pending\":true}", "application/json")]
    [InlineData(204, "", "application/json")]
    [InlineData(200, "", "application/json")]
    [InlineData(200, "{PRIVATE-EXPORT-CANARY", "application/json")]
    [InlineData(200, "null", "application/json")]
    [InlineData(200, "[]", "application/json")]
    [InlineData(200, "true", "application/json")]
    [InlineData(200, "{}{}", "application/json")]
    [InlineData(200, "{}", "text/plain")]
    [InlineData(200, "{}", "application/jsonp")]
    public async Task Export_RejectsUnconfirmedOrInvalidJsonWithoutDownload(int status, string payload, string mediaType)
    {
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler((HttpStatusCode)status, payload, mediaType);
        var result = await CreateProxy((HttpStatusCode)status, handler).ForwardJsonDownloadAsync(
            CreateHttpContext(authentication), HttpMethod.Post, "/api/account/export", new { synthetic = true },
            "synthetic-export.json", "application/json");
        Assert.Equal(StatusCodes.Status502BadGateway,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.IsNotType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>(result);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(0, authentication.SignOutCount);
        Assert.DoesNotContain("PRIVATE-EXPORT-CANARY",
            System.Text.Json.JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value));
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("Application/JSON")]
    public async Task Export_CurrentApiEnvelopePreservesExactBytesAndFilename(string mediaType)
    {
        var payload = " \n" + CreateExportPayload().TrimEnd('}') +
            ",\"additionalMetadata\":{\"note\":\"café\"}}\n";
        var handler = new StaticResponseHandler(HttpStatusCode.OK, payload, mediaType);
        var result = await CreateProxy(HttpStatusCode.OK, handler).ForwardJsonDownloadAsync(
            CreateHttpContext(new RecordingAuthenticationService()), HttpMethod.Post, "/api/account/export",
            new { synthetic = true }, "synthetic-export.json", "application/json");
        var file = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>(result);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(payload), file.FileContents.ToArray());
        Assert.Equal("application/json", file.ContentType);
        Assert.Equal("synthetic-export.json", file.FileDownloadName);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Export_TooDeepJsonFailsSafely()
    {
        var payload = "{\"synthetic\":" + new string('[', 65) + "0" + new string(']', 65) + "}";
        var handler = new StaticResponseHandler(HttpStatusCode.OK, payload);
        var result = await CreateProxy(HttpStatusCode.OK, handler).ForwardJsonDownloadAsync(
            CreateHttpContext(new RecordingAuthenticationService()), HttpMethod.Post, "/api/account/export",
            new { synthetic = true }, "synthetic-export.json", "application/json");
        Assert.Equal(StatusCodes.Status502BadGateway,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }


    public static IEnumerable<object[]> InvalidExportEnvelopes()
    {
        yield return ["empty-object", "{}"];
        yield return ["error-object", "{\"message\":\"PRIVATE-EXPORT-CANARY\"}"];
        yield return ["schema-only", "{\"schemaVersion\":\"1.1\"}"];
        var valid = CreateExportPayload();
        var root = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
        foreach (var name in root.Select(property => property.Key).ToArray())
        {
            var missing = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
            missing.Remove(name);
            yield return ["missing-" + name, missing.ToJsonString()];
            var wrong = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
            wrong[name] = System.Text.Json.Nodes.JsonValue.Create(42);
            yield return ["wrong-type-" + name, wrong.ToJsonString()];
            yield return ["duplicate-" + name, valid.TrimEnd('}') +
                "," + System.Text.Json.JsonSerializer.Serialize(name) + ":" + root[name]!.ToJsonString() + "}"];
        }
        foreach (var version in new[] { "", "1.0", "1.2", "1.1 ", "login required" })
        {
            var changed = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
            changed["schemaVersion"] = version;
            yield return ["unsupported-schema-" + version, changed.ToJsonString()];
        }
        foreach (var field in root["profile"]!.AsObject().Select(property => property.Key).ToArray())
        {
            var missing = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
            missing["profile"]!.AsObject().Remove(field);
            yield return ["missing-profile-" + field, missing.ToJsonString()];
            var wrong = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
            wrong["profile"]![field] = 42;
            yield return ["wrong-profile-" + field, wrong.ToJsonString()];
            yield return ["duplicate-profile-" + field,
                valid.Replace("\"profile\":{", "\"profile\":{" +
                    System.Text.Json.JsonSerializer.Serialize(field) + ":" +
                    (root["profile"]![field]?.ToJsonString() ?? "null") + ",", StringComparison.Ordinal)];
        }
        foreach (var field in new[] { "exportedAtUtc", "profile.createdAtUtc", "profile.lastLoginAtUtc" })
        {
            var changed = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
            if (field.StartsWith("profile.", StringComparison.Ordinal))
                changed["profile"]![field[8..]] = "not-a-timestamp";
            else changed[field] = "not-a-timestamp";
            yield return ["invalid-date-" + field, changed.ToJsonString()];
        }
        foreach (var field in root.Where(property => property.Value is System.Text.Json.Nodes.JsonArray)
            .Select(property => property.Key))
        {
            foreach (var entry in new[] { "null", "true", "1", "\"record\"", "[]" })
            {
                var changed = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
                changed[field] = System.Text.Json.Nodes.JsonNode.Parse("[" + entry + "]");
                yield return ["invalid-record-" + field + "-" + entry, changed.ToJsonString()];
            }
        }
        // Escaped names must be interpreted before duplicate/required-field checks.
        yield return ["escaped-duplicate", valid.TrimEnd('}') + ",\"schema\\u0056ersion\":\"1.1\"}"];
        yield return ["truncated-root", valid[..^1]];
        yield return ["trailing-second-object", valid + "{}"];
        yield return ["trailing-value", valid + "true"];
    }

    [Theory]
    [MemberData(nameof(InvalidExportEnvelopes))]
    public async Task Export_RejectsIncompleteOrContradictoryEnvelopeWithoutDownload(string scenario, string payload)
    {
        Assert.False(string.IsNullOrEmpty(scenario));
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler(HttpStatusCode.OK, payload);
        var result = await CreateProxy(HttpStatusCode.OK, handler).ForwardJsonDownloadAsync(
            CreateHttpContext(authentication), HttpMethod.Post, "/api/account/export", new { synthetic = true },
            "synthetic-export.json", "application/json");
        Assert.Equal(StatusCodes.Status502BadGateway,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.IsNotType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>(result);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(0, authentication.SignOutCount);
        Assert.DoesNotContain("PRIVATE-EXPORT-CANARY",
            System.Text.Json.JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_PreservesNullableProfileAndRecordCollections(bool lastLoginPresent)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(CreateExportPayload())!.AsObject();
        root["profile"]!["lastLoginAtUtc"] = lastLoginPresent ? "2026-10-29T01:00:00+00:00" : null;
        root["profile"]!["displayName"] = "";
        root["profile"]!["isActive"] = false;
        root["bankConnections"] = System.Text.Json.JsonSerializer.SerializeToNode(new[]
        {
            new FullWorth.API.Services.Accounts.BankConnectionExport(
                Guid.Parse("11111111-1111-1111-1111-111111111111"), "Synthetic bank", "Connected",
                null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)
        }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        root["profile"]!["additionalMetadata"] = System.Text.Json.Nodes.JsonNode.Parse("{\"nested\":[1,null]}");
        var payload = root.ToJsonString();
        var handler = new StaticResponseHandler(HttpStatusCode.OK, payload);
        var result = await CreateProxy(HttpStatusCode.OK, handler).ForwardJsonDownloadAsync(
            CreateHttpContext(new RecordingAuthenticationService()), HttpMethod.Post, "/api/account/export",
            new { synthetic = true }, "synthetic-export.json", "application/json");
        var file = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>(result);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(payload), file.FileContents.ToArray());
        Assert.Equal(1, handler.RequestCount);
    }

    private static string CreateExportPayload()
    {
        // Serialize actual current API DTO with MVC's default web naming policy.
        // A coordinated API schema change must update the BFF contract explicitly.
        var timestamp = new DateTimeOffset(2026, 10, 29, 1, 0, 0, TimeSpan.Zero);
        var export = new FullWorth.API.Services.Accounts.AccountDataExportResult(
            SchemaVersion: FullWorth.API.Services.Accounts.AccountDataExportBuilder.CurrentSchemaVersion,
            ExportedAtUtc: timestamp,
            Profile: new FullWorth.API.Services.Accounts.AccountProfileExport(
                "Synthetic café", "synthetic@fullworth.local", timestamp, null, true),
            BankConnections: [], BankAccounts: [], BankTransactions: [], BillStreams: [],
            BillStatements: [], BillLineItems: [], BillChanges: [], BillAlerts: [],
            StatementUploads: [], AiEvaluations: [], PlaidLinkSessions: []);
        return System.Text.Json.JsonSerializer.Serialize(export,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    }

    private static DefaultHttpContext
        CreateHttpContext(
            RecordingAuthenticationService
                authentication)
    {
        var services =
            new ServiceCollection()
                .AddSingleton<
                    IAuthenticationService>(
                    authentication)
                .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices =
                services
        };
    }

    private static AdminBffWriteProxyService
        CreateProxy(
            HttpStatusCode responseStatusCode,
            StaticResponseHandler? handler = null)
    {
        var client =
            new HttpClient(
                handler ?? new StaticResponseHandler(
                    responseStatusCode))
            {
                BaseAddress =
                    new Uri(
                        "https://api.fullworth.test")
            };

        return new AdminBffWriteProxyService(
            new SingleHttpClientFactory(
                client));
    }

    private sealed class RecordingAuthenticationService
        : IAuthenticationService
    {
        public int SignOutCount { get; private set; }

        public string? LastSignOutScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(
            HttpContext context,
            string? scheme)
        {
            var identity =
                new ClaimsIdentity(
                    [
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            Guid.NewGuid()
                                .ToString("D"))
                    ],
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var properties =
                new AuthenticationProperties
                {
                    ExpiresUtc =
                        DateTimeOffset.UtcNow
                            .AddMinutes(10)
                };

            properties.StoreTokens(
                [
                    new AuthenticationToken
                    {
                        Name =
                            "access_token",

                        Value =
                            "test-access-token"
                    },

                    new AuthenticationToken
                    {
                        Name =
                            "refresh_token",

                        Value =
                            "test-refresh-token"
                    },

                    new AuthenticationToken
                    {
                        Name =
                            "expires_at",

                        Value =
                            DateTimeOffset.UtcNow
                                .AddMinutes(10)
                                .ToString("O")
                    },

                    new AuthenticationToken
                    {
                        Name =
                            "token_type",

                        Value =
                            "Bearer"
                    }
                ]);

            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(
                        new ClaimsPrincipal(
                            identity),
                        properties,
                        CookieAuthenticationDefaults
                            .AuthenticationScheme)));
        }

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignOutCount++;
            LastSignOutScheme =
                scheme;

            return Task.CompletedTask;
        }
    }

    private sealed class SingleHttpClientFactory(
        HttpClient client)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            return client;
        }
    }

    private sealed class StaticResponseHandler(
        HttpStatusCode responseStatusCode,
        string? payload = null,
        string mediaType = "application/json")
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastAuthorizationScheme { get; private set; }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            RequestCount++;
            LastMethod = request.Method;
            LastPath = request.RequestUri?.AbsolutePath;
            LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(
                new HttpResponseMessage(
                    responseStatusCode)
                {
                    Content = payload is null ? null : new StringContent(payload, System.Text.Encoding.UTF8, mediaType)
                });
        }
    }
}
