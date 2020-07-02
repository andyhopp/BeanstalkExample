#!/bin/bash

cd /lib/systemd/system  
cat > RESTAPI.service <<EOF
[Unit]  
Description=.NET Core App 
  
[Service]  
ExecStart=/opt/rest-api/ASPNETExample.Core.API --service --urls=http://+:80
EnvironmentFile=/etc/environment
WorkingDirectory=/opt/rest-api
Restart=on-failure  
SyslogIdentifier=rest-api
PrivateTmp=true  
  
[Install]  
WantedBy=multi-user.target 
EOF

systemctl daemon-reload  
systemctl enable RESTAPI.service 