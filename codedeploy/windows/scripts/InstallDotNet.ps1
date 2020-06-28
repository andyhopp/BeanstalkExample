if (-not ($(dotnet --list-sdks) -match "3.1.3")) {
	$DOTNET_31_SDK_VERSION="3.1.301"
	echo 'Installing .NET Core...'
	Invoke-WebRequest -OutFile dotnet-install.ps1 https://dot.net/v1/dotnet-install.ps1
	.\dotnet-install.ps1 -Version $DOTNET_31_SDK_VERSION
	dotnet --list-sdks
} else {
	echo '.NET Core 3.1.3XX SDK detected.'
}