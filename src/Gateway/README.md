# API Gateway

YARP reverse-proxy host with empty production routes/clusters and process health endpoints. The Gateway validates future protected requests using the shared RS256 JWT validation foundation; downstream APIs validate tokens again. CORS and security headers are configured. No production business route, token issuance, Login, or Register endpoint exists yet.
