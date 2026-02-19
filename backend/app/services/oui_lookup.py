from __future__ import annotations

import json
from pathlib import Path


class OuiLookupService:
    """Resolve MAC address vendors from 24-bit OUI mapping data.

    Preferred format is JSON from repositories like `ttafsir/mac-oui-lookup`
    where keys are OUIs and values are vendor names.

    Supported key styles include:
    - `001A2B`
    - `00:1A:2B`
    - `00-1A-2B`
    - `001A2B/24`

    Legacy CSV fallback is also supported for compatibility:
    - `00:1A:2B,Vendor Name`
    """

    def __init__(self, oui_file: Path) -> None:
        self._map: dict[str, str] = {}
        if not oui_file.exists():
            return

        if oui_file.suffix.lower() == ".json":
            self._load_json(oui_file)
            return

        self._load_csv(oui_file)

    def _load_json(self, oui_file: Path) -> None:
        try:
            payload = json.loads(oui_file.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return

        if isinstance(payload, dict):
            for raw_key, raw_value in payload.items():
                key = self._normalize_oui(str(raw_key))
                if key and raw_value is not None:
                    vendor = str(raw_value).strip()
                    if vendor:
                        self._map[key] = vendor

    def _load_csv(self, oui_file: Path) -> None:
        for line in oui_file.read_text(encoding="utf-8").splitlines():
            text = line.strip()
            if not text or text.startswith("#") or "," not in text:
                continue
            prefix, vendor = text.split(",", maxsplit=1)
            key = self._normalize_oui(prefix)
            value = vendor.strip()
            if key and value:
                self._map[key] = value

    def lookup(self, mac: str | None) -> str | None:
        oui = self._extract_oui(mac)
        if not oui:
            return None
        return self._map.get(oui)

    def _extract_oui(self, mac: str | None) -> str | None:
        if not mac:
            return None

        token = self._strip_mac_separators(mac.strip().upper())
        if len(token) < 6:
            return None

        oui = token[:6]
        if any(ch not in "0123456789ABCDEF" for ch in oui):
            return None
        return oui

    def _normalize_oui(self, raw: str) -> str | None:
        token = self._strip_mac_separators(raw.strip().upper())
        if len(token) < 6:
            return None

        oui = token[:6]
        if any(ch not in "0123456789ABCDEF" for ch in oui):
            return None
        return oui

    @staticmethod
    def _strip_mac_separators(value: str) -> str:
        return value.replace("-", "").replace(":", "").replace(".", "")
