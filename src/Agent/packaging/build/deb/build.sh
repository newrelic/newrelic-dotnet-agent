#!/bin/bash

if [ -z "$AGENT_VERSION" ]; then
    echo "AGENT_VERSION is not set"
    exit -1
fi

PACKAGE_NAME='newrelic-netcore20-agent'
INSTALL_ROOT=/tmp/${PACKAGE_NAME}
ARCH='amd64'
PACKAGE_FILE_BASENAME="${PACKAGE_NAME}_${AGENT_VERSION}_$ARCH"

mkdir ${INSTALL_ROOT} && mkdir ${INSTALL_ROOT}/DEBIAN

INSTALL_LOCATION=${INSTALL_ROOT}/usr/local/${PACKAGE_NAME}

mkdir -p ${INSTALL_LOCATION}

cp -R "New Relic Home x64 CoreClr_Linux"/* ${INSTALL_LOCATION}

pushd ${INSTALL_LOCATION}

# the logs directory gets created by postinst
rm -rf logs Logs

cp /common/setenv.sh .
cp /common/run.sh .
cp /docs/netcore20-agent-readme.md ./README.md

mv Extensions extensions

dos2unix *.x* extensions/*.x* *.sh

cp /deb/control ${INSTALL_ROOT}/DEBIAN
cp /deb/postinst ${INSTALL_ROOT}/DEBIAN

dos2unix ${INSTALL_ROOT}/DEBIAN/*

printf "\nPackage: ${PACKAGE_NAME}\nVersion: ${AGENT_VERSION}\n" >> ${INSTALL_ROOT}/DEBIAN/control

# create debian package
dpkg-deb --build ${INSTALL_ROOT}
cp /tmp/${PACKAGE_NAME}.deb /release/${PACKAGE_FILE_BASENAME}.deb
# create a copy of the agent that only uses the package name to make it easy to link to builds
cp /tmp/${PACKAGE_NAME}.deb /release/${PACKAGE_NAME}.deb

# create tar ball
tar cvfz /release/${PACKAGE_FILE_BASENAME}.tar.gz -C ${INSTALL_LOCATION} ..