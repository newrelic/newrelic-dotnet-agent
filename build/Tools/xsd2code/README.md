# Using Xsd2Code to generate Configuration.cs or Extension.cs

When either of the following two XSD files are changed, `xsd2code` will need to be run manually to regenerate the class files.

- src\Agent\NewRelic\Agent\Core\Config\Configuration.xsd
- src\Agent\NewRelic\Agent\Core\Extension\Extension.xsd

## Powershell Commands

The following commands should be run from from the root of the repository.

### Configuration.cs

`$rootDirectory = Resolve-Path ".\"; .\build\Tools\xsd2code\xsd2code.exe "$rootDirectory\src\Agent\NewRelic\Agent\Core\Config\Configuration.xsd" NewRelic.Agent.Core.Config Configuration.cs /cl /ap /sc /xa`

### Extension.cs

`$rootDirectory = Resolve-Path ".\"; .\build\Tools\xsd2code\xsd2code.exe "$rootDirectory\src\Agent\NewRelic\Agent\Core\Extension\Extension.xsd" NewRelic.Agent.Core.Extension Extension.cs /cl /ap /sc /xa`
