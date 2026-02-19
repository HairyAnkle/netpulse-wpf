using System;
using System.Text.Json.Serialization;

namespace UyKonek.Models
{
    public sealed class DeviceModel
    {
        public string Ip { get; set; } = string.Empty;
        public string Mac { get; set; } = string.Empty;
        public string? Hostname { get; set; }
        public string? Vendor { get; set; }

        [JsonPropertyName("first_seen")]
        public DateTimeOffset FirstSeen { get; set; }

        [JsonPropertyName("last_seen")]
        public DateTimeOffset LastSeen { get; set; }

        [JsonPropertyName("is_new")]
        public bool IsNew { get; set; }
    }
}
