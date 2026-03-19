package services

import (
	"context"
	"log"
	"time"
	"uykonek-backend/models"
	"uykonek-backend/repository"
	"uykonek-backend/utils"
)

type DeviceService struct {
	repo    *repository.DeviceRepository
	scanner *ARPScanner
}

func NewDeviceService(repo *repository.DeviceRepository, scanner *ARPScanner) *DeviceService {
	return &DeviceService{repo: repo, scanner: scanner}
}

func (s *DeviceService) ScanNetwork(ctx context.Context) ([]models.Device, error) {
	_, subnet, err := utils.GetLocalIPv4AndSubnet()
	if err != nil {
		return nil, err
	}

	hosts, err := utils.HostsInSubnet(subnet)
	if err != nil {
		return nil, err
	}

	log.Printf("Starting network scan on %s", subnet.String())
	rawDevices := s.scanner.ScanSubnet(ctx, hosts)
	now := time.Now().UTC()

	devices := make([]models.Device, 0, len(rawDevices))
	for _, d := range rawDevices {
		devices = append(devices, s.repo.Upsert(d, now))
	}
	return devices, nil
}
