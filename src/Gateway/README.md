# API Gateway

YARP reverse-proxy host with catch-all routes and clusters under `/api/identity/`, `/api/parking/`, `/api/booking/`, `/api/payment/`, and `/api/notification/`, plus process health endpoints. The Gateway validates future protected requests using the shared RS256 JWT validation foundation; downstream APIs validate tokens again. CORS and security headers are configured. No business endpoint implementation, token issuance, Login, or Register endpoint exists yet.
