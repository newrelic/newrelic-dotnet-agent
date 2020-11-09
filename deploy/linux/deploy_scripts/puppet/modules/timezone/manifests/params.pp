class timezone::params {
  case $::operatingsystem {
    /(Ubuntu|Debian|Gentoo|CentOS|Amazon)/: {
      $package = 'tzdata'
      $zoneinfo_dir = '/usr/share/zoneinfo/'
      $config_file = '/etc/localtime'
    }
    default: {
      fail("Unsupported platform: ${::operatingsystem}")
    }
  }
}
