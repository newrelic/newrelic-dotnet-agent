#!/bin/bash

EXIT_STATUS=0

for container in test_debian test_centos; do

    docker-compose run $container || EXIT_STATUS=$?

    docker-compose run $container bash -c "cd /apps/http_client_test && /test/http_client_test.sh" || EXIT_STATUS=$?

    docker-compose run $container bash -c "cd /apps/http_client_test && /test/http_client_test_tarball.sh" || EXIT_STATUS=$?

    docker-compose run $container bash -c "cd /apps/custom_attributes && /test/custom_attributes_test.sh" || EXIT_STATUS=$?

    docker-compose run $container bash -c "cd /apps/custom_attributes && /test/custom_attributes_test_tarball.sh" || EXIT_STATUS=$?

    docker-compose run $container bash -c "cd /apps/custom_xml && /test/custom_xml_test.sh" || EXIT_STATUS=$?

    # The mvc app is built into the container so that we don't have to check in all that generated stuff
    docker-compose run $container bash -c "cd /container_apps/mvc && /test/mvc_test.sh" || EXIT_STATUS=$?

done

exit $EXIT_STATUS