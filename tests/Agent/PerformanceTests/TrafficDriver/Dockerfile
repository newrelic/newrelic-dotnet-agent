FROM python:3.12-slim

WORKDIR /locust

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY locustfile.py .

# Default entrypoint - all locust flags are passed via docker compose command
ENTRYPOINT ["locust"]
