"C:\Program Files\dotnet\dotnet.exe" --version
"C:\Program Files\dotnet\dotnet.exe" restore NewRelic.AspNetCore.Middleware.sln
"C:\Program Files\dotnet\dotnet.exe" build --version-suffix %BUILD_NUMBER% -c Release NewRelic.AspNetCore.Middleware.sln
"C:\Program Files\dotnet\dotnet.exe" pack  src/NewRelic.AspNetCore.Middleware/NewRelic.AspNetCore.Middleware.csproj --no-build --output nupkgs --configuration Release --version-suffix %BUILD_NUMBER%