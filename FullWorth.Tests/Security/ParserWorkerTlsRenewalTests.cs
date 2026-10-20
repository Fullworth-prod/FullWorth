using System.Security.Cryptography.X509Certificates;

public sealed class ParserWorkerTlsRenewalTests
{
    [Fact]
    public void LongRunningWorker_RenewsBeforeExpiry_AndPublishesOnlyPublicPin()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "parser-worker.cer.pem");
        var clock = new TestClock(DateTimeOffset.UtcNow);
        using var provider = new ParserWorkerTlsCertificateProvider(path, clock);
        var original = provider.GetCertificate();
        var originalThumbprint = original.Thumbprint;

        clock.Advance(TimeSpan.FromHours(23));
        Assert.Same(original, provider.GetCertificate());

        // Startup-only certificates fail after two days. A still-running worker
        // must recover on its next handshake even after an idle period beyond expiry.
        clock.Advance(TimeSpan.FromDays(2));
        var renewed = provider.GetCertificate();
        Assert.NotEqual(originalThumbprint, renewed.Thumbprint);
        Assert.True(renewed.HasPrivateKey);
        Assert.True(renewed.NotAfter.ToUniversalTime() > clock.GetUtcNow().UtcDateTime);
        Assert.True(original.HasPrivateKey); // an in-flight handshake retains its key
        using var pin = X509CertificateLoader.LoadCertificateFromFile(path);
        Assert.False(pin.HasPrivateKey);
        Assert.Equal(renewed.RawData, pin.RawData);
        Assert.Same(renewed, provider.GetCertificate());
    }

    [Fact]
    public void Renewal_IsSerializedAcrossConcurrentHandshakes()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "parser-worker.cer.pem");
        var clock = new TestClock(DateTimeOffset.UtcNow);
        using var provider = new ParserWorkerTlsCertificateProvider(path, clock);
        var original = provider.GetCertificate().Thumbprint;
        clock.Advance(TimeSpan.FromHours(25));

        var peers = new X509Certificate2[16];
        Parallel.For(0, peers.Length, index => peers[index] = provider.GetCertificate());

        Assert.NotEqual(original, peers[0].Thumbprint);
        Assert.All(peers, peer => Assert.Same(peers[0], peer));
        using var pin = X509CertificateLoader.LoadCertificateFromFile(path);
        Assert.Equal(peers[0].RawData, pin.RawData);
    }

    [Fact]
    public void FailedPublication_FailsHandshake_AndCanRetryWithoutServingExpiredKey()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "parser-worker.cer.pem");
        var clock = new TestClock(DateTimeOffset.UtcNow);
        using var provider = new ParserWorkerTlsCertificateProvider(path, clock);
        var original = provider.GetCertificate().Thumbprint;
        clock.Advance(TimeSpan.FromDays(3));
        File.Delete(path);
        Directory.CreateDirectory(path);

        var failure = Record.Exception(() => provider.GetCertificate());
        Assert.True(failure is IOException or UnauthorizedAccessException);

        Directory.Delete(path);
        var recovered = provider.GetCertificate();
        Assert.NotEqual(original, recovered.Thumbprint);
        using var pin = X509CertificateLoader.LoadCertificateFromFile(path);
        Assert.Equal(recovered.RawData, pin.RawData);
    }

    [Fact]
    public void DisposedProvider_RefusesNewHandshakes()
    {
        using var directory = new TemporaryDirectory();
        using var provider = new ParserWorkerTlsCertificateProvider(
            Path.Combine(directory.Path, "parser-worker.cer.pem"));
        provider.Dispose();
        provider.Dispose();
        Assert.Throws<ObjectDisposedException>(() => provider.GetCertificate());
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "fullworth-tls-renewal-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
