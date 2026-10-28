using System.Net.Http;
using System.Text;
using System.Text.Json;
using AiUsageWidget.Data;

namespace AiUsageWidget.Scheduling;

public sealed class AntigravityConversationRpc : IConversationRpc
{
    private readonly HttpClient client = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, _, _, errors) =>
            errors == System.Net.Security.SslPolicyErrors.None || message.RequestUri is { IsLoopback: true }
    }) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 8 * 1024 * 1024 };
    private AntigravityServerEndpoint? endpoint;
    private Uri? selectedUri;
    public async Task<JsonElement> CallAsync(string method, object args, CancellationToken ct)
    {
        if (endpoint is null || selectedUri is null)
        {
            endpoint = await Task.Run(AntigravityLanguageServerDiscovery.Find, ct).ConfigureAwait(false);
            if (endpoint is null) throw new HttpRequestException("Antigravity service unavailable.");
            foreach (var uri in endpoint.BaseUris.Where(u => u.IsLoopback))
            {
                try { await SendAsync(uri, "GetCapabilities", new { }, ct).ConfigureAwait(false); selectedUri = uri; break; }
                catch (Exception ex) when (ex is HttpRequestException or ConversationRpcException or TaskCanceledException && !ct.IsCancellationRequested) { }
            }
            if (selectedUri is null) throw new HttpRequestException("Antigravity service is incompatible.");
        }
        try { return await SendAsync(selectedUri, method, args, ct).ConfigureAwait(false); }
        catch (HttpRequestException) { selectedUri = null; endpoint = null; throw; }
        catch (ConversationRpcException ex) when (ex.Code is "unauthenticated" or "permission_denied")
        { selectedUri = null; endpoint = null; throw; }
    }
    private async Task<JsonElement> SendAsync(Uri baseUri, string method, object args, CancellationToken ct)
    {
        if (!baseUri.IsLoopback || baseUri.Scheme is not ("http" or "https")) throw new InvalidOperationException("Only local service requests are allowed.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "exa.language_server_pb.LanguageServerService/" + method));
        request.Headers.Add("X-Codeium-Csrf-Token", endpoint!.CsrfToken);
        request.Headers.Add("Connect-Protocol-Version", "1");
        request.Content = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        if (!response.IsSuccessStatusCode)
            throw new ConversationRpcException(json.RootElement.TryGetProperty("code", out var code) ? code.ToString() : ((int)response.StatusCode).ToString(),
                json.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? "" : "");
        return json.RootElement.Clone();
    }
    public void Dispose() => client.Dispose();
}
