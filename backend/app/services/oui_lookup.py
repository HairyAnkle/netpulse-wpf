from pathlib import Path


class OuiLookupService:
    def __init__(self, oui_file: Path) -> None:
        self._map: dict[str, str] = {}
        if oui_file.exists():
            for line in oui_file.read_text(encoding="utf-8").splitlines():
                if not line.strip() or line.startswith("#"):
                    continue
                prefix, vendor = line.split(",", maxsplit=1)
                self._map[prefix.strip().upper()] = vendor.strip()

    def lookup(self, mac: str | None) -> str | None:
        if not mac:
            return None
        normalized = mac.replace("-", ":").upper()
        prefix = ":".join(normalized.split(":")[:3])
        return self._map.get(prefix)
