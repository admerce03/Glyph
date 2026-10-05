using System.Text.Json;

namespace Glyph.Infrastructure.Forms;

public sealed class JsonFormAutofillProfileStore : IFormAutofillProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FormAutofillProfile? _cache;

    public JsonFormAutofillProfileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public FormAutofillProfile Current
    {
        get
        {
            _gate.Wait();
            try
            {
                return LoadUnlocked();
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public async Task SaveAsync(FormAutofillProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache = profile;
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(profile, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private FormAutofillProfile LoadUnlocked()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_filePath))
        {
            _cache = new FormAutofillProfile();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _cache = JsonSerializer.Deserialize<FormAutofillProfile>(json, JsonOptions)
                ?? new FormAutofillProfile();
        }
        catch (JsonException)
        {
            _cache = new FormAutofillProfile();
        }

        return _cache;
    }
}
