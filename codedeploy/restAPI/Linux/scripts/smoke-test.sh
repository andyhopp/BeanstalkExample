#!/bin/bash
curl -s -S \
  --connect-timeout 5 \
  --max-time 10 \
  --retry-connrefused \
  --retry 5 \
  --retry-delay 0 \
  --retry-max-time 40 \
  http://localhost:80/SmokeTest \
  > /dev/null