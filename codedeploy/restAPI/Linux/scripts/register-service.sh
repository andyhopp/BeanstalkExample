#!/bin/bash
SERVICE_ID=rest-api
SERVICE_PATH=/opt/$SERVICE_ID/
SERVICE_NAME=RESTAPI

cd /lib/systemd/system  
cat > $SERVICE_NAME.service <<EOF
[Unit]  
Description=.NET Core App 
  
[Service]  
ExecStart=$SERVICE_PATHASPNETExample.Core.API --service --urls=http://+:80
EnvironmentFile=/etc/environment
WorkingDirectory=$SERVICE_PATH  
Restart=on-failure  
SyslogIdentifier=$SERVICE_ID  
PrivateTmp=true  
  
[Install]  
WantedBy=multi-user.target 
EOF

systemctl daemon-reload  
systemctl enable $SERVICE_NAME.service 