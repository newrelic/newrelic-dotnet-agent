FROM rpmbuild/centos7

USER root
ADD Image-ExifTool-12.28.tar.gz /exiftool

# temporary workaround until we can move to a non-centos7 base image
RUN sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo && \ 
    sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo && \
    sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo
    
RUN yum --assumeyes install dos2unix rpm-sign expect perl-ExtUtils-MakeMaker

# Install exiftool
RUN cd /exiftool/Image-ExifTool-12.28; \
perl Makefile.PL; \
make; \
make install
