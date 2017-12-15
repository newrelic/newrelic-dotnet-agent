$ErrorActionPreference = "Stop"

$AGENT_VERSION=''
$APT_REPO_URL='http://test-repo-production.s3.amazonaws.com/debian'
$YUM_REPO_URL='http://test-repo-production.s3.amazonaws.com/pub/newrelic/el7'

if ($env:Version) {
    $AGENT_VERSION = $env:Version.Replace("-beta", "")
    Write-Host "AGENT_VERSION=$AGENT_VERSION"
} else {
    Write-Host "env:Version must be set, exiting"
    exit 1
}

if ($env:RELEASE_ACTION) {
    switch($env:RELEASE_ACTION) {
        "release-2-fake-production" {
            $APT_REPO_URL='http://test-repo-production.s3.amazonaws.com/debian'
            $YUM_REPO_URL='http://test-repo-production.s3.amazonaws.com/pub/newrelic/el7'                        
        }
        "release-2-fake-testing" {
            $APT_REPO_URL='http://test-repo-testing.s3.amazonaws.com/75abcxx/debian'
            $YUM_REPO_URL='http://test-repo-testing.s3.amazonaws.com/75abcxxx/pub/newrelic-testing/el7'                                    
        }
        "release-2-production" {
            $APT_REPO_URL='http://apt.newrelic.com/debian'
            $YUM_REPO_URL='http://yum.newrelic.com/pub/newrelic/el7'                        
        }
        "release-2-testing" {
            $APT_REPO_URL='https://nr-downloads-private.s3-us-east-1.amazonaws.com/75ac22b116/debian'
            $YUM_REPO_URL='https://nr-downloads-private.s3-us-east-1.amazonaws.com/75ac22b116/pub/newrelic-testing/el7'                                    
        }
        default { Write-Host "Unrecognized value $env:RELEASE_ACTION set for RELEASE_ACTION"}
    }
} else {
    Write-Host "env:RELEASE_ACTION must be set, exiting"
    exit 1
}

Write-Host "RELEASE_ACTION=$env:RELEASE_ACTION"
Write-Host "APT_REPO_URL=$APT_REPO_URL"
Write-Host "YUM_REPO_URL=$YUM_REPO_URL"

docker-compose build
docker-compose run test_debian bash -c "/test/repo_tests/test_apt_repo_install.sh $APT_REPO_URL $AGENT_VERSION"
docker-compose run test_centos bash -c "/test/repo_tests/test_yum_repo_install.sh $YUM_REPO_URL $AGENT_VERSION"
