# .NET Core Agent Linux Package Deployment

The assets in this path are used to deploy the Linux packages (.deb and .rpm) for the .NET Core Agent to New Relic's public package sources (apt.newrelic.com and yum.newrelic.com).

## Requirements
1. Docker, with the ability to run Linux containers.
2. AWS S3 access keys with read/write access to the bucket(s) you are updating
3. A Linux-like command line environment, such as `git-bash` on Windows, or a real Linux system or VM (e.g. WSL2)
4. The proper GPG signing keys and their IDs.

To deploy the .rpm and .deb packages for a particular release version (e.g. 10.0.0): (note: all commands should be run from the same location as this README)

1. Add the packages to be released to the `packages` subfolder:
    
        cp <packages_to_be_released> ./packages

2. Add the GPG signing keys to the `deploy_scripts` subfolder:

        cp private-key-1.gpg ./deploy_scripts
        cp private-key-2.gpg ./deploy_scripts

3. Create the `docker.env` [environment variable file](https://docs.docker.com/compose/env-file/) with required values (the values shown here are just examples, you will need to supply the correct ones):

        echo "AGENT_VERSION=10.0.0" >docker.env
        echo "S3_BUCKET=s3://your-s3-bucket" >>docker.env
        echo "AWS_ACCESS_KEY_ID=AKAKAKAKAKAKAKA" >>docker.env
        echo "AWS_SECRET_ACCESS_KEY=6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ" >>docker.env
        echo "OLD_PRIVATE_KEY=/data/deploy_scripts/private-key-1.gpg" >> docker.env
        echo "NEW_PRIVATE_KEY=/data/deploy_scripts/private-key-2.gpg" >> docker.env
        echo "NEW_PRIVATE_KEY_PASSPHRASE=xxxxxxxxxxxxxx" >> docker.env
        echo "OLD_KEY_ID="0123456789ABCFEF" >> docker.env
        echo "NEW_KEY_ID="FEDCBA9876543210" >> docker.env

4. Optionally, add additional non-required environment variables to the environment file:
    - `AWS_DEFAULT_REGION` (defaults to `us-west-2`)
    - `AWS_DEFAULT_OUTPUT` (defaults to `text`)
    - `ACTION` (value can be `release` (add new packages to the repos) or `rollback` (remove existing packages from the repos), defaults to `release`)

5. Build the Docker container:

        docker compose build

6. Run the deploy (or rollback) process:

        docker compose run deploy_packages

Note that the scripts in ./deploy_scripts came from the PHP agent team and have a lot of logic in them to support their particular build/test/release processes, 
not all of which we are using.  However, since we are sharing the same public package sources with the PHP agent, anything this script does needs to be cautious 
to avoid breaking their repos.  In particular, before we attempt to deploy a version of our agent to the main public repos, we should make sure the PHP agent team isn't also trying to deploy at the same time.

Other notes:

* It is safe to deploy packages that are already in the repository.  This has come up historically in the context of "something went wrong with the deploy process and the repo is in a bad state (but the version we just deployed does exist in the repo now), is it safe to just re-run the deploy with the same version of the agent?"  It is.
* You can rollback a version without having the actual .rpm and .deb files for the version being removed in the `packages` subfolder.
