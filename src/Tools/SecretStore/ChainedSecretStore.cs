namespace SecretStore
{
    using System.Collections.Generic;
    using System.Linq;

    public class ChainedSecretStore : ISecretStore
    {
        private readonly IList<ISecretStore> _list;

        public ChainedSecretStore(params ISecretStore[] list)
        {
            _list = list.ToList();
        }

        #region Implementation of ISecretStore

        public string GetSecret(string name)
        {
            foreach (var secretStore in _list)
            {
                var secret = secretStore.GetSecret(name);
                if (secret != null)
                {
                    return secret;
                }
            }
            return null;
        }

        #endregion
    }
}