using System;
using System.Text.RegularExpressions;

namespace SecretStore
{
    public class SecretService
    {
        private readonly Regex _placeholder = new Regex(@"\{\{(\w+)\}\}");
        private readonly ISecretStore _secretStore;

        public SecretService(ISecretStore secretStore)
        {
            _secretStore = secretStore;
        }

        public string PopulateSecrets(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var index = 0;
                Match match;
                do
                {
                    match = _placeholder.Match(value, index);
                    if (match.Success)
                    {
                        var secretName = match.Groups[1].Value;
                        var secretValue = _secretStore.GetSecret(secretName);
                        if (secretValue == null)
                        {
                            throw new ApplicationException($"The secret name '{secretName}' was not present in vault. Ensure that you have a local secrets.txt file in the src folder.");
                        }
                        value = value.Replace(match.Value, secretValue, StringComparison.InvariantCulture);
                        index = match.Index + 1;
                    }
                } while (match.Success);
            }
            return value;
        }
    }
}