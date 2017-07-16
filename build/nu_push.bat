set dist_folder=packages-dist
set version=0.9.11
rem nuget push .\%dist_folder%\SlimMessageBus.0.9.10.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.Host.0.9.14.nupkg -Source %nuget_source%
rem nuget push .\%dist_folder%\SlimMessageBus.Host.ServiceLocator.0.9.11.nupkg -Source %nuget_source%
rem nuget push .\%dist_folder%\SlimMessageBus.Host.Autofac.0.9.11.nupkg -Source %nuget_source%
rem nuget push .\%dist_folder%\SlimMessageBus.Host.Serialization.Json.0.9.10.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.Host.Kafka.0.9.14.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.Host.AzureEventHub.0.9.14-alpha1.nupkg -Source %nuget_source%


