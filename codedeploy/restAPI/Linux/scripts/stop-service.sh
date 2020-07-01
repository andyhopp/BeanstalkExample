#!/bin/bash
isExistApp=`pgrep ASPNETExample.Core.API`
if [[ -n  $isExistApp ]]; then
    service RESTAPI stop
fi
systemctl disable RESTAPI.service
rm /lib/systemd/system/RESTAPI.service
systemctl daemon-reload