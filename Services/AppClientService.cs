using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
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

        public async Task<List<string>> TracerouteAsync(string ip, CancellationToken cancellationToken)
        {
            var isWindows = OperatingSystem.IsWindows();
            var file = isWindows ? "tracert" : "traceroute";
            var args = isWindows ? $"-d -h 12 {ip}" : $"-n -m 12 {ip}";

            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var lines = new List<string>();
            while (!process.StandardOutput.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.Trim());
            }

            var err = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(err))
            {
                lines.Add($"stderr: {err.Trim()}");
            }

            return lines;
        }

        public async Task<string> SendWakeOnLanAsync(string mac)
        {
            var normalized = NormalizeMac(mac);
            if (normalized == null)
            {
                throw new InvalidOperationException("Selected device has invalid MAC for WOL.");
            }

            var packet = BuildMagicPacket(normalized);
            using var client = new UdpClient();
            client.EnableBroadcast = true;
            await client.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
            return $"Magic packet sent to {normalized} on UDP/9.";
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

        private static byte[]? NormalizeMac(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return null;
            var parts = mac.Replace('-', ':').Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6) return null;

            var bytes = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                if (!byte.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                {
                    return null;
                }
            }

            return bytes;
        }

        private static byte[] BuildMagicPacket(byte[] mac)
        {
            var packet = new byte[102];
            for (int i = 0; i < 6; i++) packet[i] = 0xFF;
            for (int i = 1; i <= 16; i++)
            {
                Buffer.BlockCopy(mac, 0, packet, i * 6, 6);
            }
            return packet;
        }
    }
}
