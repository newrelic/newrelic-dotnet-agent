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

0. Set the container registry name and login server in a variable
```
$acrUserName="{container registry username}"
$acrName="{container registry name}"
```
1. Log in to the container registry
```
az login
az acr login --name $acrUserName
```
2. Configure buildx in Docker Desktop
```
docker buildx create --use
```
3. Build the base images. The images will be pushed to the container registry with tags per .NET version

```
docker buildx  build --build-arg DOTNET_VERSION="8.0" --build-arg DOTNET_QUALITY="GA" -f Dockerfile.AmazonBaseImage --tag $acrName/amazonlinux-aspnet:8.0 --platform linux/amd64,linux/arm64/v8 --push .
docker buildx  build --build-arg DOTNET_VERSION="10.0" --build-arg DOTNET_QUALITY="GA" -f Dockerfile.AmazonBaseImage --tag $acrName/amazonlinux-aspnet:10.0 --platform linux/amd64,linux/arm64/v8 --push .

docker buildx  build --build-arg DOTNET_VERSION="8.0" --build-arg DOTNET_QUALITY="GA" -f Dockerfile.FedoraBaseImage --tag $acrName/fedora-aspnet:8.0 --platform linux/amd64,linux/arm64/v8 --push .
docker buildx  build --build-arg DOTNET_VERSION="10.0" --build-arg DOTNET_QUALITY="GA" -f Dockerfile.FedoraBaseImage --tag $acrName/fedora-aspnet:10.0 --platform linux/amd64,linux/arm64/v8 --push .
```

4. Disable buildx in Docker Desktop
`docker buildx rm`

5. Log out of the {container registry name} container repository
`az logout`

# Enabling Anonymous Pull Access

By default, anonymous pull access is disabled but is required for the container integration tests workflow. See [Configure anonymous pull access](https://learn.microsoft.com/en-us/azure/container-registry/anonymous-pull-access) for details. 
