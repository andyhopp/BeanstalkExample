$ErrorActionPreference = 'Stop'
New-Service -Name WebApp -BinaryPathName "c:\WebApp\ASPNETExample.Core.exe --service --urls=http://+:80" -DisplayName ".NET Core Example" -StartupType Automatic 
# allow HTTP through the Windows firewall
netsh advfirewall firewall add rule name = "Open HTTP" dir=in action=allow protocol=TCP localport=80