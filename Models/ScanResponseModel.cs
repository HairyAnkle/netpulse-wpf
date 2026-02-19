using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UyKonek.Models
{
    public sealed class ScanResponseModel
    {
        public ScanMetadataModel Scan { get; set; } = new();
        public List<DeviceModel> Devices { get; set; } = new();
    }

    public sealed class ScanMetadataModel
    {
        [JsonPropertyName("scan_id")]
        public int ScanId { get; set; }

        public string Subnet { get; set; } = string.Empty;

        [JsonPropertyName("ts_start")]
        public DateTimeOffset TsStart { get; set; }

        [JsonPropertyName("ts_end")]
        public DateTimeOffset TsEnd { get; set; }

        [JsonPropertyName("host_count")]
        public int HostCount { get; set; }
    }
}
