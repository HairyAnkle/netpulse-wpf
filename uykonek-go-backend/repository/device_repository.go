package repository

import (
	"sync"
	"time"
	"uykonek-backend/models"
)

type DeviceRepository struct {
	mu      sync.RWMutex
	devices map[string]models.Device
}

func NewDeviceRepository() *DeviceRepository {
	return &DeviceRepository{devices: make(map[string]models.Device)}
}

func (r *DeviceRepository) Upsert(device models.Device, seenAt time.Time) models.Device {
	r.mu.Lock()
	defer r.mu.Unlock()

	existing, ok := r.devices[device.Ip]
	if ok {
		existing.LastSeen = seenAt
		if device.Mac != "" {
			existing.Mac = device.Mac
		}
		if device.Hostname != "" {
			existing.Hostname = device.Hostname
		}
		if device.Vendor != "" {
			existing.Vendor = device.Vendor
		}
		existing.IsNew = false
		r.devices[device.Ip] = existing
		return existing
	}

	device.FirstSeen = seenAt
	device.LastSeen = seenAt
	device.IsNew = true
	r.devices[device.Ip] = device
	return device
}

func (r *DeviceRepository) List() []models.Device {
	r.mu.RLock()
	defer r.mu.RUnlock()

	result := make([]models.Device, 0, len(r.devices))
	for _, d := range r.devices {
		result = append(result, d)
	}
	return result
}
