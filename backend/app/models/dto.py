from datetime import datetime
from pydantic import BaseModel, Field


class DeviceDto(BaseModel):
    ip: str
    mac: str
    hostname: str | None = None
    vendor: str | None = None
    first_seen: datetime
    last_seen: datetime
    is_new: bool


class ScanMetadataDto(BaseModel):
    scan_id: int
    subnet: str
    ts_start: datetime
    ts_end: datetime
    host_count: int


class ScanDevicesResponseDto(BaseModel):
    scan: ScanMetadataDto
    devices: list[DeviceDto] = Field(default_factory=list)
