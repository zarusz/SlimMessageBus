set sln_file=SlimMessageBus.sln
nuget.exe restore %sln_file%
msbuild %sln_file% /t:Clean;Build /p:Platform="Any CPU" /p:Configuration=Release