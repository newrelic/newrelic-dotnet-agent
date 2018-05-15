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

if ($env:S3_BUCKET) {
    switch($env:S3_BUCKET) {
        "s3://test-repo-production" {
            $APT_REPO_URL='http://test-repo-production.s3.amazonaws.com/debian'
            $YUM_REPO_URL='http://test-repo-production.s3.amazonaws.com/pub/newrelic/el7'                        
        }
        "s3://test-repo-testing/75abcxxx/" {
            $APT_REPO_URL='http://test-repo-testing.s3.amazonaws.com/75abcxxx/debian'
            $YUM_REPO_URL='http://test-repo-testing.s3.amazonaws.com/75abcxxx/pub/newrelic-testing/el7'                                    
        }
        "s3://nr-downloads-main" {
            $APT_REPO_URL='http://apt.newrelic.com/debian'
            $YUM_REPO_URL='http://yum.newrelic.com/pub/newrelic/el7'                        
        }
        "s3://nr-downloads-private/75ac22b116/" {
            $APT_REPO_URL='https://nr-downloads-private.s3-us-east-1.amazonaws.com/75ac22b116/debian'
            $YUM_REPO_URL='https://nr-downloads-private.s3-us-east-1.amazonaws.com/75ac22b116/pub/newrelic-testing/el7'                                    
        }
        default { Write-Host "Unrecognized value $env:S3_BUCKET set for S3_BUCKET"}
    }
} else {
    Write-Host "S3_BUCKET must be set, exiting"
    exit 1
}

Write-Host "S3_BUCKET=$env:S3_BUCKET"
Write-Host "APT_REPO_URL=$APT_REPO_URL"
Write-Host "YUM_REPO_URL=$YUM_REPO_URL"

docker-compose build
docker-compose run test_debian bash -c "/test/repo_tests/test_apt_repo_install.sh $APT_REPO_URL $AGENT_VERSION"
docker-compose run test_centos bash -c "/test/repo_tests/test_yum_repo_install.sh $YUM_REPO_URL $AGENT_VERSION"
