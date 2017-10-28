Import-Module $PSScriptRoot\psake\psake.psm1

& Invoke-psake $PSScriptRoot\tasks.ps1 NuPush -parameters @{"nuget_source"="https://www.nuget.org"}