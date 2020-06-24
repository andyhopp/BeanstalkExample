$ErrorActionPreference = 'Stop'
New-Service -Name eShopOnWeb -BinaryPathName "c:\WebApp\BeanstalkExample.exe --service" -DisplayName "eShopOnWeb Sample App" -StartupType Automatic 
# allow HTTP through the Windows firewall
netsh advfirewall firewall add rule name = "Open HTTP" dir=in action=allow protocol=TCP localport=80