set dist_folder=packages-dist
set csproj_config=Release
set csproj_platform=AnyCPU 

echo Cleaning dist folder %dist_folder%
rmdir /S /Q %dist_folder%
mkdir %dist_folder%

nuget pack ./SlimMessageBus/SlimMessageBus.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
nuget pack ./SlimMessageBus.Host/SlimMessageBus.Host.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
nuget pack ./SlimMessageBus.Host.Serialization.Json/SlimMessageBus.Host.Serialization.Json.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
nuget pack ./SlimMessageBus.Host.ServiceLocator/SlimMessageBus.Host.ServiceLocator.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
nuget pack ./SlimMessageBus.Host.Autofac/SlimMessageBus.Host.Autofac.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
nuget pack ./SlimMessageBus.Host.Kafka/SlimMessageBus.Host.Kafka.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects

rem nuget pack ./SlimMessageBus.Core/SlimMessageBus.Core.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
rem nuget pack ./SlimMessageBus.ServiceLocator/SlimMessageBus.ServiceLocator.csproj -OutputDirectory ./%dist_folder% -Prop Configuration=%csproj_config% -Prop Platform=%csproj_platform% -IncludeReferencedProjects
