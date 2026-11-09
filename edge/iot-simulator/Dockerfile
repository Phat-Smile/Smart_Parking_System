FROM python:3.12-slim-bookworm

ENV PYTHONUNBUFFERED=1
WORKDIR /app
RUN python -m pip install --no-cache-dir paho-mqtt==2.1.0
COPY edge/iot-simulator/src ./src

CMD ["python", "-u", "src/main.py"]

