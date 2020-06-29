#!/bin/bash
DOTNET_31_SDK_VERSION="3.1.301"
if [ ! -x "$(command -v dotnet)" ] || ! (dotnet --list-sdks | grep "^3\.1\.3")
then
    echo 'Installing .NET Core...'
    curl -sSL -o /usr/local/bin/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
    chmod +x /usr/local/bin/dotnet-install.sh
    /usr/local/bin/dotnet-install.sh -v $DOTNET_31_SDK_VERSION --install-dir /usr/share/dotnet
    if [ ! -L /usr/bin/dotnet ]; then
      ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
    fi
    if echo ":$PATH:" | grep -v -q ":~/.dotnet/tools/:"; then
      PATH="~/.dotnet/tools/:$PATH"
    fi
    dotnet --list-sdks
else
    echo '.NET Core SDK 3.1.3XX detected.'
fi

if ( systemctl list-units --full -all | grep -v -Eq "^\\s+xray.service" )
then
  echo 'Installing X-Ray daemon...'
  curl -o /tmp/aws-xray-daemon-3.x.rpm https://s3.us-east-2.amazonaws.com/aws-xray-assets.us-east-2/xray-daemon/aws-xray-daemon-3.x.rpm
  yum install -y /tmp/aws-xray-daemon-3.x.rpm
else
  echo 'X-Ray daemon already installed.'
fi
