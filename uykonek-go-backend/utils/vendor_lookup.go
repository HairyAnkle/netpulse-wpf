package utils

import (
	"encoding/json"
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

	if path == "" {
		return vl
	}

	if !filepath.IsAbs(path) {
		if wd, err := os.Getwd(); err == nil {
			path = filepath.Join(wd, path)
		}
	}

	content, err := os.ReadFile(path)
	if err != nil {
		return vl
	}

	loaded := map[string]string{}
	if err := json.Unmarshal(content, &loaded); err != nil {
		return vl
	}

	for k, v := range loaded {
		prefix := normalizePrefix(k)
		if prefix != "" {
			vl.prefixes[prefix] = v
		}
	}

	return vl
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
