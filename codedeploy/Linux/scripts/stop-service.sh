#!/bin/bash
isExistApp=`pgrep ASPNETExample.Core`
if [[ -n  $isExistApp ]]; then
    service WebApp stop
fi