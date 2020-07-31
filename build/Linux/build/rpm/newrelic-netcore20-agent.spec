# Turn off some things we don't want
%define        __spec_install_post %{nil}
%define          debug_package %{nil}
%define        __os_install_post %{_dbpath}/brp-compress

Name: newrelic-netcore20-agent
Version: %{getenv:AGENT_VERSION}
Release: 1
License: Commercial
Vendor: New Relic, Inc.
Group: Development/Languages
Summary: The New Relic .NET agent for .NET Core 2.0

URL: http://newrelic.com/
BuildRoot: %{_tmppath}/%{name}-%{version}-%{release}-root

SOURCE0: %{name}-%{version}.tar.gz
%define _install /usr/local/%{name}

%description
The New Relic .NET agent monitors applications running
on .NET Core 2.0.

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

# create logs dir
mkdir -p $NEWRELIC_HOME/logs 2> /dev/null

if [ ! -L /var/log/newrelic/dotnet ]; then
  mkdir -p /var/log/newrelic 2> /dev/null
  ln -sTf $NEWRELIC_HOME/logs /var/log/newrelic/dotnet 2> /dev/null
fi

echo "export CORECLR_NEWRELIC_HOME=${NEWRELIC_HOME}" > /etc/profile.d/%{name}-path.sh
source /etc/profile.d/%{name}-path.sh

chmod o+w $NEWRELIC_HOME/logs
chmod +x $NEWRELIC_HOME/*.sh 2> /dev/null

printf "Initialize the New Relic .NET Agent environment variables by running:\n"
printf "\t\033[1msource /etc/profile.d/%{name}-path.sh\033[0m\n"
printf "\t\033[1msource $CORECLR_NEWRELIC_HOME/setenv.sh\033[0m\n"
