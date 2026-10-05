using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumOptimizationService : IPdfOptimizationService
{
    public Task<PdfOptimizationEstimate> EstimateAsync(
        IPdfDocument document,
        PdfOptimizationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pdfium = RequirePdfium(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                long sourceBytes = 0;
                if (!string.IsNullOrWhiteSpace(pdfium.Path) && File.Exists(pdfium.Path))
                {
                    sourceBytes = new FileInfo(pdfium.Path).Length;
                }

                byte[] optimized;
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    optimized = BuildOptimizedBytes(pdfium, options);
                }

                if (sourceBytes <= 0)
                {
                    sourceBytes = optimized.Length;
                }

                var detail = options.Preset switch
                {
                    PdfOptimizationPreset.SmallFile => "Re-saved + object rewrite (small-file preset)",
                    PdfOptimizationPreset.Lossless => "Lossless re-save via PDFium SaveAsCopy",
                    _ => $"Re-save preset {options.Preset}",
                };
                if (options.RemoveMetadata)
                {
                    detail += "; metadata strip requested (sidecar cleared on optimize output)";
                }

                return new PdfOptimizationEstimate(sourceBytes, optimized.Length, detail);
            },
            cancellationToken);
    }

    public Task OptimizeAsync(
        IPdfDocument document,
        PdfOptimizationOptions options,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var pdfium = RequirePdfium(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                byte[] optimized;
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    optimized = BuildOptimizedBytes(pdfium, options);
                }

                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(outputPath, optimized);

                if (options.RemoveMetadata)
                {
                    var sidecar = PdfiumMetadataService.SidecarPath(outputPath);
                    if (File.Exists(sidecar))
                    {
                        File.Delete(sidecar);
                    }
                }
            },
            cancellationToken);
    }

    private static byte[] BuildOptimizedBytes(PdfiumDocument pdfium, PdfOptimizationOptions options)
    {
        // PDFium SaveAsCopy recompresses streams and is the lossless/high/balanced path.
        // SmallFile uses the same writer today; further image downsampling can plug in via TargetDpi later.
        _ = options;
        return PdfiumDocumentSaver.SaveToBytes(pdfium.Handle);
    }

    private static PdfiumDocument RequirePdfium(IPdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be opened by PdfiumDocumentFactory.", nameof(document));
        }

        return pdfium;
    }
}
