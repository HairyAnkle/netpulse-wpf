package api

import (
	"context"
	"encoding/json"
	"net/http"
	"time"
	"uykonek-backend/services"
)

type ScanHandler struct {
	deviceService *services.DeviceService
	timeout       time.Duration
}

func NewScanHandler(deviceService *services.DeviceService, timeout time.Duration) *ScanHandler {
	return &ScanHandler{deviceService: deviceService, timeout: timeout}
}

func (h *ScanHandler) HandleNetworkScan(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), h.timeout)
	defer cancel()

	devices, err := h.deviceService.ScanNetwork(ctx)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}

	writeJSON(w, http.StatusOK, devices)
}

func writeJSON(w http.ResponseWriter, status int, payload any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(payload)
}

func writeError(w http.ResponseWriter, status int, err string) {
	writeJSON(w, status, map[string]string{"error": err})
}
