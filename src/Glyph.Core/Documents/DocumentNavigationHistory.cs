namespace Glyph.Core.Documents;

/// <summary>
/// Simple back/forward stack for in-document page navigation (TOC, links, Find jumps).
/// </summary>
public sealed class DocumentNavigationHistory
{
    private readonly List<int> _back = [];
    private readonly List<int> _forward = [];
    private int? _current;

    public bool CanGoBack => _back.Count > 0;

    public bool CanGoForward => _forward.Count > 0;

    public int? Current => _current;

    public void NavigateTo(int pageIndex)
    {
        if (_current is int current && current == pageIndex)
        {
            return;
        }

        if (_current is int previous)
        {
            _back.Add(previous);
        }

        _current = pageIndex;
        _forward.Clear();
    }

    public int? GoBack()
    {
        if (!CanGoBack || _current is null)
        {
            return null;
        }

        _forward.Add(_current.Value);
        var index = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        _current = index;
        return index;
    }

    public int? GoForward()
    {
        if (!CanGoForward)
        {
            return null;
        }

        if (_current is int current)
        {
            _back.Add(current);
        }

        var index = _forward[^1];
        _forward.RemoveAt(_forward.Count - 1);
        _current = index;
        return index;
    }
}
