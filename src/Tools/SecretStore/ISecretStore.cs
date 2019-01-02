namespace SecretStore
{
    public interface ISecretStore
    {
        string GetSecret(string name);
    }
}