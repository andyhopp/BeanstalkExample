$ErrorActionPreference = 'Stop'
Stop-Service BeanstalkExample
sc.exe delete BeanstalkExample