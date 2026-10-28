# TECH-03 - Mapbox Live Test Report

Date: 2026-10-12
Branch: features/Implementation_MapboxAPIValidation
Provider: Mapbox

## Automated Test Results

- Unit tests: 15/15 PASS
- Mock tests: 4/4 PASS
- Live tests: 3/3 PASS

## Live API Results

### Geocoding v6
- Status: PASS (technical)
- Features returned: 5
- Feature type: street
- Observation: Several returned coordinates are far from the
  intended Ho Chi Minh City address.
- Address accuracy: Requires manual verification.

### Directions v5
- Status: PASS
- Reachable: true
- Duration: 387.259 seconds
- Distance: 1395.634 meters

### Matrix v1
- Status: PASS
- Origin: 1
- Destinations: 3
- Durations: 373.3, 238.6, 620.5 seconds
- Distances: 1390.0, 1155.8, 2721.8 meters

### Search Box
- Mock: PASS
- Live: Not tested
- Reason: Vietnam is outside advertised coverage.

## Remaining Acceptance Items

- Manually evaluate Vietnam geocoding accuracy.
- Confirm API limits and pricing.
- Obtain reviewer approval.

No access token is included in this report.
