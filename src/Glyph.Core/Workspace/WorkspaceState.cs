using Glyph.Core.Documents;

namespace Glyph.Core.Workspace;

/// <summary>
/// Tracks open document sessions for a window/workspace.
/// </summary>
public sealed class WorkspaceState
{
    private readonly List<DocumentSession> _documents = [];

    public IReadOnlyList<DocumentSession> Documents => _documents;

    public DocumentSession? ActiveDocument { get; private set; }

    public DocumentSession Open(DocumentKind kind, string displayName, string? path = null)
    {
        var session = new DocumentSession(kind, displayName, path);
        _documents.Add(session);
        ActiveDocument = session;
        return session;
    }

    public bool Activate(DocumentId id)
    {
        var match = _documents.FirstOrDefault(d => d.Id.Equals(id));
        if (match is null)
        {
            return false;
        }

        ActiveDocument = match;
        return true;
    }

    public bool Close(DocumentId id)
    {
        var index = _documents.FindIndex(d => d.Id.Equals(id));
        if (index < 0)
        {
            return false;
        }

        var closing = _documents[index];
        _documents.RemoveAt(index);

        if (ActiveDocument?.Id.Equals(closing.Id) == true)
        {
            ActiveDocument = _documents.Count == 0
                ? null
                : _documents[Math.Clamp(index, 0, _documents.Count - 1)];
        }

        return true;
    }
}
