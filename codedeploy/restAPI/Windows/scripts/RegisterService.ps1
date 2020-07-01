$ErrorActionPreference = 'Stop'
New-Service -Name RESTAPI -BinaryPathName "c:\RESTAPI\ASPNETExample.Core.API.exe --service --urls=http://+:80" -DisplayName "REST API" -StartupType Automatic 
# allow HTTP through the Windows firewall
netsh advfirewall firewall add rule name = "Open HTTP" dir=in action=allow protocol=TCP localport=80