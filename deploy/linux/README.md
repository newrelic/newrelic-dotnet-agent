# .NET Core Agent Linux Package Deployment

The assets in this path are used to deploy the Linux packages (.deb and .rpm) for the .NET Core Agent to New Relic's public package sources (apt.newrelic.com and yum.newrelic.com).

## Requirements
1. Docker, with the ability to run Linux containers
2. AWS S3 access keys with read/write access to the bucket(s) you are updating

To deploy the .rpm and .deb packages for a particular release version (e.g. 6.18.123.0), run the following commands from this directory (same directory as this README):

1. `cp <packages_to_be_released> ./packages`
2. `docker-compose build`
3. The following environment variables MUST be set in the environment that `deploy.bash` executes in inside the container:
    - `AGENT_VERSION` (e.g. 6.18.123.0)
    - `AWS_ACCESS_KEY_ID`
    - `AWS_SECRET_ACCESS_KEY`
4. These environment variables can be passed to the container in a few ways:
    - In a file, specified in the `docker-compose.yml` file (currently `docker.env`), one name/val pair per line with no quoting
    - You can set them explicity in the `docker-compose run` command, like so:
    `docker-compose run -e AGENT_VERSION=6.18.123.0 -e AWS_ACCESS_KEY_ID=AKAKAKAKAKAKAKA -e AWS_SECRET_ACCESS_KEY=6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ6SQ deploy_packages`
5. The following environment variables MAY be passed to the container, but are not required:
    - `AWS_DEFAULT_REGION` (defaults to `us-west-2`)
    - `AWS_DEFAULT_OUTPUT` (defaults to `text`)
    - `RELEASE_ACTION` (defaults to `release-2-fake-production`; set to `release-2-production` to REALLY release new agent packages for Linux)
6. `docker-compose run deploy_packages`

Note that the scripts in ./deploy_scripts came from the PHP agent team and have a lot of logic in them to support their particular build/test/release processes, not all of which
we are using.  However, since we are sharing the same public package sources with the PHP agent, anything this script does needs to be cautious to avoid breaking their repos.
In particular, before we attempt to deploy a version of our agent to the main public repos, we should make sure the PHP agent team isn't also trying to deploy at the same time.
