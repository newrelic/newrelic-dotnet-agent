#
# Provisioning for PHP/LSM deploy VM
#

class { 'timezone':
  timezone => 'America/Los_Angeles'
}

# APT repo mgmt
package { "apt-utils":
  ensure => installed
}

# YUM repo mgmt
package { "createrepo-c":
  ensure => installed
}

# Used by deploy scripts to gather build artifacts from Jenkins.
file { ["/vagrant/builds"]:
  ensure => "directory",
  mode   => 0755,
  owner  => vagrant,
  group  => vagrant
}

# Contains the APT, YUM, and product file repositories that will be
# published to CHI as the final deploy step.
file { ["/vagrant/staging"]:
  ensure => "directory",
  mode   => 0755,
  owner  => vagrant,
  group  => vagrant
}
