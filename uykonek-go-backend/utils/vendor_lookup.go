package utils

import (
	"encoding/json"
	"log"
	"net"
	"os"
	"path/filepath"
	"strings"
)

type VendorLookup struct {
	prefixes map[string]string
}

func NewVendorLookup(path string) *VendorLookup {
	vl := &VendorLookup{prefixes: map[string]string{
		"00:1A:2B": "Cisco",
		"F0:18:98": "Samsung",
		"3C:5A:B4": "Google",
		"D8:BB:2C": "Apple",
	}}

	resolved := resolveOUIPath(path)
	if resolved == "" {
		log.Printf("OUI lookup file not found, using built-in sample map only")
		return vl
	}

	content, err := os.ReadFile(resolved)
	if err != nil {
		log.Printf("failed reading OUI file %s: %v", resolved, err)
		return vl
	}

	loaded := map[string]string{}
	if err := json.Unmarshal(content, &loaded); err != nil {
		log.Printf("failed parsing OUI file %s: %v", resolved, err)
		return vl
	}

	for k, v := range loaded {
		prefix := normalizePrefix(k)
		if prefix != "" {
			vl.prefixes[prefix] = v
		}
	}

	log.Printf("loaded %d OUI prefixes from %s", len(vl.prefixes), resolved)
	return vl
}

func resolveOUIPath(path string) string {
	candidates := make([]string, 0, 4)
	if path != "" {
		candidates = append(candidates, path)
	}
	candidates = append(candidates,
		"backend/data/latest_oui_lookup.json",
		"../backend/data/latest_oui_lookup.json",
	)

	wd, _ := os.Getwd()
	for _, c := range candidates {
		if c == "" {
			continue
		}
		candidate := c
		if !filepath.IsAbs(candidate) {
			candidate = filepath.Join(wd, candidate)
		}
		if _, err := os.Stat(candidate); err == nil {
			return candidate
		}
	}
	return ""
}

func (v *VendorLookup) Lookup(mac string) string {
	normalized := normalizeMACAddress(mac)
	if normalized == "" {
		return "Unknown"
	}
	if isBroadcastMAC(normalized) {
		return "Broadcast"
	}
	if isLocallyAdministeredMAC(normalized) {
		return "Private/Randomized"
	}

	prefix := normalizePrefix(normalized)
	if prefix == "" {
		return "Unknown"
	}
	if vendor, ok := v.prefixes[prefix]; ok {
		return vendor
	}
	return "Unknown"
}

func normalizePrefix(mac string) string {
	m := strings.ToUpper(strings.ReplaceAll(strings.ReplaceAll(strings.TrimSpace(mac), "-", ":"), ".", ""))
	if m == "" {
		return ""
	}
	if strings.Contains(m, ":") {
		parts := strings.Split(m, ":")
		if len(parts) < 3 {
			return ""
		}
		p := make([]string, 0, 3)
		for _, part := range parts[:3] {
			if len(part) == 1 {
				part = "0" + part
			}
			if len(part) != 2 {
				return ""
			}
			p = append(p, part)
		}
		return strings.Join(p, ":")
	}
	if len(m) < 6 {
		return ""
	}
	return strings.Join([]string{m[:2], m[2:4], m[4:6]}, ":")
}

func normalizeMACAddress(mac string) string {
	m := strings.ToUpper(strings.ReplaceAll(strings.ReplaceAll(strings.TrimSpace(mac), "-", ":"), ".", ""))
	if m == "" {
		return ""
	}
	if strings.Contains(m, ":") {
		parts := strings.Split(m, ":")
		if len(parts) != 6 {
			return ""
		}
		for i, p := range parts {
			if len(p) == 1 {
				parts[i] = "0" + p
			}
			if len(parts[i]) != 2 {
				return ""
			}
		}
		return strings.Join(parts, ":")
	}
	if len(m) != 12 {
		return ""
	}
	return strings.Join([]string{m[0:2], m[2:4], m[4:6], m[6:8], m[8:10], m[10:12]}, ":")
}

func isBroadcastMAC(mac string) bool {
	return strings.EqualFold(mac, "FF:FF:FF:FF:FF:FF")
}

func isLocallyAdministeredMAC(mac string) bool {
	hw, err := net.ParseMAC(mac)
	if err != nil || len(hw) == 0 {
		return false
	}
	return hw[0]&0x02 == 0x02
}
