package services

import (
	"context"
	"errors"
	"net"
	"os/exec"
	"runtime"
	"strconv"
	"strings"
	"time"
)

type PingResult struct {
	IP        string `json:"ip"`
	Alive     bool   `json:"alive"`
	LatencyMS int64  `json:"latency_ms"`
}

type PingService struct {
	timeout time.Duration
	ports   []int
}

func NewPingService(timeout time.Duration) *PingService {
	return &PingService{timeout: timeout, ports: []int{80, 443, 22, 445, 139, 3389}}
}

func (s *PingService) Ping(ctx context.Context, ip string) PingResult {
	res := PingResult{IP: ip, Alive: false, LatencyMS: 0}
	for _, port := range s.ports {
		select {
		case <-ctx.Done():
			return res
		default:
		}

		start := time.Now()
		conn, err := (&net.Dialer{Timeout: s.timeout}).DialContext(ctx, "tcp", net.JoinHostPort(ip, strconv.Itoa(port)))
		latency := time.Since(start).Milliseconds()
		if conn != nil {
			_ = conn.Close()
			res.Alive = true
			res.LatencyMS = latency
			return res
		}
		if isHostReachableError(err) {
			res.Alive = true
			res.LatencyMS = latency
			return res
		}
	}

	// Fallback: system ping is more reliable for hosts that do not expose TCP ports.
	if latency, ok := pingByCommand(ctx, ip, s.timeout); ok {
		res.Alive = true
		res.LatencyMS = latency
	}
	return res
}

func isHostReachableError(err error) bool {
	if err == nil {
		return true
	}
	if errors.Is(err, context.DeadlineExceeded) || errors.Is(err, context.Canceled) {
		return false
	}
	msg := strings.ToLower(err.Error())
	return strings.Contains(msg, "connection refused")
}

func pingByCommand(ctx context.Context, ip string, timeout time.Duration) (int64, bool) {
	var cmd *exec.Cmd
	if runtime.GOOS == "windows" {
		ms := int(timeout.Milliseconds())
		if ms < 500 {
			ms = 500
		}
		cmd = exec.CommandContext(ctx, "ping", "-n", "1", "-w", strconv.Itoa(ms), ip)
	} else {
		sec := int(timeout.Seconds())
		if sec < 1 {
			sec = 1
		}
		cmd = exec.CommandContext(ctx, "ping", "-c", "1", "-W", strconv.Itoa(sec), ip)
	}

	start := time.Now()
	out, err := cmd.CombinedOutput()
	latency := time.Since(start).Milliseconds()
	if err != nil {
		return 0, false
	}

	body := strings.ToLower(string(out))
	if strings.Contains(body, "ttl=") || strings.Contains(body, "1 received") || strings.Contains(body, "bytes from") {
		if latency < 0 {
			latency = 0
		}
		return latency, true
	}
	return 0, false
}
