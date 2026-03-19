package api

import (
	"context"
	"net/http"
	"time"
	"uykonek-backend/services"
	"uykonek-backend/utils"
)

type PortHandler struct {
	scanner *services.PortScanner
	timeout time.Duration
}

func NewPortHandler(scanner *services.PortScanner, timeout time.Duration) *PortHandler {
	return &PortHandler{scanner: scanner, timeout: timeout}
}

func (h *PortHandler) HandlePortScan(w http.ResponseWriter, r *http.Request) {
	ip := r.URL.Query().Get("ip")
	if _, err := utils.ParseIPv4(ip); err != nil {
		writeError(w, http.StatusBadRequest, err.Error())
		return
	}

	ctx, cancel := context.WithTimeout(r.Context(), h.timeout)
	defer cancel()

	result := h.scanner.Scan(ctx, ip, services.CommonPorts)
	writeJSON(w, http.StatusOK, result)
}
