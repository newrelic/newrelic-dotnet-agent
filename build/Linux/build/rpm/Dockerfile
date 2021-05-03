FROM rpmbuild/centos7

USER root
RUN yum --assumeyes install dos2unix rpm-sign expect perl-ExtUtils-MakeMaker

# Install exiftool
RUN curl -o exiftool.tgz https://exiftool.org/Image-ExifTool-12.25.tar.gz; \
tar -zxvf exiftool.tgz; \
cd Image-ExifTool-12.25; \
perl Makefile.PL; \
make; \
make install
