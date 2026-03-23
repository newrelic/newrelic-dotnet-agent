# Copyright 2020 New Relic, Inc. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
#
# Locust traffic driver for the .NET agent performance test app.
# Drives traffic to all endpoints with a realistic mix of request types.
#
# Environment variables:
#   TARGET_HOST  - base URL of the test app (default: http://testapp:8080)
#
# Run headless (non-interactive) with:
#   locust --headless -u <users> -r <spawn_rate> --run-time <duration>
#          --host http://testapp:8080 --csv results --exit-code-on-error 0

import os
from locust import HttpUser, TaskSet, task, between, events
import logging

logger = logging.getLogger(__name__)


class PerformanceTasks(TaskSet):
    """Mixed workload that exercises the key instrumentation paths."""

    @task(5)
    def simple(self):
        """High-frequency simple requests - measures baseline transaction overhead."""
        with self.client.get("/home/simple", catch_response=True) as response:
            if response.status_code != 200:
                response.failure(f"Expected 200, got {response.status_code}")

    @task(2)
    def nested_async(self):
        """Nested async calls - exercises multi-segment transactions."""
        with self.client.get("/home/nested", catch_response=True) as response:
            if response.status_code != 200:
                response.failure(f"Expected 200, got {response.status_code}")

    @task(3)
    def rabbitmq_publish(self):
        """RabbitMQ publish - exercises message broker instrumentation (producer side)."""
        with self.client.post("/rabbitmq/publish", catch_response=True) as response:
            if response.status_code != 200:
                response.failure(f"Expected 200, got {response.status_code}")

    @task(2)
    def rabbitmq_consume(self):
        """RabbitMQ consume - exercises message broker instrumentation (consumer side)."""
        with self.client.get("/rabbitmq/consume", catch_response=True) as response:
            if response.status_code != 200:
                response.failure(f"Expected 200, got {response.status_code}")

@task(2)
    def mongo_crud(self):
        """MongoDB CRUD - exercises MongoDb26 datastore instrumentation wrapper."""
        with self.client.get("/mongo/crud", catch_response=True) as response:
            if response.status_code != 200:
                response.failure(f"Expected 200, got {response.status_code}")

    @task(1)
    def health(self):
        """Health check - validates the app is alive throughout the test."""
        with self.client.get("/health", catch_response=True) as response:
            if response.status_code != 200:
                response.failure(f"Health check failed: {response.status_code}")


class PerformanceTestUser(HttpUser):
    tasks = [PerformanceTasks]
    wait_time = between(0.1, 0.5)

    def on_start(self):
        # Warm-up: hit health endpoint before joining the load
        self.client.get("/health")


@events.quitting.add_listener
def on_quitting(environment, **kwargs):
    """Log a summary and set a non-zero exit code if the error rate is too high."""
    stats = environment.stats.total
    error_rate = stats.fail_ratio

    logger.info(
        "Test complete: %d requests, %d failures, %.1f%% error rate, "
        "median %.0f ms, p95 %.0f ms",
        stats.num_requests,
        stats.num_failures,
        error_rate * 100,
        stats.get_response_time_percentile(0.5) or 0,
        stats.get_response_time_percentile(0.95) or 0,
    )

    # Fail the run if error rate exceeds 1%
    if error_rate > 0.01:
        logger.error(
            "Error rate %.1f%% exceeds 1%% threshold - marking run as failed",
            error_rate * 100,
        )
        environment.process_exit_code = 1
