# MockNewRelic

This application serves as a mock New Relic or "collector" set of endpoints for tests requiring that round trip for proper verification. 

It is a very naive implementation and not meant to serve as a proper "mock collector" in its current form.

## SSL

To support protocol 15+ this has been updated to use SSL. A self-signed cert has been generated and leveraged.

Applications using this mock New Relic application will need to override their certificate validation to allow for the untrusted cert. This is so we don't have to install a custom root as trusted on all of the dev and build machines. This is OK because **we are not testing SSL** but just leveraging the mock endpoints for validating things that are hard to test without a round trip that we want to cover via integration tests.

Applications may also need to add additional TLS settings. 

Examples 

```cs
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
ServicePointManager.ServerCertificateValidationCallback = delegate
{
    //force trust on all certificates for simplicity
    return true;
};
```

The cert was generated in the following fashion...

```powershell
$cert = New-SelfSignedCertificate -certstorelocation cert:\localmachine\my -dnsname ".NET Agent Test Certificate Authority"
Write-Host $cert

$pwd = ConvertTo-SecureString -String "password1" -Force -AsPlainText

$path = "cert:\localMachine\my\" + $cert.Thumbprint 
Export-PfxCertificate -cert $path -FilePath testcert.pfx -Password $pwd
```

The cert also needs to be set to copy to the output directory. I set to "copy always" to be safe.