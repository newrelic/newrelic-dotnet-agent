This folder contains the YUM repo definition files for the .NET agent.  They are optionally deployed to either the production download.newrelic.com or our test mirror by running the agent deploy workflow and enabling the "linux-deploy-yum-repo-definitions" option.

`newrelic-dotnet-agent.repo` lists only one signing keys under `gpgkey`, the newer one used to sign packages >=10.48.0.  This repo definition can be used on all supported RHEL-family repos, but will not support installing agents older than 10.48.0.

`newrelic-dotnet-agent-legacy.repo` lists two signing keys under `gpgkey`: the one used to sign packages <=10.47.0, and the newer one used to sign packages >=10.48.0.  This repo definition can't be used on RHEL >10 or its derivitives due to their more restrictive crypto policy.