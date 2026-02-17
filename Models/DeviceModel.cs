using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UyKonek.Models
{
    public sealed class DeviceModel
    {
        public string Ip { get; set; } = string.Empty;
        public string Mac { get; set; } = string.Empty;
        public string? Hostname { get; set; }
        public string? Vendor { get; set; }
        public DateTimeOffset FirstSeen { get; set; }
        public DateTimeOffset LastSeen { get; set; }
        public bool IsNew { get; set; }
    }
}
