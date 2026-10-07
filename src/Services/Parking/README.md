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

### US-SEARCH-005 — Parking Detail

- Public `GET /api/parking/lots/{parkingLotId}` endpoint.
- Returns a parking lot only when it is administrator-approved; missing, pending and rejected lots return public `404`.
- S1 detail projection contains the ParkingLot master data currently persisted by this service: identity, name, address, coordinates and parking-lot type.
- Remaining walk-in capacity, predicted availability, current price, distance, OSRM driving time and 3D layout are intentionally not fabricated here; they depend on later feature slices and team-owned contracts.