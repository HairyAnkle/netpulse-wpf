using System.Text.Json.Serialization;

namespace UyKonek.Models
{
    public sealed class PingResultModel
    {
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = string.Empty;

        [JsonPropertyName("alive")]
        public bool Alive { get; set; }

        [JsonPropertyName("latency_ms")]
        public long LatencyMs { get; set; }
    }

    public sealed class PortScanResultModel
    {
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = string.Empty;

        [JsonPropertyName("open_ports")]
        public int[] OpenPorts { get; set; } = System.Array.Empty<int>();
    }
}
