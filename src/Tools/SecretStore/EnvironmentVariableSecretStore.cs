namespace SecretStore;

public class EnvironmentVariableSecretStore : ISecretStore
{
    public string GetSecret(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (value == "(empty)")
        {
            return string.Empty;
        }
        return value;
    }
}