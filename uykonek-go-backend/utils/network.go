package utils

import (
	"encoding/binary"
	"errors"
	"fmt"
	"net"
)

func GetLocalIPv4AndSubnet() (net.IP, *net.IPNet, error) {
	ifaces, err := net.Interfaces()
	if err != nil {
		return nil, nil, err
	}

	for _, iface := range ifaces {
		if iface.Flags&net.FlagUp == 0 || iface.Flags&net.FlagLoopback != 0 {
			continue
		}

		addrs, err := iface.Addrs()
		if err != nil {
			continue
		}

		for _, addr := range addrs {
			ipNet, ok := addr.(*net.IPNet)
			if !ok || ipNet.IP == nil {
				continue
			}

			ip := ipNet.IP.To4()
			if ip == nil {
				continue
			}

			mask := net.CIDRMask(24, 32)
			network := net.IPNet{IP: ip.Mask(mask), Mask: mask}
			return ip, &network, nil
		}
	}

	return nil, nil, errors.New("no active IPv4 interface found")
}

func HostsInSubnet(ipNet *net.IPNet) ([]string, error) {
	if ipNet == nil || ipNet.IP == nil || ipNet.Mask == nil {
		return nil, errors.New("invalid subnet")
	}

	baseIP := ipNet.IP.To4()
	if baseIP == nil {
		return nil, errors.New("only IPv4 subnets are supported")
	}

	ones, bits := ipNet.Mask.Size()
	if bits != 32 {
		return nil, errors.New("unexpected subnet size")
	}

	hostBits := 32 - ones
	if hostBits <= 0 {
		return nil, errors.New("subnet has no host addresses")
	}

	count := 1 << hostBits
	if count > 256 {
		count = 256
	}

	start := binary.BigEndian.Uint32(baseIP)
	hosts := make([]string, 0, count)
	for i := 0; i < count; i++ {
		ipVal := start + uint32(i)
		ipBytes := make([]byte, 4)
		binary.BigEndian.PutUint32(ipBytes, ipVal)
		hosts = append(hosts, net.IP(ipBytes).String())
	}

	return hosts, nil
}

func ParseIPv4(ipStr string) (net.IP, error) {
	ip := net.ParseIP(ipStr)
	if ip == nil || ip.To4() == nil {
		return nil, fmt.Errorf("invalid ip address")
	}
	return ip.To4(), nil
}
