#!/bin/bash
set -e # Halt the build script on any error

# subroutine for signing .rpm
function sign_rpm {
    rpm_file="$1"
    # unpack the tarball, rename it and set perms
    dtrx --one here "$GPG_KEYS"
    mv gpg-conf "$HOME/.gnupg"
    chown root:root "$HOME/.gnupg"
    chmod 0700 "$HOME/.gnupg"

    # append the necessary lines to ~/.rpmmacros for signing
    cat << MACROS | tee -a "${HOME}/.rpmmacros"
%_signature gpg
%_gpg_path ${HOME}/.gnupg
%_gpg_name New Relic <support@newrelic.com>
%_gpgbin /usr/bin/gpg
MACROS

    # create an expect script to run the sign command
    # this is necessary because the sign tool asks for a passphrase
    cat << EXPECT | tee sign.expect
#!/usr/bin/expect -f
set timeout -1
spawn rpm --addsign $rpm_file
expect "Enter pass phrase:"
send -- "\r"
expect eof
EXPECT

    # run the expect script
    chmod a+x sign.expect
    ./sign.expect
}

PACKAGE_NAME='newrelic-dotnet-agent'
AGENT_HOMEDIR='newrelichome_x64_coreclr_linux'

if [ -z "$AGENT_VERSION" ]; then
    # Get the agent version from the core .dll
    version_from_dll=$(exiftool ./${AGENT_HOMEDIR}/NewRelic.Agent.Core.dll |grep "Product Version Number" |cut -d':' -f2 |tr -d ' ')
    if [[ "$version_from_dll" =~ [0-9]+\.[0-9]+\.[0-9]+\.[0-9]+ ]]; then
        major=$(echo $version_from_dll | cut -d'.' -f1)
        minor=$(echo $version_from_dll | cut -d'.' -f2)
        patch=$(echo $version_from_dll | cut -d'.' -f3)
        export AGENT_VERSION="${major}.${minor}.${patch}"
    else
        echo "AGENT_VERSION is not set, exiting."
        exit 1
    fi
fi
echo "AGENT_VERSION=|$AGENT_VERSION|"
TARGET_SYSTEM_INSTALL_PATH="/usr/local/${PACKAGE_NAME}"
SPECFILE="/rpm/${PACKAGE_NAME}.spec"

## Create the rpm build environment
mkdir -p ~/rpmbuild/{RPMS,SRPMS,BUILD,SOURCES,SPECS,tmp}
cat <<EOF >~/.rpmmacros
%_topdir   %(echo $HOME)/rpmbuild
%_tmppath  %{_topdir}/tmp
EOF

dos2unix ${SPECFILE} && cp ${SPECFILE} ~/rpmbuild/SPECS
cd ~/rpmbuild

## Create the tarball with the structure rpmbuild wants for our project
TARBALL_ROOT="${PACKAGE_NAME}-${AGENT_VERSION}"
mkdir "${TARBALL_ROOT}"
TARBALL_CONTENT_PATH="${TARBALL_ROOT}${TARGET_SYSTEM_INSTALL_PATH}"
mkdir -p "${TARBALL_CONTENT_PATH}"

cp -R /data/${AGENT_HOMEDIR}/* "${TARBALL_CONTENT_PATH}"

pushd ${TARBALL_CONTENT_PATH}

# the logs directory gets created by postinst
rm -rf logs Logs

cp /common/setenv.sh .
cp /common/run.sh .
cp /docs/core-agent-readme.md ./README.md

dos2unix *.x* extensions/*.x* *.sh

# agentinfo.json
cp /rpm/agentinfo.json .

popd
tar -zcvf "./SOURCES/${PACKAGE_NAME}-${AGENT_VERSION}.tar.gz" "${TARBALL_ROOT}"

# create RPM package(s)
rpmbuild -bb SPECS/${PACKAGE_NAME}.spec

cp RPMS/x86_64/* /release

rpm_file=$(ls -1 /release/*.rpm |tail -n 1)
echo "rpm_file=$rpm_file"

# If the $GPG_KEYS env var is set, sign the rpm(s)
if [[ ! -z "$GPG_KEYS" ]]; then
    echo "GPG_KEYS is set, signing .rpms"
    sign_rpm "$rpm_file"
fi

if [ $? -gt 0 ] ; then
    echo "::error Docker run exited with code: $?"
fi
