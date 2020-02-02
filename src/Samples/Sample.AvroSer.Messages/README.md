# What

* The Avro contract is defined in [Avro IDL](https://avro.apache.org/docs/current/idl.html#overview_usage) (*.avdl).
* Then it is transformed into the Avro Protocol (*.avpr) - using the java tool.
* Then we generated C# classes - using the dotnet avro tool.

# Prerequisites

Install Avro dotnet tools for code generation:
```cmd
dotnet tool install -g Apache.Avro.Tools
```

Install [Java SDK (1.8)](https://www.oracle.com/technetwork/java/javase/downloads/jdk8-downloads-2133151.html).

# Usage

In PowerShell:
```cmd
cd Tools
.\gen.ps1
```

C# classes and *.avpr files are generated from the *.avdl files.
