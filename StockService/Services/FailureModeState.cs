namespace StockService.Services;

public sealed class FailureModeState
{
    private readonly Lock _lock = new();
    private bool _enabled;
    private string _message = "Falha simulada manualmente para validacao do tratamento entre microsservicos.";

    public bool Enabled
    {
        get
        {
            lock (_lock)
            {
                return _enabled;
            }
        }
    }

    public string Message
    {
        get
        {
            lock (_lock)
            {
                return _message;
            }
        }
    }

    public void Set(bool enabled, string? message)
    {
        lock (_lock)
        {
            _enabled = enabled;
            if (!string.IsNullOrWhiteSpace(message))
            {
                _message = message.Trim();
            }
        }
    }
}
