# NetPulse MVP Architecture

- `client/NetPulse.Client`: WPF desktop app using MVVM.
  - `ViewModels/DashboardViewModel`: orchestrates scans and state.
  - `Services/ApiClientService`: typed HTTP calls to FastAPI backend.
- `backend/app`: FastAPI service.
  - `services/network_scanner.py`: subnet detection, ping sweep, ARP parsing, hostname lookup.
  - `services/oui_lookup.py`: offline vendor mapping from local CSV.
  - `storage/database.py`: SQLite schema + persistence.
- `backend/runs`: deterministic per-scan JSON artifacts (`scan_<id>.json`).
- `backend/data/netpulse.db`: persisted scan/device history.

Safety and MVP limits:
- User-initiated scanning only (`POST /scan/devices`).
- Timeout + rate limiting.
- No exploit/bruteforce/credential functionality.
