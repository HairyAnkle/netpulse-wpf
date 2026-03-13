package api

import (
	"context"
	"net/http"
	"time"
	"uykonek-backend/services"
	"uykonek-backend/utils"
)

type PingHandler struct {
	service *services.PingService
	timeout time.Duration
}

func NewPingHandler(service *services.PingService, timeout time.Duration) *PingHandler {
	return &PingHandler{service: service, timeout: timeout}
}

func (h *PingHandler) HandlePing(w http.ResponseWriter, r *http.Request) {
	ip := r.URL.Query().Get("ip")
	if _, err := utils.ParseIPv4(ip); err != nil {
		writeError(w, http.StatusBadRequest, err.Error())
		return
	}

	ctx, cancel := context.WithTimeout(r.Context(), h.timeout)
	defer cancel()

	result := h.service.Ping(ctx, ip)
	writeJSON(w, http.StatusOK, result)
}
