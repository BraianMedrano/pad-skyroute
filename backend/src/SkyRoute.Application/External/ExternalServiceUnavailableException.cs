namespace SkyRoute.Application.External;

// Thrown by a real adapter when its external service (RENAPER, Open-Meteo, Padi) cannot be reached
// or keeps failing after the standard retries. The API answers 503 with this message in `detail`.
public sealed class ExternalServiceUnavailableException(string service, Exception? innerException = null)
    : Exception($"{service} is unavailable right now. Try again later.", innerException)
{
    public string Service { get; } = service;
}
