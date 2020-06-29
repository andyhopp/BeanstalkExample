#!/bin/bash
yum install -y zlib libcurl
DOTNET_31_SDK_VERSION="3.1.301"
if [ ! -x "$(command -v dotnet)" ] || ! (dotnet --list-sdks | grep "^3\.1\.3")
then
    echo 'Installing .NET Core...'
    curl -sSL -o /usr/local/bin/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
    chmod +x /usr/local/bin/dotnet-install.sh
    /usr/local/bin/dotnet-install.sh -v $DOTNET_31_SDK_VERSION
    dotnet --list-sdks
else
    echo '.NET Core SDK 3.1.3XX detected.'
fi
