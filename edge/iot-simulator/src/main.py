import os
import signal
from threading import Event


def main() -> None:
    mqtt_host = os.getenv("MQTT_HOST", "rabbitmq")
    mqtt_port = int(os.getenv("MQTT_PORT", "1883"))
    mqtt_username = os.getenv("MQTT_USERNAME", "")
    mqtt_password = os.getenv("MQTT_PASSWORD", "")
    simulation_enabled = os.getenv("DEVICE_SIMULATION_ENABLED", "false").lower() == "true"
    stop_requested = Event()

    signal.signal(signal.SIGTERM, lambda *_: stop_requested.set())
    signal.signal(signal.SIGINT, lambda *_: stop_requested.set())

    if simulation_enabled:
        print(
            "IoT simulator scaffold started; traffic generation is not implemented "
            f"(MQTT credentials configured: {bool(mqtt_username and mqtt_password)}).",
            flush=True,
        )
    else:
        print(
            f"IoT simulator idle; MQTT target is {mqtt_host}:{mqtt_port}; "
            f"credentials configured: {bool(mqtt_username and mqtt_password)}; no traffic is sent.",
            flush=True,
        )

    stop_requested.wait()


if __name__ == "__main__":
    main()
