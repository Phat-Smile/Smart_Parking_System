# Deployment

The root `docker-compose.yml` defines the local stack. `deploy/rabbitmq/` contains RabbitMQ plugin and MQTT configuration; `deploy/observability/` contains Collector, Jaeger, and Prometheus configuration. PostgreSQL is configured by Compose environment and initialized by explicit service-owned EF migrations. No Kubernetes or production deployment descriptor is present.
