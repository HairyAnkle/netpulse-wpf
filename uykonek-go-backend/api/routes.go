package api

import (
	"log"
	"net/http"
)

func RegisterRoutes(mux *http.ServeMux, scan *ScanHandler, ping *PingHandler, ports *PortHandler) {
	mux.HandleFunc("/scan/network", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			writeError(w, http.StatusMethodNotAllowed, "method not allowed")
			return
		}
		scan.HandleNetworkScan(w, r)
	})

	mux.HandleFunc("/scan/ping", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			writeError(w, http.StatusMethodNotAllowed, "method not allowed")
			return
		}
		ping.HandlePing(w, r)
	})

	mux.HandleFunc("/scan/ports", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			writeError(w, http.StatusMethodNotAllowed, "method not allowed")
			return
		}
		ports.HandlePortScan(w, r)
	})

	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
	})

	log.Println("Routes registered")
}
