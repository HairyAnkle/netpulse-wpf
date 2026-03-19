using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UyKonek.Models;

namespace UyKonek.Services
{
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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_settingsService.BackendBaseUrl}/scan/network");
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}: {body}");
            }

            var devices = await response.Content.ReadFromJsonAsync<List<DeviceModel>>(
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                cancellationToken) ?? new List<DeviceModel>();

            var now = DateTimeOffset.UtcNow;
            return new ScanResponseModel
            {
                Devices = devices,
                Scan = new ScanMetadataModel
                {
                    ScanId = BuildScanId(now),
                    Subnet = "local-network",
                    TsStart = now,
                    TsEnd = now,
                    HostCount = devices.Count,
                }
            };
        }

        public async Task<PingResultModel> PingAsync(string ip, CancellationToken cancellationToken)
        {
            var encoded = WebUtility.UrlEncode(ip);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_settingsService.BackendBaseUrl}/scan/ping?ip={encoded}");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ReadJson<PingResultModel>(response, cancellationToken);
        }

        public async Task<PortScanResultModel> ScanPortsAsync(string ip, CancellationToken cancellationToken)
        {
            var encoded = WebUtility.UrlEncode(ip);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_settingsService.BackendBaseUrl}/scan/ports?ip={encoded}");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ReadJson<PortScanResultModel>(response, cancellationToken);
        }

        private static int BuildScanId(DateTimeOffset timestamp)
        {
            var id = timestamp.ToUnixTimeSeconds();
            return id > int.MaxValue ? int.MaxValue : (int)id;
        }

        private static async Task<T> ReadJson<T>(HttpResponseMessage response, CancellationToken cancellationToken) where T : class, new()
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}: {body}");
            }

            return await response.Content.ReadFromJsonAsync<T>(
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                cancellationToken) ?? new T();
        }
    }
}
