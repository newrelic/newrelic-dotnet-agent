# Building  Custom Base Images for the Container Integration Tests
Instructions for building a set of custom base images, used by the Container Integration Tests solution. The images have the ASP.NET Core runtime pre-installed, which considerably reduces the time required to execute the container tests. 

*Note that this process only builds Amazon Linux and Fedora base images; the base images for the other Linux distros we test are available with ASP.NET Core runtime already installed.*

### References: 
https://www.docker.com/blog/multi-arch-build-and-images-the-simple-way/
https://docs.docker.com/engine/reference/commandline/buildx_build/


## Azure container registry login via Docker
From a Powershell command prompt in the same folder as this README file:
1. Log in to the DotNetReg container repository
`docker login -u dotnet-agent-token -p {insert password here} dotnetreg.azurecr.io`
2. Configure buildx in Docker Desktop
`docker buildx create --use`
3. Build the base images. The images will be pushed to the DotNetReg container registry with tags per .NET version

```
docker buildx  build --build-arg DOTNET_VERSION="6.0" -f Dockerfile.AmazonBaseImage --tag dotnetreg.azurecr.io/amazonlinux-aspnet:6.0 --platform linux/amd64,linux/arm64/v8 --push .
docker buildx  build --build-arg DOTNET_VERSION="8.0" -f Dockerfile.AmazonBaseImage --tag dotnetreg.azurecr.io/amazonlinux-aspnet:8.0 --platform linux/amd64,linux/arm64/v8 --push .

docker buildx  build --build-arg DOTNET_VERSION="6.0" -f Dockerfile.FedoraBaseImage --tag fedora-aspnet:6.0 --platform linux/amd64,linux/arm64/v8 -push .
docker buildx  build --build-arg DOTNET_VERSION="8.0" -f Dockerfile.FedoraBaseImage --tag fedora-aspnet:8.0 --platform linux/amd64,linux/arm64/v8 -push .
```

4. Disable buildx in Docker Desktop
`docker buildx rm`

5. Log out of the DotNetReg container repository
`docker logout dotnetreg.azurecr.io`

### Azure Container Registry Changes:
The DotNetReg container registry was originally created on the Basic SKU. The only way to enable anonymous pull from the container registry is to upgrade to the Standard SKU. For reference, this is the process that was followed:

#### Prerequisites:
* [Azure CLI (32 bit)](https://aka.ms/installazurecliwindows)

#### Azure login
1. Open a Powershell command prompt
2. Run `az login <your_azure_login_email` and follow the authentication instructions
3. Run the following:
```
  az acr update --name dotnetreg --sku Standard  # sku was Basic
  az acr update --name dotnetreg --anonymous-pull-enabled
```