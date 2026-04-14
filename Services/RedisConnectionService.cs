using StackExchange.Redis;

namespace RealTime.Services;

public interface IRedisConnectionService
{
    IDatabase GetDatabase();
}

public sealed class RedisConnectionService : IRedisConnectionService, IHostedService, IDisposable
{
    // Delays in seconds: 1st retry → 15s, 2nd → 30s, 3rd and beyond → 60s
    private static readonly int[] RetryDelaysSeconds = [15, 30, 60];

    private readonly string _connectionString;
    private readonly ILogger<RedisConnectionService> _logger;
    private volatile IConnectionMultiplexer? _multiplexer;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    public RedisConnectionService(IConfiguration config, ILogger<RedisConnectionService> logger)
    {
        _connectionString = config.GetConnectionString("Redis") ?? "localhost:49004";
        _logger = logger;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public IDatabase GetDatabase()
    {
        var mux = _multiplexer;
        if (mux is null || !mux.IsConnected)
            throw new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                "Redis is not available.");
        return mux.GetDatabase();
    }

    // ── IHostedService ─────────────────────────────────────────────────────────

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = ConnectAsync(isInitialConnection: true, _cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    // ── Connection loop ────────────────────────────────────────────────────────

    private async Task ConnectAsync(bool isInitialConnection, CancellationToken ct)
    {
        // Only one reconnection task at a time
        if (!await _reconnectLock.WaitAsync(0))
            return;

        try
        {
            // On initial startup, the very first attempt has no wait.
            // On reconnection, every attempt is preceded by a delay.
            bool skipWait = isInitialConnection;
            int retryCount = 0;

            while (!ct.IsCancellationRequested)
            {
                if (!skipWait)
                {
                    int delay = RetryDelaysSeconds[Math.Min(retryCount, RetryDelaysSeconds.Length - 1)];
                    _logger.LogInformation(
                        "Redis reconnect attempt {Retry} in {Delay}s...",
                        retryCount + 1, delay);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    retryCount++;
                }
                skipWait = false;

                // If StackExchange.Redis already recovered on its own, skip new mux creation
                if (!isInitialConnection && _multiplexer?.IsConnected == true)
                {
                    _logger.LogInformation("Redis reconnected on its own.");
                    return;
                }

                try
                {
                    _logger.LogInformation("Connecting to Redis...");

                    var options = ConfigurationOptions.Parse(_connectionString);
                    options.AbortOnConnectFail = false;
                    options.ConnectRetry = 1;

                    var newMux = await ConnectionMultiplexer.ConnectAsync(options);

                    if (newMux.IsConnected)
                    {
                        ApplyMultiplexer(newMux);
                        _logger.LogInformation(
                            "Redis {Action} successfully.",
                            isInitialConnection ? "connected" : "reconnected");
                        return;
                    }

                    _logger.LogWarning("Redis did not connect.");
                    newMux.Dispose();
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Redis connection attempt failed.");
                }
            }
        }
        finally
        {
            _reconnectLock.Release();
        }
    }

    private void ApplyMultiplexer(IConnectionMultiplexer newMux)
    {
        newMux.ConnectionFailed += OnConnectionFailed;
        newMux.ConnectionRestored += OnConnectionRestored;

        var old = Interlocked.Exchange(ref _multiplexer, newMux);

        if (old is not null)
        {
            old.ConnectionFailed -= OnConnectionFailed;
            old.ConnectionRestored -= OnConnectionRestored;
            try { old.Dispose(); } catch { /* ignore */ }
        }
    }

    // ── Event handlers ─────────────────────────────────────────────────────────

    private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogWarning(
            "Redis connection lost. FailureType={FailureType}, Endpoint={Endpoint}",
            e.FailureType, e.EndPoint);
        _ = ConnectAsync(isInitialConnection: false, _cts.Token);
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogInformation(
            "Redis connection restored. Endpoint={Endpoint}", e.EndPoint);
    }

    // ── IDisposable ────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _cts.Cancel();
        var mux = Interlocked.Exchange(ref _multiplexer, null);
        mux?.Dispose();
        _reconnectLock.Dispose();
        _cts.Dispose();
    }
}
