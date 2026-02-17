import asyncio
import ipaddress
import platform
import re
import socket
from dataclasses import dataclass


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
        ip = self._detect_local_ipv4()
        if ip:
            parts = ip.split(".")
            return ".".join(parts[:3]) + ".0/24"
        return "192.168.1.0/24"

    def _detect_local_ipv4(self) -> str | None:
        # Prefer the outbound interface used for internet-bound traffic.
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            try:
                sock.connect(("8.8.8.8", 80))
                candidate = sock.getsockname()[0]
                if candidate and not candidate.startswith("127."):
                    return candidate
            except OSError:
                pass

        hostname = socket.gethostname()
        try:
            candidates = socket.getaddrinfo(hostname, None, socket.AF_INET)
        except OSError:
            return None

        for item in candidates:
            candidate = item[4][0]
            if candidate and not candidate.startswith("127."):
                return candidate
        return None

    async def discover(self, subnet: str) -> list[HostObservation]:
        network = ipaddress.ip_network(subnet, strict=False)
        semaphore = asyncio.Semaphore(self.concurrency)

        async def probe(ip: str) -> str | None:
            async with semaphore:
                if await self._ping(ip):
                    return ip
                return None

        alive_ips = [ip for ip in await asyncio.gather(*(probe(str(h)) for h in network.hosts())) if ip]

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
        table = await self._read_arp_with_command("arp", "-a")
        if table:
            return table
        return await self._read_arp_with_command("ip", "neigh")

    async def _read_arp_with_command(self, *cmd: str) -> dict[str, str]:
        try:
            proc = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.DEVNULL,
            )
        except FileNotFoundError:
            return {}

        out, _ = await proc.communicate()
        text = out.decode(errors="ignore")
        return self._parse_neighbor_output(text)

    @staticmethod
    def _parse_neighbor_output(text: str) -> dict[str, str]:
        table: dict[str, str] = {}
        mac_pattern = r"([0-9a-f]{2}[:-]){5}[0-9a-f]{2}"

        # Linux arp: ? (192.168.1.1) at aa:bb:cc:dd:ee:ff [ether] on wlan0
        linux_arp = re.compile(rf"\((?P<ip>\d+\.\d+\.\d+\.\d+)\)\s+at\s+(?P<mac>{mac_pattern})", re.IGNORECASE)
        # Windows arp: 192.168.1.1          aa-bb-cc-dd-ee-ff     dynamic
        windows_arp = re.compile(rf"(?P<ip>\d+\.\d+\.\d+\.\d+)\s+(?P<mac>{mac_pattern})", re.IGNORECASE)
        # ip neigh: 192.168.1.1 dev wlan0 lladdr aa:bb:cc:dd:ee:ff REACHABLE
        ip_neigh = re.compile(rf"(?P<ip>\d+\.\d+\.\d+\.\d+).*?lladdr\s+(?P<mac>{mac_pattern})", re.IGNORECASE)

        for pattern in (linux_arp, windows_arp, ip_neigh):
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
