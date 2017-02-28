#!/bin/bash

for i in "$@"
do
case ${i} in
    -v=*|--version=*)
    VERSIONNUMBER="${i#*=}"
    shift # past argument=value
    ;;
    *)
            # unknown option
    ;;
esac
done

echo "VERSION = ${VERSIONNUMBER}"

if [ -z ${VERSIONNUMBER+x} ]
then
    echo "No version is specified, defaulting to 0.0.0"
    VERSIONNUMBER=0.0.0
fi

scriptsDir=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
rootDir="$(dirname "$scriptsDir")"
projectFiles=${rootDir}/src/**/*.csproj

for projectFile in ${projectFiles}
do
    echo "patching $projectFile with version $VERSIONNUMBER"
	sed -i -E "s/(<Version>)0.0.0(<\/Version>)/\1$VERSIONNUMBER\2/" ${projectFile}
done