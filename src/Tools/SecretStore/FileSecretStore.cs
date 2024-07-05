namespace SecretStore;

public class FileSecretStore : ISecretStore
{
    private readonly IDictionary<string, string> _secrets;

    public FileSecretStore(string path)
    {
        if (!File.Exists(path))
        {
            _secrets = new Dictionary<string, string>();
            return;
        }

        var lines = File.ReadAllLines(path);
        _secrets = lines
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x!.TrimStart().StartsWith('#')) // skip empty lines or starting with a comment #
            .Select(x => x.Split('=', 2).Select(i => i.Trim()).ToArray())
            .GroupBy(x => x[0], x => x.Length == 2 ? x[1] : string.Empty)
            .ToDictionary(x => x.Key, x => x.LastOrDefault()); // take the last value for the key
    }

    public string GetSecret(string name)
    {
        _secrets.TryGetValue(name, out var value);
        return value;
    }
}