# UyKonek Go Backend

A production-style concurrent network intelligence backend for a WPF desktop client.

## Run

```bash
go mod init uykonek-backend
go run main.go
```

Server URL: `http://localhost:8080`

## API

```bash
curl http://localhost:8080/scan/network
curl "http://localhost:8080/scan/ping?ip=192.168.1.1"
curl "http://localhost:8080/scan/ports?ip=192.168.1.10"
```

## WPF integration

Use `HttpClient` from the C# WPF app:

```csharp
using var client = new HttpClient();
var json = await client.GetStringAsync("http://localhost:8080/scan/network");
```

Deserialize JSON and bind to your device table/view model collection.

## Notes

- Uses worker pool concurrency for subnet scanning and port scanning.
- Loads OUI vendor mappings from `../backend/data/latest_oui_lookup.json`.
- Validates inputs and returns structured errors, e.g. `{ "error": "invalid ip address" }`.
