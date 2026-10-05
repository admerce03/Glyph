namespace Glyph.Core.Documents;

/// <summary>
/// Deferred GPU acceleration path (F57-07).
/// </summary>
public static class GpuAccelerationPolicy
{
    public const bool Win2dCompositionPathAdopted = false;

    public const string DeferredReason =
        "Win2D/Composition GPU path not adopted yet; page/image bitmaps render on CPU.";
}
