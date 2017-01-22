set dist_folder=packages-dist
set nuget_source=https://www.nuget.org
nuget push .\%dist_folder%\SlimMessageBus.0.9.1.*.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.Host.0.9.1.*.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.Host.ServiceLocator.0.9.1.*.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.Host.Serialization.Json.0.9.1.*.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.Host.Kafka.0.9.1.*.nupkg -Source %nuget_source%


