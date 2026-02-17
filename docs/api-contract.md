# NetPulse API Contract (MVP)

## `GET /health`

Response:

```json
{
  "status": "ok",
  "service": "netpulse-backend"
}
```

## `POST /scan/devices`

Performs a user-initiated network discovery for the active local subnet.

- Rate limited (minimum 3 seconds between starts)
- One active scan at a time
- Scan timeout: 90 seconds

Response:

```json
{
  "scan": {
    "scan_id": 12,
    "subnet": "192.168.1.0/24",
    "ts_start": "2026-01-20T20:11:31.200000Z",
    "ts_end": "2026-01-20T20:11:39.550000Z",
    "host_count": 3
  },
  "devices": [
    {
      "ip": "192.168.1.1",
      "mac": "3C:5A:B4:11:22:33",
      "hostname": "router.local",
      "vendor": "Contoso Device Labs",
      "first_seen": "2026-01-18T15:00:00Z",
      "last_seen": "2026-01-20T20:11:39.100000Z",
      "is_new": false
    }
  ]
}
```
