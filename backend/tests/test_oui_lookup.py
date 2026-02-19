import json
import tempfile
import unittest
from pathlib import Path

from app.services.oui_lookup import OuiLookupService


class OuiLookupServiceTests(unittest.TestCase):
    def test_lookup_from_json_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            oui_json = Path(tmp) / "oui.json"
            oui_json.write_text(
                json.dumps(
                    {
                        "001122": "Vendor Hex",
                        "AA:BB:CC": "Vendor Colon",
                        "DD-EE-FF": "Vendor Dash",
                        "30-F3-3A": "+plugg srl",
                        "70-02-58": "01DB-METRAVIB",
                        "C4-93-13": "100fio networks technology llc",
                        "08-00-24": "10NET COMMUNICATIONS/DCA",
                    }
                ),
                encoding="utf-8",
            )
            service = OuiLookupService(oui_json)

            self.assertEqual(service.lookup("00:11:22:33:44:55"), "Vendor Hex")
            self.assertEqual(service.lookup("aa-bb-cc-00-11-22"), "Vendor Colon")
            self.assertEqual(service.lookup("DD:EE:FF:99:88:77"), "Vendor Dash")
            self.assertEqual(service.lookup("30:F3:3A:12:34:56"), "+plugg srl")
            self.assertEqual(service.lookup("70-02-58-00-00-01"), "01DB-METRAVIB")
            self.assertEqual(service.lookup("C4:93:13:AA:BB:CC"), "100fio networks technology llc")
            self.assertEqual(service.lookup("08.00.24.11.22.33"), "10NET COMMUNICATIONS/DCA")

    def test_lookup_from_csv_fallback(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            csv_file = Path(tmp) / "oui.csv"
            csv_file.write_text("00:AA:BB,CSV Vendor", encoding="utf-8")
            service = OuiLookupService(csv_file)

            self.assertEqual(service.lookup("00:aa:bb:00:11:22"), "CSV Vendor")

    def test_invalid_mac_returns_none(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            oui_json = Path(tmp) / "oui.json"
            oui_json.write_text(json.dumps({"001122": "Vendor"}), encoding="utf-8")
            service = OuiLookupService(oui_json)

            self.assertIsNone(service.lookup("invalid"))


if __name__ == "__main__":
    unittest.main()
