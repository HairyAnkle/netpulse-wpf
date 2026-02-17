# Setup and Run

## Prerequisites

- Windows 10/11 for WPF client runtime
- Python 3.10+
- .NET 8 SDK

## Backend

```bash
cd backend
python -m venv .venv
# Windows PowerShell: .venv\Scripts\Activate.ps1
# bash: source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --host 127.0.0.1 --port 8787
```

## Client

```bash
cd client/NetPulse.Client
dotnet run
```

In the Dashboard screen:
- Click **Scan Network**
- Results appear in the DataGrid
- Use **Copy IP** / **Copy MAC** on the selected row

## Notes and limitations

- ARP parsing can require elevated privileges or an ARP cache warm-up depending on OS policy.
- Hostname detection is best-effort (`socket.gethostbyaddr`).
- Backend URL defaults to `http://127.0.0.1:8787` and can be moved into persisted settings in a future iteration.
