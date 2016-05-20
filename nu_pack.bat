nuget pack ./SlimMessageBus/SlimMessageBus.csproj -OutputDirectory ./packages-own -Prop Configuration=Release -Prop Platform=AnyCPU -IncludeReferencedProjects
nuget pack ./SlimMessageBus.Core/SlimMessageBus.Core.csproj -OutputDirectory ./packages-own -Prop Configuration=Release -Prop Platform=AnyCPU -IncludeReferencedProjects
nuget pack ./SlimMessageBus.ServiceLocator/SlimMessageBus.ServiceLocator.csproj -OutputDirectory ./packages-own -Prop Configuration=Release -Prop Platform=AnyCPU -IncludeReferencedProjects
