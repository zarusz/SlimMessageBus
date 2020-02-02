# What

This shows the Avro IDL contract first apparch using the Apache.Avro library.
First we need to define the IDL (*.avdl), then we generate C# classes from it.

* The Avro contract is defined in [Avro IDL](https://avro.apache.org/docs/current/idl.html#overview_usage) (*.avdl).
* Then it is transformed into the Avro Protocol (*.avpr) - using the java tool.
* Then we generated C# classes - using the dotnet avro tool.

# Prerequisites

1. Install Avro dotnet tools for code generation:
```cmd
dotnet tool install -g Apache.Avro.Tools
```

2. Install [Java SDK (1.8)](https://www.oracle.com/technetwork/java/javase/downloads/jdk8-downloads-2133151.html).

# Usage

In PowerShell:
```cmd
cd Tools
.\gen.ps1
```

C# classes and *.avpr files are generated from the *.avdl files.
