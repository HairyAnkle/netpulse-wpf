package config

import "os"

type Config struct {
	Address     string
	WorkerCount int
	PortTimeout string
	OUIDataPath string
	PingTimeout string
	ScanTimeout string
}

func Load() Config {
	cfg := Config{
		Address:     ":8080",
		WorkerCount: 50,
		PortTimeout: "600ms",
		PingTimeout: "1s",
		ScanTimeout: "6s",
		OUIDataPath: "../backend/data/latest_oui_lookup.json",
	}

	if v := os.Getenv("UYKONEK_ADDR"); v != "" {
		cfg.Address = v
	}
	if v := os.Getenv("UYKONEK_OUI_PATH"); v != "" {
		cfg.OUIDataPath = v
	}

	return cfg
}
