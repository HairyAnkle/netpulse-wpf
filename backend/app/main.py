from __future__ import annotations

import asyncio
import logging
from datetime import UTC, datetime
from pathlib import Path

from fastapi import FastAPI, HTTPException

from app.models.dto import DeviceDto, ScanDevicesResponseDto, ScanMetadataDto
from app.services.network_scanner import NetworkScannerService
from app.services.oui_lookup import OuiLookupService
from app.storage.database import Database

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("netpulse-backend")

app = FastAPI(title="NetPulse API", version="0.1.0")
base_dir = Path(__file__).resolve().parents[1]
db = Database(base_dir / "data" / "netpulse.db")
scanner = NetworkScannerService()
oui_file = base_dir / "data" / "oui.json"
if not oui_file.exists():
    oui_file = base_dir / "data" / "oui_sample.csv"
oui = OuiLookupService(oui_file)
run_dir = base_dir / "runs"
run_dir.mkdir(parents=True, exist_ok=True)

scan_lock = asyncio.Lock()
last_scan_started_at: datetime | None = None
MIN_SCAN_INTERVAL_SECONDS = 3
MAX_SCAN_SECONDS = 90


@app.on_event("startup")
async def startup_event() -> None:
    await db.initialize()


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok", "service": "netpulse-backend"}


@app.post("/scan/devices", response_model=ScanDevicesResponseDto)
async def scan_devices() -> ScanDevicesResponseDto:
    global last_scan_started_at

    now = datetime.now(UTC)
    if last_scan_started_at and (now - last_scan_started_at).total_seconds() < MIN_SCAN_INTERVAL_SECONDS:
        raise HTTPException(status_code=429, detail="Scan rate limit exceeded. Please wait a few seconds.")

    if scan_lock.locked():
        raise HTTPException(status_code=409, detail="A scan is already running.")

    async with scan_lock:
        last_scan_started_at = datetime.now(UTC)
        subnet = await scanner.detect_subnet()
        ts_start = datetime.now(UTC)
        scan_id = await db.create_scan(subnet=subnet, ts_start=ts_start)

        try:
            observations = await asyncio.wait_for(scanner.discover(subnet), timeout=MAX_SCAN_SECONDS)
        except asyncio.TimeoutError as exc:
            logger.warning("Scan %s timed out", scan_id)
            raise HTTPException(status_code=504, detail="Scan timed out.") from exc

        devices: list[DeviceDto] = []
        for obs in observations:
            if not obs.mac:
                continue
            vendor = oui.lookup(obs.mac)
            timestamp = datetime.now(UTC)
            first_seen, is_new = await db.upsert_device(obs.mac, vendor, timestamp)
            await db.insert_observation(obs.mac, obs.ip, obs.hostname, timestamp, scan_id)

            devices.append(
                DeviceDto(
                    ip=obs.ip,
                    mac=obs.mac,
                    hostname=obs.hostname,
                    vendor=vendor,
                    first_seen=first_seen,
                    last_seen=timestamp,
                    is_new=is_new,
                )
            )

        ts_end = datetime.now(UTC)
        await db.complete_scan(scan_id=scan_id, ts_end=ts_end, host_count=len(devices))

        payload = ScanDevicesResponseDto(
            scan=ScanMetadataDto(
                scan_id=scan_id,
                subnet=subnet,
                ts_start=ts_start,
                ts_end=ts_end,
                host_count=len(devices),
            ),
            devices=devices,
        )
        output_file = run_dir / f"scan_{scan_id}.json"
        output_file.write_text(payload.model_dump_json(indent=2), encoding="utf-8")
        logger.info("Completed scan %s on subnet %s (%s hosts)", scan_id, subnet, len(devices))
        return payload
