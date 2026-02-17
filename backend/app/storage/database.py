from __future__ import annotations

from datetime import UTC, datetime
from pathlib import Path

import aiosqlite


class Database:
    def __init__(self, db_path: Path) -> None:
        self.db_path = db_path

    async def initialize(self) -> None:
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        async with aiosqlite.connect(self.db_path) as db:
            await db.executescript(
                """
                CREATE TABLE IF NOT EXISTS devices(
                    mac TEXT PRIMARY KEY,
                    vendor TEXT,
                    nickname TEXT,
                    first_seen TEXT NOT NULL,
                    last_seen TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS scans(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ts_start TEXT NOT NULL,
                    ts_end TEXT,
                    subnet TEXT NOT NULL,
                    host_count INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS observations(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    mac TEXT NOT NULL,
                    ip TEXT NOT NULL,
                    hostname TEXT,
                    ts TEXT NOT NULL,
                    scan_id INTEGER NOT NULL,
                    FOREIGN KEY(mac) REFERENCES devices(mac),
                    FOREIGN KEY(scan_id) REFERENCES scans(id)
                );
                """
            )
            await db.commit()

    async def create_scan(self, subnet: str, ts_start: datetime) -> int:
        async with aiosqlite.connect(self.db_path) as db:
            cursor = await db.execute(
                "INSERT INTO scans(ts_start, subnet, host_count) VALUES(?,?,0)",
                (ts_start.isoformat(), subnet),
            )
            await db.commit()
            return int(cursor.lastrowid)

    async def complete_scan(self, scan_id: int, ts_end: datetime, host_count: int) -> None:
        async with aiosqlite.connect(self.db_path) as db:
            await db.execute(
                "UPDATE scans SET ts_end=?, host_count=? WHERE id=?",
                (ts_end.isoformat(), host_count, scan_id),
            )
            await db.commit()

    async def upsert_device(self, mac: str, vendor: str | None, now: datetime) -> tuple[datetime, bool]:
        async with aiosqlite.connect(self.db_path) as db:
            cursor = await db.execute("SELECT first_seen FROM devices WHERE mac=?", (mac,))
            existing = await cursor.fetchone()
            if existing:
                first_seen = datetime.fromisoformat(existing[0])
                await db.execute(
                    "UPDATE devices SET vendor=COALESCE(?,vendor), last_seen=? WHERE mac=?",
                    (vendor, now.isoformat(), mac),
                )
                await db.commit()
                return first_seen, False

            await db.execute(
                "INSERT INTO devices(mac, vendor, nickname, first_seen, last_seen) VALUES(?,?,?,?,?)",
                (mac, vendor, None, now.isoformat(), now.isoformat()),
            )
            await db.commit()
            return now, True

    async def insert_observation(
        self,
        mac: str,
        ip: str,
        hostname: str | None,
        ts: datetime,
        scan_id: int,
    ) -> None:
        async with aiosqlite.connect(self.db_path) as db:
            await db.execute(
                "INSERT INTO observations(mac, ip, hostname, ts, scan_id) VALUES(?,?,?,?,?)",
                (mac, ip, hostname, ts.isoformat(), scan_id),
            )
            await db.commit()
