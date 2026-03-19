package main

import (
	"log"
	"net/http"
	"time"
	"uykonek-backend/api"
	"uykonek-backend/config"
	"uykonek-backend/repository"
	"uykonek-backend/services"
	"uykonek-backend/utils"
)

func main() {
	cfg := config.Load()

	pingTimeout, err := time.ParseDuration(cfg.PingTimeout)
	if err != nil {
		log.Fatalf("invalid ping timeout: %v", err)
	}
	portTimeout, err := time.ParseDuration(cfg.PortTimeout)
	if err != nil {
		log.Fatalf("invalid port timeout: %v", err)
	}
	scanTimeout, err := time.ParseDuration(cfg.ScanTimeout)
	if err != nil {
		log.Fatalf("invalid scan timeout: %v", err)
	}

	vendorLookup := utils.NewVendorLookup(cfg.OUIDataPath)
	repo := repository.NewDeviceRepository()
	pingSvc := services.NewPingService(pingTimeout)
	arpScanner := services.NewARPScanner(cfg.WorkerCount, pingSvc, vendorLookup)
	deviceSvc := services.NewDeviceService(repo, arpScanner)
	portScanner := services.NewPortScanner(portTimeout)

	scanHandler := api.NewScanHandler(deviceSvc, scanTimeout)
	pingHandler := api.NewPingHandler(pingSvc, pingTimeout)
	portHandler := api.NewPortHandler(portScanner, scanTimeout)

	mux := http.NewServeMux()
	api.RegisterRoutes(mux, scanHandler, pingHandler, portHandler)

	server := &http.Server{
		Addr:         cfg.Address,
		Handler:      loggingMiddleware(mux),
		ReadTimeout:  10 * time.Second,
		WriteTimeout: 30 * time.Second,
		IdleTimeout:  60 * time.Second,
	}

	log.Printf("UyKonek backend listening on http://localhost%s", cfg.Address)
	if err := server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatalf("server error: %v", err)
	}
}

func loggingMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		next.ServeHTTP(w, r)
		log.Printf("%s %s (%s)", r.Method, r.URL.Path, time.Since(start))
	})
}
