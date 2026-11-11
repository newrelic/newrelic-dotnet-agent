# Building  Custom Base Images for the Container Integration Tests
Instructions for building a set of custom base images, used by the Container Integration Tests solution. The images have the ASP.NET Core runtime pre-installed, which considerably reduces the time required to execute the container tests. 

*Note that this process only builds Amazon Linux and Fedora base images; the base images for the other Linux distros we test are available with ASP.NET Core runtime already installed.*

### References: 
https://www.docker.com/blog/multi-arch-build-and-images-the-simple-way/
https://docs.docker.com/engine/reference/commandline/buildx_build/

* Relevant secrets are available in 1Password
* Requires [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest) and [Docker CLI](https://docs.docker.com/reference/cli/docker/)

## Azure container registry login via Docker
From a Powershell command prompt in the same folder as this README file:

0. Set the registry name and its login server in variables. The tests read the login server from the `ContainerTestAcrName` value in the integration test secrets (the `TEST_SECRETS` secret in CI). It reaches the Dockerfiles as the `CONTAINER_TEST_ACR_NAME` build argument.
```
$acrName="{container registry name}"
$acrLoginServer="{container registry login server}"
```
1. Log in to the container registry
```
az login
az acr login --name $acrName
```
2. Create a buildx builder in Docker Desktop
```
docker buildx create --name nr-base-img --use
```
3. Build the base images. Each command pushes one multi-architecture image to the registry, tagged with its .NET version.

```
docker buildx  build --build-arg BASE_IMAGE="amazonlinux:latest" --build-arg DOTNET_VERSION="10.0" --build-arg DOTNET_QUALITY="GA" -f Dockerfile.CustomBaseImage --tag $acrLoginServer/amazonlinux-aspnet:10.0 --platform linux/amd64,linux/arm64/v8 --push .
docker buildx  build --build-arg BASE_IMAGE="amazonlinux:latest" --build-arg DOTNET_VERSION="11.0" --build-arg DOTNET_QUALITY="GA" -f Dockerfile.CustomBaseImage --tag $acrLoginServer/amazonlinux-aspnet:11.0 --platform linux/amd64,linux/arm64/v8 --push .

docker buildx  build --build-arg BASE_IMAGE="fedora:latest" --build-arg DOTNET_VERSION="10.0" --build-arg DOTNET_QUALITY="GA" -f Dockerfile.CustomBaseImage --tag $acrLoginServer/fedora-aspnet:10.0 --platform linux/amd64,linux/arm64/v8 --push .
docker buildx  build --build-arg BASE_IMAGE="fedora:latest" --build-arg DOTNET_VERSION="11.0" --build-arg DOTNET_QUALITY="GA" -f Dockerfile.CustomBaseImage --tag $acrLoginServer/fedora-aspnet:11.0 --platform linux/amd64,linux/arm64/v8 --push .
```

`DOTNET_QUALITY` is `GA`, `preview`, or `daily`. Use `preview` for a version that is not released yet: `GA` does not find a preview or RC build. Tag the image with the plain version (for example `11.0`, not `11.0-preview`), because the tests use the same value for the image tag and the TFM.

The images do not update themselves. Rebuild them when a new .NET version is added, when a version goes from preview to GA, and when you want the latest runtime patch.

4. Remove the buildx builder
`docker buildx rm nr-base-img`

5. Log out of Azure
`az logout`

# Enabling Anonymous Pull Access

By default, anonymous pull access is disabled but is required for the container integration tests workflow. See [Configure anonymous pull access](https://learn.microsoft.com/en-us/azure/container-registry/anonymous-pull-access) for details. 
