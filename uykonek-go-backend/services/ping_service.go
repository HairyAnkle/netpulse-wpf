package services

import (
	"context"
	"errors"
	"net"
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
	return &PingService{timeout: timeout, ports: []int{80, 443, 22}}
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
