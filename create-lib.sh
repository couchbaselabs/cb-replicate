#!/bin/sh

# Mac-specific instructions
export AS="as -arch x86_64"
export CC="cc -framework CoreFoundation -lobjc -liconv -arch x86_64"
export MONO_PATH=/Library/Frameworks/Mono.framework/Versions/Current
export PKG_CONFIG_PATH=$PKG_CONFIG_PATH:/usr/lib/pkgconfig:$MONO_PATH/lib/pkgconfig
export LD_LIBRARY_PATH=$MONO_PATH/lib:$MONO_PATH/include

cwd=`pwd`
cd bin/Release/

mkbundle -c -o host.c --nomain --deps -oo cb-replicate.o cbreplicate.exe *.dll

echo "\n\n\nYou can build using:\n\n\tcc bin/Release/host.c bin/Release/cb-replicate.o $MONO_PATH/lib/libmono-2.0.a -I $MONO_PATH/include/mono-2.0/ -mmacosx-version-min=10.11  -framework CoreFoundation -lobjc -liconv -v\n\n"

cd $cwd