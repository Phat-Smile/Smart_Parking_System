# Parking

## Responsibility

Parking lots, floors, zones, spots and layouts; capacity and spot assignment; parking sessions and per-session QR; entry/exit; pricing; occupancy and realtime state.

## Projects

- Parking.Domain
- Parking.Application
- Parking.Persistence
- Parking.Infrastructure
- Parking.WebAPI

## Implemented business slices

### US-SEARCH-001 — Parking Catalog

- Public `GET /api/parking/lots` endpoint.
- Returns only administrator-approved parking lots.
- Parking Service owns `parking.parking_lots`.
- Catalog response currently contains lot identity, name, address, coordinates and parking-lot type.
- Search radius, vehicle-group filtering, availability, Parking Detail, Nominatim, OSRM and AI recommendation remain separate feature branches.

