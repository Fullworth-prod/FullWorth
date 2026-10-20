using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Renews the ephemeral, public-only pin before a new TLS handshake can use an
/// expired certificate. Private keys stay in process memory.
/// </summary>
public sealed class ParserWorkerTlsCertificateProvider : IDisposable
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly TimeProvider _clock;
    private X509Certificate2 _current;
    private X509Certificate2? _previous;
    private bool _disposed;

    public ParserWorkerTlsCertificateProvider(
        string path,
        TimeProvider? clock = null)
    {
        _path = path;
        _clock = clock ?? TimeProvider.System;
        _current = ParserWorkerTlsCertificate.CreateAndPublish(_path, _clock);
    }

    public X509Certificate2 GetCertificate()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_clock.GetUtcNow().UtcDateTime <
                _current.NotAfter.ToUniversalTime().AddHours(-24))
            {
                return _current;
            }

            // Publication must succeed before Kestrel serves the new key.
            // A failed publication fails this handshake closed; a later one retries.
            var replacement = ParserWorkerTlsCertificate.CreateAndPublish(_path, _clock);
            _previous?.Dispose();
            // Keep the last certificate alive for handshakes already selecting it.
            // Rotations are at least 24 hours apart, well beyond Kestrel's 10s limit.
            _previous = _current;
            _current = replacement;
            return _current;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _current.Dispose();
            _previous?.Dispose();
        }
    }
}
