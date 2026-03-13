package services

import (
	"bufio"
	"context"
	"log"
	"net"
	"os"
	"os/exec"
	"sort"
	"strings"
	"sync"
	"time"
	"uykonek-backend/models"
	"uykonek-backend/utils"
)

type ARPScanner struct {
	workers     int
	pingService *PingService
	vendor      *utils.VendorLookup
}

func NewARPScanner(workers int, pingService *PingService, vendor *utils.VendorLookup) *ARPScanner {
	return &ARPScanner{workers: workers, pingService: pingService, vendor: vendor}
}

func (s *ARPScanner) ScanSubnet(ctx context.Context, hosts []string) []models.Device {
	warmARP(ctx, hosts)
	arpTable := readARPTable()

	jobs := make(chan string)
	results := make(chan models.Device, len(hosts))

	workerCount := s.workers
	if workerCount < 1 {
		workerCount = 50
	}

	var wg sync.WaitGroup
	for i := 0; i < workerCount; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			for ip := range jobs {
				if d, ok := s.scanHost(ctx, ip, arpTable); ok {
					results <- d
				}
			}
		}()
	}

	go func() {
		defer close(jobs)
		for _, ip := range hosts {
			select {
			case <-ctx.Done():
				return
			case jobs <- ip:
			}
		}
	}()

	wg.Wait()
	close(results)

	seen := make(map[string]struct{}, len(hosts))
	devices := make([]models.Device, 0)
	for d := range results {
		if _, ok := seen[d.Ip]; ok {
			continue
		}
		seen[d.Ip] = struct{}{}
		devices = append(devices, d)
	}

	for ip, mac := range arpTable {
		if _, ok := seen[ip]; ok || mac == "" || isBroadcastIP(ip) || strings.EqualFold(mac, "FF:FF:FF:FF:FF:FF") {
			continue
		}
		hostname := resolveHostname(ip)
		devices = append(devices, models.Device{
			Ip:       ip,
			Mac:      mac,
			Hostname: hostname,
			Vendor:   s.vendor.Lookup(mac),
		})
		seen[ip] = struct{}{}
		log.Printf("Discovered device %s (ARP cache)", ip)
	}

	sort.Slice(devices, func(i, j int) bool { return devices[i].Ip < devices[j].Ip })
	return devices
}

func (s *ARPScanner) scanHost(ctx context.Context, ip string, arpTable map[string]string) (models.Device, bool) {
	mac := arpTable[ip]
	alive := mac != "" && !strings.EqualFold(mac, "FF:FF:FF:FF:FF:FF")

	result := s.pingService.Ping(ctx, ip)
	if result.Alive {
		alive = true
	}
	if !alive {
		return models.Device{}, false
	}

	hostname := resolveHostname(ip)
	if mac == "" {
		mac = readARPEntry(ip)
	}
	vendor := s.vendor.Lookup(mac)

	log.Printf("Discovered device %s", ip)
	if isBroadcastIP(ip) {
		return models.Device{}, false
	}
	return models.Device{Ip: ip, Mac: mac, Hostname: hostname, Vendor: vendor}, true
}

func resolveHostname(ip string) string {
	names, err := net.LookupAddr(ip)
	if err != nil || len(names) == 0 {
		return ""
	}
	return strings.TrimSuffix(names[0], ".")
}

func readARPEntry(ip string) string {
	return readARPTable()[ip]
}

func readARPTable() map[string]string {
	if entries := readARPProc(); len(entries) > 0 {
		return entries
	}
	if entries := readARPByCommand(); len(entries) > 0 {
		return entries
	}
	return map[string]string{}
}

func readARPProc() map[string]string {
	f, err := os.Open("/proc/net/arp")
	if err != nil {
		return map[string]string{}
	}
	defer f.Close()

	entries := make(map[string]string)
	scanner := bufio.NewScanner(f)
	first := true
	for scanner.Scan() {
		if first {
			first = false
			continue
		}
		fields := strings.Fields(scanner.Text())
		if len(fields) < 4 {
			continue
		}
		ip := fields[0]
		mac := normalizeMAC(fields[3])
		if mac != "" {
			entries[ip] = mac
		}
	}
	return entries
}

func readARPByCommand() map[string]string {
	cmd := exec.Command("arp", "-a")
	out, err := cmd.Output()
	if err != nil {
		return map[string]string{}
	}

	entries := make(map[string]string)
	scanner := bufio.NewScanner(strings.NewReader(string(out)))
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}
		fields := strings.Fields(line)
		var ip, mac string
		for _, f := range fields {
			if parsed := net.ParseIP(strings.Trim(f, "()")); parsed != nil && parsed.To4() != nil {
				ip = parsed.String()
				continue
			}
			if m := normalizeMAC(strings.Trim(f, "()[]")); m != "" {
				mac = m
			}
		}
		if ip != "" && mac != "" {
			entries[ip] = mac
		}
	}
	return entries
}

func normalizeMAC(mac string) string {
	m := strings.ToUpper(strings.ReplaceAll(strings.ReplaceAll(strings.TrimSpace(mac), "-", ":"), ".", ""))
	if m == "" || m == "INCOMPLETE" || m == "<INCOMPLETE>" || m == "00:00:00:00:00:00" {
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
		}
		return strings.Join(parts, ":")
	}
	if len(m) != 12 {
		return ""
	}
	return strings.Join([]string{m[0:2], m[2:4], m[4:6], m[6:8], m[8:10], m[10:12]}, ":")
}

func isBroadcastIP(ip string) bool {
	parsed := net.ParseIP(strings.TrimSpace(ip))
	if parsed == nil {
		return false
	}
	v4 := parsed.To4()
	if v4 == nil {
		return false
	}
	return v4[3] == 255
}

func warmARP(ctx context.Context, hosts []string) {
	jobs := make(chan string)
	workerCount := 64
	if len(hosts) < workerCount {
		workerCount = len(hosts)
	}
	if workerCount < 1 {
		return
	}

	var wg sync.WaitGroup
	for i := 0; i < workerCount; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			d := net.Dialer{Timeout: 220 * time.Millisecond}
			for ip := range jobs {
				select {
				case <-ctx.Done():
					return
				default:
				}
				conn, _ := d.DialContext(ctx, "udp", net.JoinHostPort(ip, "9"))
				if conn != nil {
					_ = conn.Close()
				}
			}
		}()
	}

	for _, ip := range hosts {
		select {
		case <-ctx.Done():
			close(jobs)
			wg.Wait()
			return
		case jobs <- ip:
		}
	}
	close(jobs)
	wg.Wait()
}
