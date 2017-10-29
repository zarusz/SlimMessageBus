Framework "4.6"

properties {
	$root = "$PSScriptRoot\.."

	$sln_file = "$root\src\SlimMessageBus.sln"
	$sln_platform = "Any CPU"
	$csproj_platform = "AnyCPU" 
	$config = "Release"
	$dist_folder = "$root\dist"
	
	$vs_tools = "$root\src\packages\MSBuild.Microsoft.VisualStudio.Web.targets.14.0.0.3\tools\VSToolsPath"
	
	$projects = @(
		"SlimMessageBus", 
		"SlimMessageBus.Host",
		"SlimMessageBus.Host.Serialization.Json", 
		"SlimMessageBus.Host.ServiceLocator", 
		"SlimMessageBus.Host.Autofac",
		"SlimMessageBus.Host.Kafka",
		"SlimMessageBus.Host.AzureEventHub"
	)
	
	# Ensure dist folder exists
	New-Item -ErrorAction Ignore -ItemType directory -Path $dist_folder
}

task default -depends Dist

task NuRestore {
	Write-Host "===== Restore NuGet packages" -ForegroundColor Green
	#Exec { nuget restore $sln_file }
}

task Clean -depends NuRestore {
	Write-Host "===== Clean folder $dist_folder" -ForegroundColor Green
	Remove-Item $dist_folder\* -recurse
	
	Write-Host "===== Clean solution" -ForegroundColor Green
	Exec { msbuild $sln_file /t:Clean /p:Platform=$sln_platform /p:Configuration=$config /p:VSToolsPath=$vs_tools /m }
}

task Build -depends Clean { 

	Write-Host "===== Build solution" -ForegroundColor Green
	Exec { msbuild $sln_file /t:Build /p:Platform=$sln_platform /p:Configuration=$config /p:VSToolsPath=$vs_tools /m }
}

task NuPack {

	foreach ($project in $projects) {
		Write-Host "===== Package project $project" -ForegroundColor Green
		Exec { nuget pack "$root\src\$project\$project.csproj" -OutputDirectory $dist_folder -Prop Configuration=$config -Prop Platform=$csproj_platform -IncludeReferencedProjects }
	}

}

task NuPush {

	foreach ($package in Get-ChildItem $dist_folder -filter "*.nupkg" -name) {
		Write-Host "===== Push $package to $nuget_source" -ForegroundColor Green
		Exec { nuget push "$dist_folder\$package" -Source $nuget_source }
	}
}

task Package -depends Build, NuPack {
	
}
