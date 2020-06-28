#!/bin/bash
which dotnet

cd /lib/systemd/system  
cat > WebApp.service <<EOF
[Unit]  
Description=.NET Core App 
  
[Service]  
ExecStart=/opt/dotnet-example/ASPNETExample.Core
EnvironmentFile=/etc/environment
WorkingDirectory=/opt/dotnet-example/  
Restart=on-failure  
SyslogIdentifier=dotnet-example  
PrivateTmp=true  
  
[Install]  
WantedBy=multi-user.target 
EOF

systemctl daemon-reload  
systemctl enable WebApp.service 