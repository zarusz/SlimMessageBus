$root = [System.IO.Path]::GetFullPath("$PSScriptRoot\..")

$sln_file = "$root\src\SlimMessageBus.sln"
$sln_platform = "Any CPU"
$csp_platform = "AnyCPU" 
$config = "Release"
$dist_folder = "$root\dist"
# choose: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]
$msbuild_verbosity = "n"
		
$projects = @(
	"SlimMessageBus",

	"SlimMessageBus.Host",

	"SlimMessageBus.Host.Interceptor", 

	"SlimMessageBus.Host.Serialization", 

	"SlimMessageBus.Host.Serialization.Hybrid", 
	"SlimMessageBus.Host.Serialization.Json", 
	"SlimMessageBus.Host.Serialization.Avro", 

	"SlimMessageBus.Host.DependencyResolver", 

	"SlimMessageBus.Host.AspNetCore",
	"SlimMessageBus.Host.MsDependencyInjection",
	"SlimMessageBus.Host.ServiceLocator", 
	"SlimMessageBus.Host.Autofac",
	"SlimMessageBus.Host.Unity",

	"SlimMessageBus.Host.Kafka",
	"SlimMessageBus.Host.AzureEventHub",
	"SlimMessageBus.Host.AzureServiceBus",
	"SlimMessageBus.Host.Memory",
	"SlimMessageBus.Host.Redis",
	"SlimMessageBus.Host.Hybrid"
)

# msbuild.exe https://msdn.microsoft.com/pl-pl/library/ms164311(v=vs.80).aspx
	
function _AssertExec() {
	if ($LastExitCode -ne 0) { exit 1 }
}

function _Step($msg) {
	Write-Host ""
	Write-Host "===== $msg =====" -ForegroundColor Green
}

function NuRestore() {
	_Step "Restore NuGet packages"
	& dotnet restore $sln_file /p:Platform=$sln_platform /p:Configuration=$config --verbosity n
	_AssertExec
}

function _MsBuild($target) {
	_Step "$target solution"
	& dotnet build $sln_file -c $config -v $msbuild_verbosity
	_AssertExec
}

function Clean() {
	
	_Step "Clean folder $dist_folder"
	# Ensure dist folder exists
	New-Item -ErrorAction Ignore -ItemType directory -Path $dist_folder
	Remove-Item $dist_folder\* -recurse
	
	_MsBuild "Clean"
}

function Build() { 
	Clean	
	NuRestore
	_MsBuild "Build"
}

function Test() { 
	_Step "Runnint tests"
	& dotnet test $sln_file -v $msbuild_verbosity
	_AssertExec
}

function TestCi() {
	_Step "Runnint tests (skipping tests requiring local infrastructure)"
	& dotnet test $sln_file --filter "Category!=Local"
	_AssertExec
}

function NuPack() {
	foreach ($project in $projects) {
		_Step "Package project $project"
		& dotnet pack "$root\src\$project\$project.csproj" --output $dist_folder --configuration $config
		_AssertExec
	}
}

function NuPush($nuget_source) {
	foreach ($package in Get-ChildItem $dist_folder -filter "*.nupkg" -name) {
		_Step "Push $package to $nuget_source"
		& dotnet nuget push "$dist_folder\$package" --source $nuget_source
		_AssertExec
	}
}

function Package() {
	Build
	NuPack	
}
