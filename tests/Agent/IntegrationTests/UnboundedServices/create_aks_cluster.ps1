az login

$resourceGroup = "integration-test-services"
$acrName="dotnetunboundedservicesregistry" # try using dotnetunboundedservices after mid-July 2025 when the name is available
$managedIdentity="unbounded-services-identity"
$aksName = "dotnet-unbounded-services"
$publicIpName = "dotnet-unbounded-services-k8s"
$publicIpDnsName = "dotnet-unbounded-services-k8s"

# Get the current Azure subscription ID and store it in a variable
$subscriptionId = az account show --query id -o tsv
 
# create a managed identity for working with unbounded services and the acr
az identity create --name $managedIdentity --resource-group $resourceGroup

$managedIdentityClientId = az identity show --name $managedIdentity --resource-group $resourceGroup --query clientId -o tsv

# Create the Azure Container Registry
az acr create --resource-group $resourceGroup --name $acrName --sku Standard --admin-enabled true

# assign the acrpush role to the rbac principal created above
$registryId = az acr show --name $acrName --resource-group $resourceGroup --query id --output tsv
az role assignment create --assignee $managedIdentityClientId --scope $registryId --role AcrPush

$acrLoginServer = az acr show --name $acrName --resource-group $resourceGroup --query loginServer --output tsv
Write-Output "ACR Login Server: $acrLoginServer"

# build and push docker images to the acr for all services
build_and_push_acr.ps1 $acrName $resourceGroup

# Create the AKS cluster
az aks create --resource-group $resourceGroup --name $aksName --node-count 1 --enable-managed-identity --assign-identity $managedIdentityClientId --generate-ssh-keys --node-vm-size Standard_DS4_v2

# give the cluster permission to access the ACR
az aks update -n $aksName -g $resourceGroup --attach-acr $acrName

# create a public IP and write it to the console
$publicIpAddress = az network public-ip create --resource-group $resourceGroup --name $publicIpName --sku Standard
Write-Output "Public IP address: $publicIpAddress"

# assign a dns label to the public IP
$publicIpDnsHost = az network public-ip update --resource-group $resourceGroup --name $publicIpName --dns-name $publicIpDnsName
Write-Output "Public IP DNS Host: $publicIpDnsHost"

# authorize the managed identity to use the public IP, which enables the cluster to use the public IP in load balancers
az role assignment create --assignee $managedIdentityClientId --role "Network Contributor" --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup

Write-Output "AKS cluster $aksName created successfully with managed identity $managedIdentity and public IP $publicIpDnsName ($publicIpAddress)."