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

2. Add the GPG signing keys to the `keys` subfolder, with these exact names. The folder is mounted read-only into the container at run time, so the keys are never copied into the Docker image. (`*.gpg` files are git-ignored.) To keep the keys somewhere else, set `GPG_KEYS_DIR` to that folder instead.

        mkdir -p ./keys
        cp private-key-1.gpg ./keys/old-private-key.gpg
        cp private-key-2.gpg ./keys/new-private-key.gpg

3. Export the required environment variables in the shell you will run `docker compose` from. `docker-compose.yml` passes them through to the container by name, so nothing is written to disk. The values shown here are just examples; supply the correct ones.

        export AGENT_VERSION=10.0.0
        export S3_BUCKET=s3://your-s3-bucket
        export AWS_ACCESS_KEY_ID=AKAKAKAKAKAKAKA
        export AWS_SECRET_ACCESS_KEY=6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ
        export NEW_PRIVATE_KEY_PASSPHRASE=xxxxxxxxxxxxxx
        export OLD_KEY_ID=0123456789ABCFEF
        export NEW_KEY_ID=FEDCBA9876543210

    To keep secrets out of your shell history, read them with `read -rs`, e.g. `read -rs AWS_SECRET_ACCESS_KEY && export AWS_SECRET_ACCESS_KEY`. If you use temporary AWS credentials, also export `AWS_SESSION_TOKEN`.

4. Optionally, export these non-required environment variables:
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
