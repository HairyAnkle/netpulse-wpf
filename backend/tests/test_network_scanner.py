import unittest

from app.services.network_scanner import NetworkScannerService


class NetworkScannerServiceTests(unittest.TestCase):
    def test_parse_neighbor_output_linux_arp(self) -> None:
        text = "? (192.168.1.12) at aa:bb:cc:dd:ee:ff [ether] on eth0"
        result = NetworkScannerService._parse_neighbor_output(text)

        self.assertEqual(result["192.168.1.12"], "AA:BB:CC:DD:EE:FF")

    def test_parse_neighbor_output_windows_arp(self) -> None:
        text = "  192.168.1.1          aa-bb-cc-dd-ee-ff     dynamic"
        result = NetworkScannerService._parse_neighbor_output(text)

        self.assertEqual(result["192.168.1.1"], "AA:BB:CC:DD:EE:FF")

    def test_parse_neighbor_output_ip_neigh(self) -> None:
        text = "192.168.1.44 dev wlan0 lladdr aa:bb:cc:dd:ee:ff REACHABLE"
        result = NetworkScannerService._parse_neighbor_output(text)

        self.assertEqual(result["192.168.1.44"], "AA:BB:CC:DD:EE:FF")


if __name__ == "__main__":
    unittest.main()
