FROM python:3.12-slim-bookworm

ENV PYTHONUNBUFFERED=1
WORKDIR /app
COPY edge/iot-simulator/src ./src

CMD ["python", "-u", "src/main.py"]

