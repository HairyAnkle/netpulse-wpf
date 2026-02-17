import asyncio
import ipaddress
import platform
import re
import socket
from dataclasses import dataclass
from datetime import UTC, datetime


@dataclass
class HostObservation:
    ip: str
    mac: str | None
    hostname: str | None


class NetworkScannerService:
    def __init__(self, ping_timeout_s: float = 0.8, concurrency: int = 128) -> None:
        self.ping_timeout_s = ping_timeout_s
        self.concurrency = concurrency

    async def detect_subnet(self) -> str:
        hostname = socket.gethostname()
        candidates = socket.getaddrinfo(hostname, None, socket.AF_INET)
        for item in candidates:
            ip = item[4][0]
            if ip.startswith("127."):
                continue
            parts = ip.split(".")
            return ".".join(parts[:3]) + ".0/24"
        return "192.168.1.0/24"

    async def discover(self, subnet: str) -> list[HostObservation]:
        network = ipaddress.ip_network(subnet, strict=False)
        semaphore = asyncio.Semaphore(self.concurrency)

        async def probe(ip: str) -> str | None:
            async with semaphore:
                if await self._ping(ip):
                    return ip
                return None

        alive_ips = [
            ip
            for ip in await asyncio.gather(*(probe(str(h)) for h in network.hosts()))
            if ip
        ]

        arp_table = await self._read_arp_table()
        observations: list[HostObservation] = []
        for ip in alive_ips:
            mac = arp_table.get(ip)
            hostname = await self._resolve_hostname(ip)
            observations.append(HostObservation(ip=ip, mac=mac, hostname=hostname))
        return observations

    async def _ping(self, ip: str) -> bool:
        system = platform.system().lower()
        if "windows" in system:
            cmd = ["ping", "-n", "1", "-w", str(int(self.ping_timeout_s * 1000)), ip]
        else:
            cmd = ["ping", "-c", "1", "-W", str(max(1, int(self.ping_timeout_s))), ip]
        try:
            proc = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.DEVNULL,
                stderr=asyncio.subprocess.DEVNULL,
            )
            await asyncio.wait_for(proc.wait(), timeout=self.ping_timeout_s + 0.5)
            return proc.returncode == 0
        except Exception:
            return False

    async def _read_arp_table(self) -> dict[str, str]:
        proc = await asyncio.create_subprocess_exec(
            "arp",
            "-a",
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.DEVNULL,
        )
        out, _ = await proc.communicate()
        text = out.decode(errors="ignore")
        table: dict[str, str] = {}
        pattern = re.compile(
            r"(?P<ip>\d+\.\d+\.\d+\.\d+)\s+(?P<mac>([0-9a-f]{2}[:-]){5}[0-9a-f]{2})",
            re.IGNORECASE,
        )
        for m in pattern.finditer(text):
            table[m.group("ip")] = m.group("mac").replace("-", ":").upper()
        return table

    async def _resolve_hostname(self, ip: str) -> str | None:
        loop = asyncio.get_running_loop()
        try:
            value = await loop.run_in_executor(None, socket.gethostbyaddr, ip)
            return value[0]
        except Exception:
            return None
