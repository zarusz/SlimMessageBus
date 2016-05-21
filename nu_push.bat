set dist_folder=packages-dist
set nuget_source=https://www.nuget.org
nuget push .\%dist_folder%\SlimMessageBus.0.8.1.*.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.Core.0.8.1.*.nupkg -Source %nuget_source%
nuget push .\%dist_folder%\SlimMessageBus.ServiceLocator.0.8.1.*.nupkg -Source %nuget_source%
