using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlimMessageBus.Host.AzureServiceBus.Test
{
    public class SecretsStore
    {
        private readonly IDictionary<string, string> _secrets;
        private readonly Regex _placeholder = new Regex(@"\{\{(\w+)\}\}");

        public SecretsStore(string path)
        {
            var lines = File.ReadAllLines(path);
            _secrets = lines
                .Select(x => x.Split('=', 2, StringSplitOptions.RemoveEmptyEntries))
                .ToDictionary(x => x[0], x => x[1]);
        }

        public string GetSecret(string name)
        {
            return _secrets[name];
        }

        public string PopulateSecrets(string value)
        {
            var index = 0;
            Match match;
            do
            {
                match = _placeholder.Match(value, index);
                if (match.Success)
                {
                    var secretName = match.Groups[1].Value;
                    var secretValue = GetSecret(secretName);
                    value = value.Replace(match.Value, secretValue, StringComparison.InvariantCulture);
                    index = match.Index + 1;
                }
            } while (match.Success);

            return value;
        }
    }
}