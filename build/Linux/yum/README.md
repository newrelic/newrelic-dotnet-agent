This folder contains the YUM repo definition file for the .NET agent and was manually uploaded to https://download.newrelic.com/dot_net_agent/yum. 

If changes to this file are needed, use the AWS CLI to upload to the S3 bucket:
```
$env:AWS_ACCESS_KEY_ID="access_key_for_s3_bucket"
$env:AWS_SECRET_ACCESS_KEY="secret_access_key_for_s3_bucket"
 aws s3 cp ./newrelic-dotnet-agent.repo s3://<bucket_name>/dot_net_agent/yum
 ```