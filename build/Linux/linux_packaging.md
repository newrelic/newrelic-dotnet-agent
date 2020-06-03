# Linux Packaging

The Linux CoreCLR agent can be packaged into .rpm (for Red Hat/Centos/Oracle/SUSE systems) and/or .deb (for Debian/Ubuntu/Mint systems) packages using Docker.

1. From Visual Studio, build the FullAgent solution in 64-bit mode, `Release` or `Debug` depending on your needs.
2. In Powershell, from the top-level `Build` directory:
      1. `.\package.ps1 -configuration {Release|Debug}` (choose the configuration based on which mode you built the agent as)
3. In Powershell, from `Build/Linux`:
      1. `docker-compose build`
      2. `docker-compose run build_rpm`
      3. `docker-compose run build_deb`

Optional: sign the .rpm

1. `docker-compose run -e GPG_KEYS=/keys/gpg.tar.bz2 build_rpm`

You can do ad hoc testing inside containers using the `test_debian` and/or `test_centos` services and `bash`.

     docker-compose run test_debian bash
     docker-compose run test_centos bash
