package utils

import (
	"encoding/json"
	"log"
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
	prefix := normalizePrefix(mac)
	if prefix == "" {
		return "Unknown"
	}

	if vendor, ok := v.prefixes[prefix]; ok {
		return vendor
	}
	return "Unknown"
}

func normalizePrefix(mac string) string {
	m := strings.ToUpper(strings.ReplaceAll(strings.ReplaceAll(mac, "-", ":"), ".", ""))
	if strings.Contains(m, ":") {
		parts := strings.Split(m, ":")
		if len(parts) < 3 {
			return ""
		}
		return strings.Join(parts[:3], ":")
	}

	if len(m) < 6 {
		return ""
	}
	return strings.Join([]string{m[:2], m[2:4], m[4:6]}, ":")
}
