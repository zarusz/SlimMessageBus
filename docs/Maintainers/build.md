## Building and Testing

### Run Build

```bash
cd src
dotnet build
dotnet test --filter Category!=Integration
```

### Run Tests

The integration tests use [Testcontainers](https://testcontainers.com/) and a container runtime is required (e.g. Docker).

However, some tests require cloud infrastructure (AWS, Azure).
SMB has message brokers set up in the cloud (secrets not shared) for its GitHub actions pipeline.

To run tests you need to update the `secrets.txt` to match your cloud infrastructure or [local infrastructure](/src/Infrastructure/README.md).

Run all tests:

```bash
dotnet test
```

Run all tests except integration tests that require local/cloud infrastructure:

```bash
dotnet test --filter Category!=Integration
```
