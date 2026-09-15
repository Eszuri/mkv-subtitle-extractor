namespace MksStudio.Core.Subtitles;

/// <summary>
/// High-performance undo/redo manager tailored for subtitle text editing.
/// Supports coalescing consecutive keystrokes, capturing discrete format actions (Bold, Color, etc.),
/// and preserving caret / selection positions without requiring extra UI buttons.
/// </summary>
public sealed class SubtitleTextUndoManager
{
    public readonly struct TextState
    {
        public string Text { get; }
        public int SelectionStart { get; }
        public int SelectionLength { get; }

        public TextState(string text, int selStart, int selLen)
        {
            Text = text ?? string.Empty;
            SelectionStart = Math.Max(0, selStart);
            SelectionLength = Math.Max(0, selLen);
        }
    }

    private readonly Stack<TextState> _undoStack = new();
    private readonly Stack<TextState> _redoStack = new();
    private string? _lastTrackedText;
    private DateTime _lastTrackedTime = DateTime.MinValue;
    private bool _isExecutingUndoRedo;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Reset(string initialText, int caret = 0)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _lastTrackedText = initialText ?? string.Empty;
        _lastTrackedTime = DateTime.UtcNow;
    }

    public void RecordChange(string currentText, int selStart, int selLen, bool forceNewStep = false)
    {
        if (_isExecutingUndoRedo) return;
        currentText ??= string.Empty;

        // Nothing changed
        if (_lastTrackedText != null && currentText == _lastTrackedText) return;

        var now = DateTime.UtcNow;
        // Checkpoint rule: force discrete step (e.g. preset action, paste) OR typing pause > 750ms OR stack empty
        if (forceNewStep || (now - _lastTrackedTime).TotalMilliseconds > 750 || _undoStack.Count == 0)
        {
            _undoStack.Push(new TextState(_lastTrackedText ?? string.Empty, selStart, selLen));
            _redoStack.Clear();
        }

        _lastTrackedText = currentText;
        _lastTrackedTime = now;
    }

    public TextState? Undo(string currentText, int currentSelStart, int currentSelLen)
    {
        if (_undoStack.Count == 0) return null;

        _isExecutingUndoRedo = true;
        try
        {
            _redoStack.Push(new TextState(currentText ?? string.Empty, currentSelStart, currentSelLen));
            var prev = _undoStack.Pop();
            _lastTrackedText = prev.Text;
            _lastTrackedTime = DateTime.UtcNow;
            return prev;
        }
        finally
        {
            _isExecutingUndoRedo = false;
        }
    }

    public TextState? Redo(string currentText, int currentSelStart, int currentSelLen)
    {
        if (_redoStack.Count == 0) return null;

        _isExecutingUndoRedo = true;
        try
        {
            _undoStack.Push(new TextState(currentText ?? string.Empty, currentSelStart, currentSelLen));
            var next = _redoStack.Pop();
            _lastTrackedText = next.Text;
            _lastTrackedTime = DateTime.UtcNow;
            return next;
        }
        finally
        {
            _isExecutingUndoRedo = false;
        }
    }
}
