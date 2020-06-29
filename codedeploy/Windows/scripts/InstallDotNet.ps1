if (-not ($(dotnet --list-sdks) -match "3.1.3")) {
	$DOTNET_31_SDK_VERSION="3.1.301"
	echo 'Installing .NET Core...'
	Invoke-WebRequest -OutFile dotnet-install.ps1 https://dot.net/v1/dotnet-install.ps1
	.\dotnet-install.ps1 -Version $DOTNET_31_SDK_VERSION
	dotnet --list-sdks
} else {
	echo '.NET Core 3.1.3XX SDK detected.'
}

$daemonName = "AWSXRayDaemon"
if (-not (Get-Service $daemonName -ErrorAction SilentlyContinue)) {
	$targetLocation = "C:\Program Files\Amazon\XRay"
	if (-not (Test-Path $targetLocation)) {
    		New-Item -ItemType Directory $targetLocation
	}

	$destPath = Join-Path $targetLocation "aws-xray-daemon"
	if (Test-Path $destPath) {
    		Remove-Item -Recurse -Force $destPath
	}

	$url = "https://s3.us-east-2.amazonaws.com/aws-xray-assets.us-east-2/xray-daemon/aws-xray-daemon-windows-service-3.x.zip"
	$zipFileName = [System.IO.Path]::GetFileName($url)
	$zipPath = Join-Path $targetLocation $zipFileName
	$daemonPath = Join-Path $destPath "xray.exe"
	$daemonLogPath = Join-Path $targetLocation "xray-daemon.log"
	Invoke-WebRequest -Uri $url -OutFile $zipPath
	Add-Type -Assembly "System.IO.Compression.Filesystem"
	[System.IO.Compression.Zipfile]::ExtractToDirectory($zipPath, $destPath)
	New-Service -Name $daemonName -StartupType Automatic -BinaryPathName "`"$daemonPath`" -f `"$daemonLogPath`""
	Start-Service AWSXRayDaemon
}