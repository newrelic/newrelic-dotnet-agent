# Linux Packaging

The Linux CoreCLR agent can be packaged into .rpm (for Red Hat/Centos/Oracle/SUSE systems) and/or .deb (for Debian/Ubuntu/Mint systems) packages using Docker for Windows in Linux Containers mode.

1. From Visual Studio, build the FullAgent solution in 64-bit mode, `Release` or `Debug` depending on your needs.
2. In Powershell, from the top-level `Build` directory:
      1. `.\package.ps1 -configuration {Release|Debug}` (choose the configuration based on which mode you built the agent as)
3. In Powershell, from `Build/Linux`:
      1. `docker-compose build`
      2. `docker-compose run build_rpm`
      3. `docker-compose run build_deb`

If you want to test .RPM signing as part of the build process, do the following (note: this is totally optional and not required under normal dev/test circumstances):

1. Get the GPG keys tarball from somebody who has it (should be called `gpg.tar.bz2`)
2. Place the tarball `Build/Linux/keys`
3. `docker-compose run -e GPG_KEYS=/keys/gpg.tar.bz2 build_rpm` (Note: this command only appears to work from Powershell, not git bash)

You can do ad hoc testing inside containers using the `test_debian` and/or `test_centos` services and `bash`.  Remember to prepend `winpty` when running a docker bash session on Windows.

     winpty docker-compose run test_debian bash
     winpty docker-compose run test_centos bash
