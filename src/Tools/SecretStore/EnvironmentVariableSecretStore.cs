namespace SecretStore
{
    using System;

    public class EnvironmentVariableSecretStore : ISecretStore
    {
        #region Implementation of ISecretStore

        public string GetSecret(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        #endregion
    }
}