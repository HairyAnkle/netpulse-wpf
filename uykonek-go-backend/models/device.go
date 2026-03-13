package models

import "time"

type Device struct {
	Ip        string    `json:"ip"`
	Mac       string    `json:"mac"`
	Hostname  string    `json:"hostname"`
	Vendor    string    `json:"vendor"`
	IsNew     bool      `json:"is_new"`
	FirstSeen time.Time `json:"first_seen"`
	LastSeen  time.Time `json:"last_seen"`
}
