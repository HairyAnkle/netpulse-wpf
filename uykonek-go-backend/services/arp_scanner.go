package services

import (
	"bufio"
	"context"
	"log"
	"net"
	"os"
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
				if d, ok := s.scanHost(ctx, ip); ok {
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

	devices := make([]models.Device, 0)
	for d := range results {
		devices = append(devices, d)
	}
	return devices
}

func (s *ARPScanner) scanHost(ctx context.Context, ip string) (models.Device, bool) {
	result := s.pingService.Ping(ctx, ip)
	if !result.Alive {
		return models.Device{}, false
	}

	hostname := resolveHostname(ip)
	mac := readARPEntry(ip)
	vendor := s.vendor.Lookup(mac)

	log.Printf("Discovered device %s", ip)
	return models.Device{
		Ip:       ip,
		Mac:      mac,
		Hostname: hostname,
		Vendor:   vendor,
	}, true
}

func resolveHostname(ip string) string {
	names, err := net.LookupAddr(ip)
	if err != nil || len(names) == 0 {
		return ""
	}
	return strings.TrimSuffix(names[0], ".")
}

func readARPEntry(ip string) string {
	f, err := os.Open("/proc/net/arp")
	if err != nil {
		return ""
	}
	defer f.Close()

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
		if fields[0] == ip {
			if fields[3] == "00:00:00:00:00:00" {
				return ""
			}
			return strings.ToUpper(fields[3])
		}
	}
	return ""
}

func warmARP(ctx context.Context, hosts []string) {
	d := net.Dialer{Timeout: 300 * time.Millisecond}
	for _, ip := range hosts {
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
}
