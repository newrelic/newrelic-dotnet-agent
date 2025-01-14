#!/bin/sh
# Dump of /usr/local/bin/cosmos/start.sh in the docker image... useful for discovering undocumented env vars, unless it changes! :)

export PAL_NO_DEFAULT_PACKAGES=1
export PAL_LOADER_SNAPS=1
export PAL_NET_TCP_PORT_MAPPING=8081=8081:8900=8900:8901=8901:8902=8902:10250=10250:10251=10251:10252=10252:10253=10253:10254=10254:10255=10255:10350=10350

ipaddr="`hostname -I | grep -o '^[0-9]\+\.[0-9]\+\.[0-9]\+\.[0-9]\+'`"

EMULATOR_PARTITION_SETTINGS="/masterpartitioncount=1 /partitioncount=${AZURE_COSMOS_EMULATOR_PARTITION_COUNT:-10} /defaultpartitioncount=${AZURE_COSMOS_EMULATOR_DEFAULT_PARTITION_COUNT:-0}"

EMULATOR_OTHER_ARGS="${AZURE_COSMOS_EMULATOR_ARGS:-/enablepreview}"

EMULATOR_KEY="${AZURE_COSMOS_EMULATOR_KEY:-C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==}"

EMULATOR_OTHER_IP_ADDRESSES="`hostname -I | grep -o '[0-9]\+\.[0-9]\+\.[0-9]\+\.[0-9]\+' | xargs | tr ' ' ','`"

EMULATOR_IP_ADDRESS=${AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE:-$ipaddr}

COSMOS_APP_HOME=/tmp/cosmos


EMULATOR_DEFAULT_CERTIFICATE="default.sslcert.pfx"
EMULATOR_CERTIFICATE_OPTION="/exportcert=c:\\${EMULATOR_DEFAULT_CERTIFICATE}"

if [ -z "${AZURE_COSMOS_EMULATOR_CERTIFICATE}${AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE}" ]; then
        # Work around to remove old emulator data since restarting with existing data does not work at this time.
        if test -d "${COSMOS_APP_HOME}/appdata"; then
            rm -fr "${COSMOS_APP_HOME}/appdata"
        fi

        mkdir -p ${COSMOS_APP_HOME}/appdata
else
        if test -d "${COSMOS_APP_HOME}/appdata"; then
            rm -fr "${COSMOS_APP_HOME}/appdata/log"
            rm -fr "${COSMOS_APP_HOME}/appdata/var"
            rm -fr "${COSMOS_APP_HOME}/appdata/wfroot"
            rm -fr "${COSMOS_APP_HOME}/appdata/Packages"
            rm -fr "${COSMOS_APP_HOME}/appdata/gateway.log"
        else
                mkdir -p ${COSMOS_APP_HOME}/appdata
        fi

        if [ ! -z "${AZURE_COSMOS_EMULATOR_CERTIFICATE}" ]; then
                if test -f ${AZURE_COSMOS_EMULATOR_CERTIFICATE}; then
                        cp -f ${AZURE_COSMOS_EMULATOR_CERTIFICATE} ${COSMOS_APP_HOME}/appdata/${EMULATOR_DEFAULT_CERTIFICATE}
                        EMULATOR_CERTIFICATE_OPTION="/importcert=c:\\${EMULATOR_DEFAULT_CERTIFICATE}"
                else
                        echo "ERROR: ${AZURE_COSMOS_EMULATOR_CERTIFICATE} not found"
                        exit 1
                fi
        else
                EMULATOR_CERTIFICATE_OPTION="/enabledatapersistence /importcert=c:\\${EMULATOR_DEFAULT_CERTIFICATE} /exportcert=c:\\${EMULATOR_DEFAULT_CERTIFICATE}"
                if test -f ${COSMOS_APP_HOME}/appdata/.system/profiles/Client/AppData/Local/CosmosDBEmulator/${EMULATOR_DEFAULT_CERTIFICATE}; then
                        cp -f ${COSMOS_APP_HOME}/appdata/.system/profiles/Client/AppData/Local/CosmosDBEmulator/${EMULATOR_DEFAULT_CERTIFICATE} ${COSMOS_APP_HOME}/appdata/${EMULATOR_DEFAULT_CERTIFICATE}
                else
                        if ! test -f ${COSMOS_APP_HOME}/appdata/${EMULATOR_DEFAULT_CERTIFICATE}; then
                                if test -d "${COSMOS_APP_HOME}/appdata/.system"; then
                                    rm -fr "${COSMOS_APP_HOME}/appdata/.system"
                                fi
                        fi
                fi
        fi
fi

./palrun -w $COSMOS_APP_HOME/appdata --http Native -p System=./packages/Windows.6.2.9200.999.0/x64 -p Common=./packages/WindowsCommon.10.0.17134.1.0 -p ./system.security.sfp -p ./system.certificates.sfp -p ./system.netfx.sfp -p Emulator=./packages/Cosmos.1.0.999.0/x64 -e DocumentDBEmulator_IPAddress=127.0.0.1 -e Cosmos.Emulator.TextLogDir=c:\log -- /Microsoft.Azure.Cosmos.Emulator.exe /enablepreview /disableRIO /minimal $EMULATOR_PARTITION_SETTINGS /disablethrottling $EMULATOR_CERTIFICATE_OPTION /alternativenames=$EMULATOR_IP_ADDRESS,$EMULATOR_OTHER_IP_ADDRESSES /alternativeips=$EMULATOR_IP_ADDRESS,$EMULATOR_OTHER_IP_ADDRESSES /publicipaddressoverride=$EMULATOR_IP_ADDRESS /AllowNetworkAccess /Key=$EMULATOR_KEY $EMULATOR_OTHER_ARGS