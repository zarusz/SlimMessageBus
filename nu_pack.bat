set dist_folder=packages-dist
set csproj_config=Release
set csproj_platform=AnyCPU 

echo Cleaning dist folder %dist_folder%
del /S /Q %dist_folder%\*

nuget pack ./SlimMessageBus/SlimMessageBus.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
nuget pack ./SlimMessageBus.Core/SlimMessageBus.Core.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
nuget pack ./SlimMessageBus.ServiceLocator/SlimMessageBus.ServiceLocator.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
