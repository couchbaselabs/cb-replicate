#!/bin/sh

# Mac-specific instructions
export AS="as -arch i386"
export CC="cc -framework CoreFoundation -lobjc -liconv -arch i386"
export PKG_CONFIG_PATH=$PKG_CONFIG_PATH:/usr/lib/pkgconfig:/Library/Frameworks/Mono.framework/Versions/Current/lib/pkgconfig
export LD_LIBRARY_PATH=/Library/Frameworks/Mono.framework/Versions/Current/lib/
export MONO_PATH=/Library/Frameworks/Mono.framework/Versions/Current/

cwd=`pwd`
cd bin/Release/

mkbundle  --deps --static -o cb-replicate cbreplicate.exe *.dll

cd $cwd