namespace Glyph.Infrastructure.Forms;

public interface IFormAutofillProfileStore
{
    FormAutofillProfile Current { get; }

    Task SaveAsync(FormAutofillProfile profile, CancellationToken cancellationToken = default);
}
