#!/bin/bash
set -e # Halt the build script on any error

# subroutine for signing .rpm.  NB: this should not be called multiple times!
function sign_rpm {
    rpm_file="$1"

    # import the private gpg key
    gpg --import --batch --pinentry-mode loopback --passphrase "$GPG_KEY_PASSPHRASE" "$GPG_KEY"

    # create a passphrase file
    echo "$GPG_KEY_PASSPHRASE" > /tmp/gpg-passphrase.txt && chmod 400 /tmp/gpg-passphrase.txt

    # append the necessary lines to ~/.rpmmacros for signing
    cat << MACROS | tee -a "${HOME}/.rpmmacros"
%_signature gpg
%_gpg_path ${HOME}/.gnupg
%_gpg_name New Relic Signing Key (2025) <support@newrelic.com>
%_gpgbin /usr/bin/gpg
%_gpg_sign_cmd_extra_args --batch --pinentry-mode loopback --passphrase-file /tmp/gpg-passphrase.txt
MACROS

    # sign the rpm
    rpm --addsign $rpm_file
}

PACKAGE_NAME='newrelic-dotnet-agent'
AGENT_HOMEDIR='newrelichome_x64_coreclr_linux'

if [ -z "$AGENT_VERSION" ]; then
    # Get the agent version from the core .dll
    version_from_dll=$(/exiftool/exiftool ./${AGENT_HOMEDIR}/NewRelic.Agent.Core.dll |grep "Product Version Number" |cut -d':' -f2 |tr -d ' ')
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

# If the $GPG_KEY env var is set, sign the rpm(s)
if [[ ! -z "$GPG_KEY" ]]; then
    echo "GPG_KEY is set, signing .rpms"
    # make sure the passphrase is also set
    if [[ -z "$GPG_KEY_PASSPHRASE" ]]; then
        echo "GPG_KEY_PASSPHRASE is not set, cannot sign .rpms"
        exit 1
    fi
    sign_rpm "$rpm_file"
fi

if [ $? -gt 0 ] ; then
    echo "::error Docker run exited with code: $?"
fi
