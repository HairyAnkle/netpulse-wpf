# NetPulse (WPF) — Network Scanner (C# + Python)

**Repo:** `netpulse-wpf`  
**Description:** Windows network scanner built with C# WPF + Python FastAPI backend: device discovery, diagnostics tools, alerts, and reporting (authorized networks only).

NetPulse is a **Windows-only** desktop app inspired by modern network scanner tools (i.e., functionality), built with:

- **Frontend:** C# **WPF** (MVVM)
- **Backend:** **Python FastAPI** (local service on 127.0.0.1)
- **Storage:** SQLite (device history, scan runs, alerts, diagnostics logs)

> ⚠️ **Authorized use only.** Use NetPulse only on networks you own or where you have explicit permission to scan.

---

## Goals

- Provide a clean **device discovery** experience: “Who’s on my Wi‑Fi/LAN?”
- Include essential **network diagnostics**: ping, traceroute, DNS lookup, port scan (safe + rate-limited)
- Track changes over time: **last seen**, **new device alerts**, scan history
- Offer a foundation for advanced features: risk scoring, camera heuristics, reporting export

---

## Key Features (Planned)

### Device Discovery
- Subnet detection (default gateway + local interface)
- Host discovery (ICMP ping + optional TCP probes)
- ARP table parsing for MAC addresses
- Device info:
  - IP, MAC, hostname (best-effort), vendor (offline OUI lookup), first/last seen
- History & tracking:
  - device timeline (new/changed/disappeared)
  - scan runs saved to SQLite

### Security & Monitoring
- **New device alerts** (first time seen / unknown MAC)
- “Suspicious device” heuristics (optional module)
- Camera heuristics (RTSP/HTTP UI port hints) — **no password guessing**

### Internet Diagnostics
- Latency / jitter / packet loss tracking
- (Optional) speed test module

### Troubleshooting Tools
- Ping
- Traceroute
- DNS lookup (A/AAAA/MX/TXT)
- Port scanning (top ports by default, opt-in deeper scan, rate-limited)

---

## Architecture

NetPulse runs the **Python backend locally** and the WPF app calls it via HTTP.

```
WPF (MVVM)
  |
  |  HTTP (localhost)
  v
FastAPI backend (async)
  |
  +-- scanning modules (discovery, ports, dns, traceroute)
  +-- SQLite persistence (device history, scan runs)
```

### Why FastAPI?
- Async scanning performance (concurrency)
- Clean API contract between UI and backend
- Easier packaging than embedded Python

---

## Repository Layout (Target)

```
.
├── client/                         # C# WPF app (MVVM)
│   ├── NetPulse.Client/
│   ├── NetPulse.Client.Tests/
│   └── ...
├── backend/                        # Python FastAPI service
│   ├── app/
│   │   ├── main.py                 # FastAPI entrypoint
│   │   ├── api/                    # routes
│   │   ├── core/                   # config, logging
│   │   ├── services/               # scanners + diagnostics
│   │   ├── models/                 # pydantic DTOs
│   │   ├── storage/                # sqlite repo
│   │   └── data/oui/               # vendor DB (offline)
│   ├── tests/
│   ├── requirements.txt
│   └── README.md
├── docs/
│   ├── screenshots/
│   ├── architecture-diagram.png
│   └── api-contract.md
├── samples/                        # sanitized sample outputs
└── README.md
```

---

## API (High-Level Contract)

Typical endpoints (planned):

- `GET /health`
- `POST /scan/devices` → returns device list + scan metadata
- `POST /scan/ports` → returns open ports/services for a target
- `GET /tools/ping?host=...`
- `GET /tools/traceroute?host=...`
- `GET /tools/dns?name=...&type=A`
- `GET /devices` → device history (from SQLite)
- `GET /alerts` → new device / changes

---

## Getting Started (Dev)

### Prerequisites
- Windows 10/11
- Python 3.10+ (recommended)
- .NET SDK (matching your WPF project, e.g., .NET 8)

### 1) Run the backend (FastAPI)
```powershell
cd backend
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --host 127.0.0.1 --port 8787 --reload
```

Check:
- `http://127.0.0.1:8787/health`

### 2) Run the WPF client
- Open `client/NetPulse.Client.sln` in Visual Studio
- Run the project
- Ensure the backend URL matches (default: `http://127.0.0.1:8787`)

---

## Packaging (Planned)

### Backend
- Bundle using PyInstaller (or ship a Python runtime folder)
- Run as a background process started by the WPF app

### Client
- Publish WPF as a self-contained build (optional)
- Optional installer (Inno Setup / WiX)

---

## Security & Ethics

- Scanning is **rate-limited** and designed for **authorized environments**
- No credential harvesting, brute forcing, or exploitation features
- Logs/exports should avoid collecting sensitive data beyond network identifiers (IP/MAC/hostname)

---

## Roadmap

### Phase 1 (MVP)
- [ ] Device discovery (ping sweep + ARP parse)
- [ ] Vendor lookup (offline OUI)
- [ ] Basic device list UI + last seen
- [ ] SQLite persistence

### Phase 2 (Fing-like)
- [ ] Ping / Traceroute / DNS tools UI
- [ ] Top-ports scanning module
- [ ] New device alerts + scan history view

### Phase 3 (Pro)
- [ ] Latency graphs + internet health
- [ ] Reporting export (HTML/PDF)
- [ ] Risk scoring + device classification heuristics

---

## License
Pick one:
- MIT
- Apache-2.0
