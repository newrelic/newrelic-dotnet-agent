# Configuration Development

The following configuration files are generated using Xsd2Code:

* `src\Agent\NewRelic\Agent\Core\Config\Configuration.cs`
* `src\Agent\NewRelic\Agent\Core\Extension\Extension.cs`

Please note the version of Xsd2Code included in the code base **requires .NET 3.5** to execute successfully.

You can find Xsd2Code in the repository here: `.\build\Tools\xsd2code`.

Updating the configuration consists of two steps:

1. Update the relevant XSD file.
2. Update the class file via `xsd2code`.
3. Add/restore the copyright to the class file.

## Update the XSD

Update the relevant XSD with new or modified configuration based on your needs.

* `src\Agent\NewRelic\Agent\Core\Config\Configuration.xsd`
* `src\Agent\NewRelic\Agent\Core\Extension\Extension.xsd`

## Update the Class

Once the relevant XSD file has been updated, run `xsd2Code` to update the class file.

The following commands should be run from from the root of the repository using PowerShell.

### Configuration.cs

```powershell
$rootDirectory = Resolve-Path ".\"; .\build\Tools\xsd2code\xsd2code.exe "$rootDirectory\src\Agent\NewRelic\Agent\Core\Config\Configuration.xsd" NewRelic.Agent.Core.Config Configuration.cs /cl /ap /sc /xa
```

### Extension.cs

```powershell
$rootDirectory = Resolve-Path ".\"; .\build\Tools\xsd2code\xsd2code.exe "$rootDirectory\src\Agent\NewRelic\Agent\Core\Extension\Extension.xsd" NewRelic.Agent.Core.Extension Extension.cs /cl /ap /sc /xa
```

## Add the Copyright

The `xsd2code` tool will not automatically generate (nor keep) the required copyright header at the top of the file.

Add/restore the copyright header to the top of the updated class file.

The current header is:

```cs
// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
```
