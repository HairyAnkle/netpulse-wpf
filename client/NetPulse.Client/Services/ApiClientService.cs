using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetPulse.Client.Models;

namespace NetPulse.Client.Services;

public sealed class ApiClientService
{
    private readonly SettingsService _settingsService;
    private readonly HttpClient _httpClient;

    public ApiClientService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(95)
        };
    }

    public async Task<ScanResponseModel> ScanDevicesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settingsService.BackendBaseUrl}/scan/devices");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}: {body}");
        }

        var scan = await response.Content.ReadFromJsonAsync<ScanResponseModel>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            cancellationToken);

        return scan ?? new ScanResponseModel();
    }
}
