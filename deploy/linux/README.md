# .NET Core Agent Linux Package Deployment

The assets in this path are used to deploy the Linux packages (.deb and .rpm) for the .NET Core Agent to New Relic's public package sources (apt.newrelic.com and yum.newrelic.com).

## Requirements
1. Docker, with the ability to run Linux containers
2. AWS S3 access keys with read/write access to the bucket(s) you are updating
3. A Linux-like command line environment, such as `git-bash` on Windows, or a real Linux system or VM (e.g. WSL2)

To deploy the .rpm and .deb packages for a particular release version (e.g. 6.18.123.0): (note: all commands should be run from the same location as this README)

1. Add the packages to be released to the `packages` subfolder:
    
        cp <packages_to_be_released> ./packages

2. Add the GPG signing keys to the `deploy_scripts` subfolder:

        cp gpg.tar.bz2 ./deploy_scripts

3. Create the `docker.env` [environment variable file](https://docs.docker.com/compose/env-file/) with required values (the values shown here are just examples, you will need to supply the correct ones):

        echo "AGENT_VERSION=6.18.123.0" >docker.env
        echo "S3_BUCKET=s3://your-s3-bucket" >>docker.env
        echo "AWS_ACCESS_KEY_ID=AKAKAKAKAKAKAKA" >>docker.env
        echo "AWS_SECRET_ACCESS_KEY=6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ" >>docker.env
        echo "GPG_KEYS=/deployscripts/gpg.tar.bz2" >>docker.env

4. Optionally, add additional non-required environment variables to the environment file:
    - `AWS_DEFAULT_REGION` (defaults to `us-west-2`)
    - `AWS_DEFAULT_OUTPUT` (defaults to `text`)
    - `ACTION` (value can be `deploy` (add new packages to the repos) or `rollback` (remove existing packages from the repos), defaults to `deploy`)

5. Make sure all script files have the correct permissions and line endings for Linux:

        find . -name "*.bash" |xargs chmod a+x
        find . -type f |xargs dos2unix

6. Build the Docker container:

        docker-compose build

7. Run the deploy (or rollback) process:

        docker-compose run deploy_packages

Note that the scripts in ./deploy_scripts came from the PHP agent team and have a lot of logic in them to support their particular build/test/release processes, 
not all of which we are using.  However, since we are sharing the same public package sources with the PHP agent, anything this script does needs to be cautious 
to avoid breaking their repos.  In particular, before we attempt to deploy a version of our agent to the main public repos, we should make sure the PHP agent team isn't also trying to deploy at the same time.
