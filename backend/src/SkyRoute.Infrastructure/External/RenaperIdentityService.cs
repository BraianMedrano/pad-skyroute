using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Polly;
using SkyRoute.Application.External;

namespace SkyRoute.Infrastructure.External;

public sealed class RenaperIdentityService(HttpClient httpClient, IConfiguration configuration) : IIdentityService
{
    public async Task<IdentityValidationResult> ValidateRecipientAsync(
        string dni, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (dni.Length is not (7 or 8) || dni.Any(digit => !char.IsAsciiDigit(digit)))
        {
            return new IdentityValidationResult(false, "DNI must contain 7 or 8 digits without separators.");
        }

        if (!Uri.TryCreate(configuration["Renaper:BaseUrl"], UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps
            || baseUri.UserInfo.Length != 0)
        {
            throw new ExternalServiceUnavailableException("RENAPER",
                new InvalidOperationException("Renaper:BaseUrl must be an absolute HTTPS URL without credentials."));
        }

        try
        {
            var endpoint = new Uri(baseUri, "/api/v1/consultas");
            using var response = await httpClient.PostAsJsonAsync(endpoint,
                new { tipo = "dni", numero = dni }, cancellationToken);

            // Only documented lookup responses are business outcomes. A proxy 404 or
            // an unexpected response must not be mistaken for a rejected recipient.
            var expectedCode = response.StatusCode switch
            {
                HttpStatusCode.NotFound => "sin_resultados",
                HttpStatusCode.Conflict => "consulta_ambigua",
                HttpStatusCode.UnprocessableEntity => "entrada_invalida",
                _ => null
            };

            if (response.StatusCode != HttpStatusCode.OK && expectedCode is null)
            {
                throw new HttpRequestException("RENAPER returned an unexpected status.", null, response.StatusCode);
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (response.StatusCode == HttpStatusCode.OK && HasString(root, "dni", dni))
            {
                return new IdentityValidationResult(true);
            }

            if (expectedCode is not null && HasString(root, "codigo", expectedCode))
            {
                return new IdentityValidationResult(false, response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "DNI was not found.",
                    HttpStatusCode.Conflict => "DNI could not be verified unambiguously.",
                    _ => "DNI was rejected as invalid."
                });
            }

            throw new JsonException("RENAPER returned an unexpected lookup response.");
        }
        catch (Exception ex) when (ex is HttpRequestException or ExecutionRejectedException or JsonException or IOException)
        {
            throw new ExternalServiceUnavailableException("RENAPER", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException("RENAPER", ex);
        }
    }

    private static bool HasString(JsonElement root, string name, string expected) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.String
        && property.ValueEquals(expected);
}
