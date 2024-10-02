# Dockerized Unbounded Integration Test Services

The tests in the [unbounded integration tests solution](../UnboundedIntegrationTests.sln) depend on external resources to function properly, e.g. tests for MySql database instrumentation need to connect to a MySql database.  The assets in this path allow a developer to use Docker Desktop to run the necessary services for the tests.

## Requirements

* Windows 10 
* [Docker Desktop 2.2+](https://docs.docker.com/docker-for-windows/install/)

Note: while not a hard requirement, the containers will perform better if you use [Docker Desktop's WSL2 (Windows Subsystem for Linux 2) backend](https://docs.docker.com/docker-for-windows/wsl/), so having WSL2 enabled on your Windows 10 system is a good idea.

## Running the containers

**Notes**:

* This folder has Dockerfiles that set up Linux containerized services and can be used with Docker Desktop's WSL2 backend. Developers should use these containers in their system to run unbounded integration tests.

* All commands below should be run from a shell (we've tested Powershell and "git-bash") in the same location as this README.
* Before Docker containers can be used the first time, they need to be built by executing `docker compose build`.

### All

To run all services:

`docker compose up`

If you don't want to follow the output of the services, you can run them in the background (detached) like this:

`docker compose up -d`

To stop the services:

`docker compose down`

**Note**: launching all of the services takes a lot of time (as much as 15 minutes in our testing) and a lot of system resources.  Unless you need to run all of the unbounded integration tests (e.g. if you've made a change to a core part of the test framework as opposed to an individual test or piece of instrumentation) it is not recommended to do this.

### Individually

It is generally best to only the the service for the tests you happen to be working on at the time (see note above).  To run a single service, execute the following:

`docker compose up <service>`

See the docker-compose.yml file for the names of the services provided.

## Configuring user secrets

The integration tests, including the unbounded ones, use `dotnet user-secrets` to manage confuration data, including the connection strings needed to access these external services.  An [example-secrets.json](example-secrets.json) file has been provided which contains connection strings for all of the containerized unbounded services.  See the [integration test documentation](../../../../docs/integration-tests.md) for how to install the user secrets.
