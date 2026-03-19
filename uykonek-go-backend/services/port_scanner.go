package services

import (
	"context"
	"log"
	"net"
	"sort"
	"strconv"
	"sync"
	"time"
)

var CommonPorts = []int{21, 22, 23, 25, 53, 80, 110, 139, 143, 443, 445, 8080}

type PortScanResult struct {
	IP        string `json:"ip"`
	OpenPorts []int  `json:"open_ports"`
}

type PortScanner struct {
	timeout time.Duration
}

func NewPortScanner(timeout time.Duration) *PortScanner {
	return &PortScanner{timeout: timeout}
}

func (s *PortScanner) Scan(ctx context.Context, ip string, ports []int) PortScanResult {
	if len(ports) == 0 {
		ports = CommonPorts
	}

	jobs := make(chan int)
	results := make(chan int, len(ports))
	workers := min(32, len(ports))
	if workers < 1 {
		workers = 1
	}

	var wg sync.WaitGroup
	for i := 0; i < workers; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			for p := range jobs {
				open := s.scanPort(ctx, ip, p)
				if open {
					results <- p
				}
			}
		}()
	}

	go func() {
		defer close(jobs)
		for _, p := range ports {
			select {
			case <-ctx.Done():
				return
			case jobs <- p:
			}
		}
	}()

	wg.Wait()
	close(results)

	openPorts := make([]int, 0)
	for p := range results {
		openPorts = append(openPorts, p)
	}
	sort.Ints(openPorts)

	return PortScanResult{IP: ip, OpenPorts: openPorts}
}

func (s *PortScanner) scanPort(ctx context.Context, ip string, port int) bool {
	dialer := &net.Dialer{Timeout: s.timeout}
	conn, err := dialer.DialContext(ctx, "tcp", net.JoinHostPort(ip, strconv.Itoa(port)))
	if err != nil {
		return false
	}
	_ = conn.Close()
	log.Printf("Port %d open on %s", port, ip)
	return true
}

func min(a, b int) int {
	if a < b {
		return a
	}
	return b
}
