# Turn off some things we don't want
%define        __spec_install_post %{nil}
%define          debug_package %{nil}
%define        __os_install_post %{_dbpath}/brp-compress

Name: newrelic-dotnet-agent
Version: %{getenv:AGENT_VERSION}
Release: 1
License: Apache Software License 2.0
Vendor: New Relic, Inc.
Group: Development/Languages
Summary: The New Relic agent for .NET Core
Obsoletes: newrelic-netcore20-agent

URL: http://newrelic.com/
BuildRoot: %{_tmppath}/%{name}-%{version}-%{release}-root

SOURCE0: %{name}-%{version}.tar.gz
%define _install /usr/local/%{name}

%description
The New Relic .NET agent monitors applications running
on .NET Core 3.1+.

%prep
%setup -q

%build
# Empty section

%install
rm -rf %{buildroot}
mkdir -p  %{buildroot}

# in builddir
cp -a * %{buildroot}

%clean
rm -rf %{buildroot}

%files
%defattr(-,root,root,-)
%config(noreplace) %{_install}/newrelic.config
%{_install}/*

%post
NEWRELIC_HOME=/usr/local/%{name}
OBSOLETE_PACKAGE_NAME=newrelic-netcore20-agent
OBSOLETE_NEWRELIC_HOME=/usr/local/${OBSOLETE_PACKAGE_NAME}

# create logs dir
mkdir -p $NEWRELIC_HOME/logs 2> /dev/null

# create symlink to logs dir in /var/log/newrelic
mkdir -p /var/log/newrelic 2> /dev/null
ln -sTf $NEWRELIC_HOME/logs /var/log/newrelic/dotnet 2> /dev/null

# remove old profile.d file if it exists
oldHomeDirFile="/etc/profile.d/${OBSOLETE_PACKAGE_NAME}-path.sh"
if [ -e $oldHomeDirFile ]; then
  echo "Cleaning up $oldHomeDirFile"
  rm -f $oldHomeDirFile
fi

# migrate data from obsoleted package, if applicable
if [ -d $OBSOLETE_NEWRELIC_HOME ]; then

  # migrate config file, backing up original first
  if [ -e $OBSOLETE_NEWRELIC_HOME/newrelic.config ]; then
    echo "Migrating newrelic.config from $OBSOLETE_NEWRELIC_HOME"
    # Move the existing config file in the new package directory out of the way
    mv $NEWRELIC_HOME/newrelic.config $NEWRELIC_HOME/newrelic.config.original
    # Copy the config file from the old package directory to the new package directory
    cp -v $OBSOLETE_NEWRELIC_HOME/newrelic.config $NEWRELIC_HOME/newrelic.config
    # Rename the config file in the old package directory so it won't get migrated again
    mv $OBSOLETE_NEWRELIC_HOME/newrelic.config $OBSOLETE_NEWRELIC_HOME/newrelic.config.migrated
  fi

  # migrate any custom instrumentation
  if [ -d $OBSOLETE_NEWRELIC_HOME/extensions ]; then
    # This is safe to run multiple times because of the -n option, which means "don't overwrite an existing file"
    # This also means that only custom instrumentation XML files (not our default auto-instrumentation ones) will be migrated the first time
    cp -nv $OBSOLETE_NEWRELIC_HOME/extensions/*.xml $NEWRELIC_HOME/extensions
  fi
fi

# Deprecated instrumentation files to remove post install
rm -f $NEWRELIC_HOME/extensions/NewRelic.Providers.Wrapper.Logging.Instrumentation.xml 2> /dev/null
rm -f $NEWRELIC_HOME/extensions/NewRelic.Providers.Wrapper.Logging.dll 2> /dev/null

echo "export CORECLR_NEWRELIC_HOME=${NEWRELIC_HOME}" > /etc/profile.d/%{name}-path.sh
source /etc/profile.d/%{name}-path.sh

chmod o+w $NEWRELIC_HOME/logs
chmod +x $NEWRELIC_HOME/*.sh 2> /dev/null

printf "Initialize the New Relic .NET Agent environment variables by running:\n"
printf "\t\033[1msource /etc/profile.d/%{name}-path.sh\033[0m\n"
printf "\t\033[1msource $CORECLR_NEWRELIC_HOME/setenv.sh\033[0m\n"
