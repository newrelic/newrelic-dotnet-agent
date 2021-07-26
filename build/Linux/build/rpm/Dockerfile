FROM rpmbuild/centos7

USER root
ADD Image-ExifTool-12.28.tar.gz /exiftool
RUN yum --assumeyes install dos2unix rpm-sign expect perl-ExtUtils-MakeMaker

# Install exiftool
RUN cd /exiftool/Image-ExifTool-12.28; \
perl Makefile.PL; \
make; \
make install
