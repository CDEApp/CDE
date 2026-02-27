// getline.cs: A command line editor
//
// Authors:
//   Miguel de Icaza (miguel@novell.com)
//
// Copyright 2008 Novell, Inc.
// Copyright 2016 Xamarin Inc
//
// Completion wanted:
//
//   * Enable bash-like completion window the window as an option for non-GUI people?
//
//   * Continue completing when Backspace is used?
//
//   * Should we keep the auto-complete on "."?
//
//   * Completion produces an error if the value is not resolvable, we should hide those errors
//
// Dual-licensed under the terms of the MIT X11 license or the
// Apache License 2.0
//
// USE -define:DEMO to build this as a standalone file and test it
//
// TODO:
//    Enter an error (a = 1);  Notice how the prompt is in the wrong line
//		This is caused by Stderr not being tracked by System.Console.
//    Completion support
//    Why is Thread.Interrupt not working?   Currently I resort to Abort which is too much.
//
// Limitations in System.Console:
//    Console needs SIGWINCH support of some sort
//    Console needs a way of updating its position after things have been written
//    behind its back (P/Invoke puts for example).
//    System.Console needs to get the DELETE character, and report accordingly.
//
// Bug:
//   About 8 lines missing, type "Con<TAB>" and not enough lines are inserted at the bottom.

using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Reflection;

namespace Mono.Terminal;

public class LineEditor
{
    public class Completion
    {
        public string[] Result;
        public string Prefix;

        public Completion(string prefix, string[] result)
        {
            Prefix = prefix;
            Result = result;
        }
    }

    public delegate Completion AutoCompleteHandler(string text, int pos);

    // null does nothing, "csharp" uses some heuristics that make sense for C#
    public string HeuristicsMode;

    //static StreamWriter log;

    // The text being edited.
    private StringBuilder _text;

    // The text as it is rendered (replaces (char)1 with ^A on display for example).
    private readonly StringBuilder _renderedText;

    // The prompt specified, and the prompt shown to the user.
    private string _prompt;
    private string _shownPrompt;

    // The current cursor position, indexes into "text", for an index
    // into rendered_text, use TextToRenderPos
    private int _cursor;

    // The row where we started displaying data.
    private int _homeRow;

    // The maximum length that has been displayed on the screen
    private int _maxRendered;

    // If we are done editing, this breaks the interactive loop
    private bool _done;

    // The thread where the Editing started taking place
    private Thread _editThread;

    // Cancellation token source for interrupting the edit loop
    private CancellationTokenSource _cancellationTokenSource;

    // Our object that tracks history
    private readonly History _history;

    // The contents of the kill buffer (cut/paste in Emacs parlance)
    private string _killBuffer = "";

    // The string being searched for
    private string _search;
    private string _lastSearch;

    // whether we are searching (-1= reverse; 0 = no; 1 = forward)
    private int _searching;

    // The position where we found the match.
    private int _matchAt;

    // Used to implement the Kill semantics (multiple Alt-Ds accumulate)
    private KeyHandler _lastHandler;

    // If we have a popup completion, this is not null and holds the state.
    private CompletionState _currentCompletion;

    // If this is set, it contains an escape sequence to reset the Unix colors to the ones that were used on startup
    private static byte[] _unixResetColors;

    // This contains a raw stream pointing to stdout, used to bypass the TermInfoDriver
    private static Stream _unixRawOutput;

    delegate void KeyHandler();

    struct Handler
    {
        public readonly ConsoleKeyInfo Cki;
        public readonly KeyHandler KeyHandler;
        public bool ResetCompletion;

        public Handler(ConsoleKey key, KeyHandler h, bool resetCompletion = true)
        {
            Cki = new ConsoleKeyInfo((char)0, key, false, false, false);
            KeyHandler = h;
            ResetCompletion = resetCompletion;
        }

        private Handler(char c, KeyHandler h, bool resetCompletion = true)
        {
            KeyHandler = h;
            // Use the "Zoom" as a flag that we only have a character.
            Cki = new ConsoleKeyInfo(c, ConsoleKey.Zoom, false, false, false);
            ResetCompletion = resetCompletion;
        }

        public Handler(ConsoleKeyInfo cki, KeyHandler h, bool resetCompletion = true)
        {
            Cki = cki;
            KeyHandler = h;
            ResetCompletion = resetCompletion;
        }

        public static Handler Control(char c, KeyHandler h, bool resetCompletion = true)
        {
            return new Handler((char)(c - 'A' + 1), h, resetCompletion);
        }

        public static Handler Alt(char c, ConsoleKey k, KeyHandler h)
        {
            var cki = new ConsoleKeyInfo(c, k, false, true, false);
            return new Handler(cki, h);
        }
    }

    /// <summary>
    ///   Invoked when the user requests auto-completion using the tab character
    /// </summary>
    /// <remarks>
    ///    The result is null for no values found, an array with a single
    ///    string, in that case the string should be the text to be inserted
    ///    for example if the word at pos is "T", the result for a completion
    ///    of "ToString" should be "oString", not "ToString".
    ///
    ///    When there are multiple results, the result should be the full
    ///    text
    /// </remarks>
    public AutoCompleteHandler AutoCompleteEvent;

    private readonly Handler[] _handlers;

    public LineEditor(string name) : this(name, 10)
    {
    }

    public LineEditor(string name, int histsize)
    {
        _handlers =
        [
            new Handler(ConsoleKey.Home, CmdHome),
            new Handler(ConsoleKey.End, CmdEnd),
            new Handler(ConsoleKey.LeftArrow, CmdLeft),
            new Handler(ConsoleKey.RightArrow, CmdRight),
            new Handler(ConsoleKey.UpArrow, CmdUp, resetCompletion: false),
            new Handler(ConsoleKey.DownArrow, CmdDown, resetCompletion: false),
            new Handler(ConsoleKey.Enter, CmdDone, resetCompletion: false),
            new Handler(ConsoleKey.Backspace, CmdBackspace, resetCompletion: false),
            new Handler(ConsoleKey.Delete, CmdDeleteChar),
            new Handler(ConsoleKey.Tab, CmdTabOrComplete, resetCompletion: false),

            // Emacs keys
            Handler.Control('A', CmdHome),
            Handler.Control('E', CmdEnd),
            Handler.Control('B', CmdLeft),
            Handler.Control('F', CmdRight),
            Handler.Control('P', CmdUp, resetCompletion: false),
            Handler.Control('N', CmdDown, resetCompletion: false),
            Handler.Control('K', CmdKillToEOF),
            Handler.Control('Y', CmdYank),
            Handler.Control('D', CmdDeleteChar),
            Handler.Control('L', CmdRefresh),
            Handler.Control('R', CmdReverseSearch),
            Handler.Control('G', delegate { }),
            Handler.Alt('B', ConsoleKey.B, CmdBackwardWord),
            Handler.Alt('F', ConsoleKey.F, CmdForwardWord),

            Handler.Alt('D', ConsoleKey.D, CmdDeleteWord),
            Handler.Alt((char)8, ConsoleKey.Backspace, CmdDeleteBackword),

            // DEBUG
            //Handler.Control ('T', CmdDebug),

            // quote
            Handler.Control('Q', delegate { HandleChar(Console.ReadKey(true).KeyChar); })
        ];

        _renderedText = new StringBuilder();
        _text = new StringBuilder();

        _history = new History(name, histsize);

        GetUnixConsoleReset();
    }

    // On Unix, there is a "default" color which is not represented by any colors in
    // ConsoleColor. It is not possible to set is by setting the ForegroundColor or
    // BackgroundColor properties, so we have to use the terminfo driver in Mono to
    // fetch these values

    void GetUnixConsoleReset()
    {
        //
        // On Unix, we want to be able to reset the color for the pop-up completion
        //
        var p = (int)Environment.OSVersion.Platform;
        var isUnix = p is 4 or 128;
        if (!isUnix)
            return;

        // Sole purpose of this call is to initialize the Terminfo driver
        _ = Console.CursorLeft;

        try
        {
            var terminfoDriver = Type.GetType("System.ConsoleDriver")
                ?.GetField("driver", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            if (terminfoDriver == null)
                return;

            if (terminfoDriver.GetType()
                    .GetField("origPair", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(terminfoDriver) is string unixResetColorsStr)
                _unixResetColors = Encoding.UTF8.GetBytes(unixResetColorsStr);
            _unixRawOutput = Console.OpenStandardOutput();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e);
        }
    }

    private void CmdDebug()
    {
        _history.Dump();
        Console.WriteLine();
        Render();
    }

    void Render()
    {
        Console.Write(_shownPrompt);
        Console.Write(_renderedText);

        var max = Math.Max(_renderedText.Length + _shownPrompt.Length, _maxRendered);

        for (var i = _renderedText.Length + _shownPrompt.Length; i < _maxRendered; i++)
            Console.Write(' ');
        _maxRendered = _shownPrompt.Length + _renderedText.Length;

        // Write one more to ensure that we always wrap around properly if we are at the
        // end of a line.
        Console.Write(' ');

        UpdateHomeRow(max);
    }

    private void UpdateHomeRow(int screenpos)
    {
        var lines = 1 + (screenpos / Console.WindowWidth);

        _homeRow = Console.CursorTop - (lines - 1);
        if (_homeRow < 0)
            _homeRow = 0;
    }


    void RenderFrom(int pos)
    {
        var rpos = TextToRenderPos(pos);
        int i;

        for (i = rpos; i < _renderedText.Length; i++)
            Console.Write(_renderedText[i]);

        if ((_shownPrompt.Length + _renderedText.Length) > _maxRendered)
            _maxRendered = _shownPrompt.Length + _renderedText.Length;
        else
        {
            var maxExtra = _maxRendered - _shownPrompt.Length;
            for (; i < maxExtra; i++)
                Console.Write(' ');
        }
    }

    void ComputeRendered()
    {
        _renderedText.Length = 0;

        for (var i = 0; i < _text.Length; i++)
        {
            int c = _text[i];
            if (c < 26)
            {
                if (c == '\t')
                    _renderedText.Append("    ");
                else
                {
                    _renderedText.Append('^');
                    _renderedText.Append((char)(c + 'A' - 1));
                }
            }
            else
                _renderedText.Append((char)c);
        }
    }

    int TextToRenderPos(int pos)
    {
        var p = 0;

        for (var i = 0; i < pos; i++)
        {
            var c = (int)_text[i];

            if (c < 26)
            {
                if (c == 9)
                    p += 4;
                else
                    p += 2;
            }
            else
                p++;
        }

        return p;
    }

    private int TextToScreenPos(int pos)
    {
        return _shownPrompt.Length + TextToRenderPos(pos);
    }

    string Prompt
    {
        get => _prompt;
        set => _prompt = value;
    }

    private int LineCount
    {
        get { return (_shownPrompt.Length + _renderedText.Length) / Console.WindowWidth; }
    }

    void ForceCursor(int newpos)
    {
        _cursor = newpos;

        var actualPos = _shownPrompt.Length + TextToRenderPos(_cursor);
        var row = _homeRow + (actualPos / Console.WindowWidth);
        var col = actualPos % Console.WindowWidth;

        if (row >= Console.BufferHeight)
            row = Console.BufferHeight - 1;
        Console.SetCursorPosition(col, row);

        //log.WriteLine ("Going to cursor={0} row={1} col={2} actual={3} prompt={4} ttr={5} old={6}", newpos, row, col, actual_pos, prompt.Length, TextToRenderPos (cursor), cursor);
        //log.Flush ();
    }

    void UpdateCursor(int newpos)
    {
        if (_cursor == newpos)
            return;

        ForceCursor(newpos);
    }

    void InsertChar(char c)
    {
        var prevLines = LineCount;
        _text = _text.Insert(_cursor, c);
        ComputeRendered();
        if (prevLines != LineCount)
        {
            Console.SetCursorPosition(0, _homeRow);
            Render();
            ForceCursor(++_cursor);
        }
        else
        {
            RenderFrom(_cursor);
            ForceCursor(++_cursor);
            UpdateHomeRow(TextToScreenPos(_cursor));
        }
    }

    static void SaveExcursion(Action code)
    {
        var savedCol = Console.CursorLeft;
        var savedRow = Console.CursorTop;
        var savedFore = Console.ForegroundColor;
        var savedBack = Console.BackgroundColor;

        code();

        Console.CursorLeft = savedCol;
        Console.CursorTop = savedRow;
        if (_unixResetColors != null)
        {
            _unixRawOutput.Write(_unixResetColors, 0, _unixResetColors.Length);
        }
        else
        {
            Console.ForegroundColor = savedFore;
            Console.BackgroundColor = savedBack;
        }
    }

    class CompletionState
    {
        public string Prefix;
        public string[] Completions;
        public int Col, Row, Width, Height;
        private int _selectedItem, _topItem;

        public CompletionState(int col, int row, int width, int height)
        {
            Col = col;
            Row = row;
            Width = width;
            Height = height;

            if (Col < 0)
                throw new ArgumentException("Cannot be less than zero" + Col, "Col");
            if (Row < 0)
                throw new ArgumentException("Cannot be less than zero", "Row");
            if (Width < 1)
                throw new ArgumentException("Cannot be less than one", "Width");
            if (Height < 1)
                throw new ArgumentException("Cannot be less than one", "Height");
        }

        void DrawSelection()
        {
            for (var r = 0; r < Height; r++)
            {
                var itemIdx = _topItem + r;
                var selected = (itemIdx == _selectedItem);

                Console.ForegroundColor = selected ? ConsoleColor.Black : ConsoleColor.Gray;
                Console.BackgroundColor = selected ? ConsoleColor.Cyan : ConsoleColor.Blue;

                var item = Prefix + Completions[itemIdx];
                if (item.Length > Width)
                    item = item.Substring(0, Width);

                Console.CursorLeft = Col;
                Console.CursorTop = Row + r;
                Console.Write(item);
                for (var space = item.Length; space <= Width; space++)
                    Console.Write(" ");
            }
        }

        public string Current => Completions[_selectedItem];

        public void Show()
        {
            SaveExcursion(DrawSelection);
        }

        public void SelectNext()
        {
            if (_selectedItem + 1 < Completions.Length)
            {
                _selectedItem++;
                if (_selectedItem - _topItem >= Height)
                    _topItem++;
                SaveExcursion(DrawSelection);
            }
        }

        public void SelectPrevious()
        {
            if (_selectedItem > 0)
            {
                _selectedItem--;
                if (_selectedItem < _topItem)
                    _topItem = _selectedItem;
                SaveExcursion(DrawSelection);
            }
        }

        void Clear()
        {
            for (var r = 0; r < Height; r++)
            {
                Console.CursorLeft = Col;
                Console.CursorTop = Row + r;
                for (var space = 0; space <= Width; space++)
                    Console.Write(" ");
            }
        }

        public void Remove()
        {
            SaveExcursion(Clear);
        }
    }

    void ShowCompletions(string prefix, string[] completions)
    {
        // Ensure we have space, determine window size
        var windowHeight = Math.Max(1, Math.Min(completions.Length, Console.WindowHeight / 5));
        var targetLine = Console.WindowHeight - windowHeight - 1;
        if (Console.CursorTop > targetLine)
        {
            var delta = Console.CursorTop - targetLine;
            Console.CursorLeft = 0;
            Console.CursorTop = Console.WindowHeight - 1;
            for (var i = 0; i < delta + 1; i++)
            {
                for (var c = Console.WindowWidth; c > 0; c--)
                    Console.Write(" "); // To debug use ("{0}", i%10);
            }

            Console.CursorTop = targetLine;
            Console.CursorLeft = 0;
            Render();
        }

        const int maxWidth = 50;
        var windowWidth = 12;
        var plen = prefix.Length;
        foreach (var s in completions)
            windowWidth = Math.Max(plen + s.Length, windowWidth);
        windowWidth = Math.Min(windowWidth, maxWidth);

        if (_currentCompletion == null)
        {
            var left = Console.CursorLeft - prefix.Length;

            if (left + windowWidth + 1 >= Console.WindowWidth)
                left = Console.WindowWidth - windowWidth - 1;

            _currentCompletion = new CompletionState(left, Console.CursorTop + 1, windowWidth, windowHeight)
            {
                Prefix = prefix,
                Completions = completions,
            };
        }
        else
        {
            _currentCompletion.Prefix = prefix;
            _currentCompletion.Completions = completions;
        }

        _currentCompletion.Show();
        Console.CursorLeft = 0;
    }

    public void HideCompletions()
    {
        if (_currentCompletion == null)
            return;
        _currentCompletion.Remove();
        _currentCompletion = null;
    }

    //
    // Triggers the completion engine, if insertBestMatch is true, then this will
    // insert the best match found, this behaves like the shell "tab" which will
    // complete as much as possible given the options.
    //
    void Complete()
    {
        if (AutoCompleteEvent == null)
            return;
        var completion = AutoCompleteEvent(_text.ToString(), _cursor);
        var completions = completion?.Result;
        if (completions == null)
        {
            HideCompletions();
            return;
        }

        var ncompletions = completions.Length;
        if (ncompletions == 0)
        {
            HideCompletions();
            return;
        }

        if (completions.Length == 1)
        {
            InsertTextAtCursor(completions[0]);
            HideCompletions();
        }
        else
        {
            var last = -1;

            for (var p = 0; p < completions[0].Length; p++)
            {
                var c = completions[0][p];


                for (var i = 1; i < ncompletions; i++)
                {
                    if (completions[i].Length <= p)
                        goto mismatch;

                    if (completions[i][p] != c)
                    {
                        goto mismatch;
                    }
                }

                last = p;
            }

            mismatch:
            var prefix = completion.Prefix;
            if (last != -1)
            {
                InsertTextAtCursor(completions[0].Substring(0, last + 1));

                // Adjust the completions to skip the common prefix
                prefix += completions[0].Substring(0, last + 1);
                for (var i = 0; i < completions.Length; i++)
                    completions[i] = completions[i].Substring(last + 1);
            }

            ShowCompletions(prefix, completions);
            Render();
            ForceCursor(_cursor);
        }
    }

    //
    // When the user has triggered a completion window, this will try to update
    // the contents of it.   The completion window is assumed to be hidden at this
    // point
    // 
    void UpdateCompletionWindow()
    {
        if (_currentCompletion != null)
            throw new Exception("This method should only be called if the window has been hidden");
        if (AutoCompleteEvent == null)
            return;

        var completion = AutoCompleteEvent(_text.ToString(), _cursor);
        var completions = completion?.Result;
        if (completions == null)
            return;

        var ncompletions = completions.Length;
        if (ncompletions == 0)
            return;

        ShowCompletions(completion.Prefix, completion.Result);
        Render();
        ForceCursor(_cursor);
    }


    //
    // Commands
    //
    void CmdDone()
    {
        if (_currentCompletion != null)
        {
            InsertTextAtCursor(_currentCompletion.Current);
            HideCompletions();
            return;
        }

        _done = true;
    }

    void CmdTabOrComplete()
    {
        var complete = false;

        if (AutoCompleteEvent != null)
        {
            if (TabAtStartCompletes)
                complete = true;
            else
            {
                for (var i = 0; i < _cursor; i++)
                {
                    if (!Char.IsWhiteSpace(_text[i]))
                    {
                        complete = true;
                        break;
                    }
                }
            }

            if (complete)
                Complete();
            else
                HandleChar('\t');
        }
        else
            HandleChar('\t');
    }

    public void CmdHistoryDump()
    {
        _history.Dump();
    }

    void CmdHome()
    {
        UpdateCursor(0);
    }

    void CmdEnd()
    {
        UpdateCursor(_text.Length);
    }

    void CmdLeft()
    {
        if (_cursor == 0)
            return;

        UpdateCursor(_cursor - 1);
    }

    void CmdBackwardWord()
    {
        var p = WordBackward(_cursor);
        if (p == -1)
            return;
        UpdateCursor(p);
    }

    void CmdForwardWord()
    {
        var p = WordForward(_cursor);
        if (p == -1)
            return;
        UpdateCursor(p);
    }

    void CmdRight()
    {
        if (_cursor == _text.Length)
            return;

        UpdateCursor(_cursor + 1);
    }

    void RenderAfter(int p)
    {
        ForceCursor(p);
        RenderFrom(p);
        ForceCursor(_cursor);
    }

    void CmdBackspace()
    {
        if (_cursor == 0)
            return;

        var completing = _currentCompletion != null;
        HideCompletions();

        _text.Remove(--_cursor, 1);
        ComputeRendered();
        RenderAfter(_cursor);
        if (completing)
            UpdateCompletionWindow();
    }

    void CmdDeleteChar()
    {
        // If there is no input, this behaves like EOF
        if (_text.Length == 0)
        {
            _done = true;
            _text = null;
            Console.WriteLine();
            return;
        }

        if (_cursor == _text.Length)
            return;
        _text.Remove(_cursor, 1);
        ComputeRendered();
        RenderAfter(_cursor);
    }

    int WordForward(int p)
    {
        if (p >= _text.Length)
            return -1;

        var i = p;
        if (Char.IsPunctuation(_text[p]) || Char.IsSymbol(_text[p]) || Char.IsWhiteSpace(_text[p]))
        {
            for (; i < _text.Length; i++)
            {
                if (Char.IsLetterOrDigit(_text[i]))
                    break;
            }

            for (; i < _text.Length; i++)
            {
                if (!Char.IsLetterOrDigit(_text[i]))
                    break;
            }
        }
        else
        {
            for (; i < _text.Length; i++)
            {
                if (!Char.IsLetterOrDigit(_text[i]))
                    break;
            }
        }

        if (i != p)
            return i;
        return -1;
    }

    int WordBackward(int p)
    {
        if (p == 0)
            return -1;

        var i = p - 1;
        if (i == 0)
            return 0;

        if (Char.IsPunctuation(_text[i]) || Char.IsSymbol(_text[i]) || Char.IsWhiteSpace(_text[i]))
        {
            for (; i >= 0; i--)
            {
                if (Char.IsLetterOrDigit(_text[i]))
                    break;
            }

            for (; i >= 0; i--)
            {
                if (!Char.IsLetterOrDigit(_text[i]))
                    break;
            }
        }
        else
        {
            for (; i >= 0; i--)
            {
                if (!Char.IsLetterOrDigit(_text[i]))
                    break;
            }
        }

        i++;

        if (i != p)
            return i;

        return -1;
    }

    void CmdDeleteWord()
    {
        var pos = WordForward(_cursor);

        if (pos == -1)
            return;

        var k = _text.ToString(_cursor, pos - _cursor);

        if (_lastHandler == CmdDeleteWord)
            _killBuffer = _killBuffer + k;
        else
            _killBuffer = k;

        _text.Remove(_cursor, pos - _cursor);
        ComputeRendered();
        RenderAfter(_cursor);
    }

    void CmdDeleteBackword()
    {
        var pos = WordBackward(_cursor);
        if (pos == -1)
            return;

        var k = _text.ToString(pos, _cursor - pos);

        if (_lastHandler == CmdDeleteBackword)
            _killBuffer = k + _killBuffer;
        else
            _killBuffer = k;

        _text.Remove(pos, _cursor - pos);
        ComputeRendered();
        RenderAfter(pos);
    }

    //
    // Adds the current line to the history if needed
    //
    void HistoryUpdateLine()
    {
        _history.Update(_text.ToString());
    }

    void CmdHistoryPrev()
    {
        if (!_history.PreviousAvailable())
            return;

        HistoryUpdateLine();

        SetText(_history.Previous());
    }

    void CmdHistoryNext()
    {
        if (!_history.NextAvailable())
            return;

        _history.Update(_text.ToString());
        SetText(_history.Next());
    }

    void CmdUp()
    {
        if (_currentCompletion == null)
            CmdHistoryPrev();
        else
            _currentCompletion.SelectPrevious();
    }

    void CmdDown()
    {
        if (_currentCompletion == null)
            CmdHistoryNext();
        else
            _currentCompletion.SelectNext();
    }

    void CmdKillToEOF()
    {
        _killBuffer = _text.ToString(_cursor, _text.Length - _cursor);
        _text.Length = _cursor;
        ComputeRendered();
        RenderAfter(_cursor);
    }

    void CmdYank()
    {
        InsertTextAtCursor(_killBuffer);
    }

    void InsertTextAtCursor(string str)
    {
        var prevLines = LineCount;
        _text.Insert(_cursor, str);
        ComputeRendered();
        if (prevLines != LineCount)
        {
            Console.SetCursorPosition(0, _homeRow);
            Render();
            _cursor += str.Length;
            ForceCursor(_cursor);
        }
        else
        {
            RenderFrom(_cursor);
            _cursor += str.Length;
            ForceCursor(_cursor);
            UpdateHomeRow(TextToScreenPos(_cursor));
        }
    }

    void SetSearchPrompt(string s)
    {
        SetPrompt("(reverse-i-search)`" + s + "': ");
    }

    void ReverseSearch()
    {
        int p;

        if (_cursor == _text.Length)
        {
            // The cursor is at the end of the string

            p = _text.ToString().LastIndexOf(_search, StringComparison.Ordinal);
            if (p != -1)
            {
                _matchAt = p;
                _cursor = p;
                ForceCursor(_cursor);
                return;
            }
        }
        else
        {
            // The cursor is somewhere in the middle of the string
            var start = (_cursor == _matchAt) ? _cursor - 1 : _cursor;
            if (start != -1)
            {
                p = _text.ToString().LastIndexOf(_search, start, StringComparison.Ordinal);
                if (p != -1)
                {
                    _matchAt = p;
                    _cursor = p;
                    ForceCursor(_cursor);
                    return;
                }
            }
        }

        // Need to search backwards in history
        HistoryUpdateLine();
        var s = _history.SearchBackward(_search);
        if (s != null)
        {
            _matchAt = -1;
            SetText(s);
            ReverseSearch();
        }
    }

    void CmdReverseSearch()
    {
        if (_searching == 0)
        {
            _matchAt = -1;
            _lastSearch = _search;
            _searching = -1;
            _search = "";
            SetSearchPrompt("");
        }
        else
        {
            if (_search == "")
            {
                if (!string.IsNullOrEmpty(_lastSearch))
                {
                    _search = _lastSearch;
                    SetSearchPrompt(_search);

                    ReverseSearch();
                }

                return;
            }

            ReverseSearch();
        }
    }

    void SearchAppend(char c)
    {
        _search = _search + c;
        SetSearchPrompt(_search);

        //
        // If the new typed data still matches the current text, stay here
        //
        if (_cursor < _text.Length)
        {
            var r = _text.ToString(_cursor, _text.Length - _cursor);
            if (r.StartsWith(_search))
                return;
        }

        ReverseSearch();
    }

    void CmdRefresh()
    {
        Console.Clear();
        _maxRendered = 0;
        Render();
        ForceCursor(_cursor);
    }

    void InterruptEdit(object sender, ConsoleCancelEventArgs a)
    {
        // Do not abort our program:
        a.Cancel = true;

        // Cancel the edit loop via cancellation token
        _cancellationTokenSource?.Cancel();
    }

    // Implements heuristics to show the completion window based on the mode
    bool HeuristicAutoComplete(bool wasCompleting, char insertedChar)
    {
        if (HeuristicsMode == "csharp")
        {
            // csharp heuristics
            if (wasCompleting)
            {
                return insertedChar != ' ';
            }

            // If we were not completing, determine if we want to now
            if (insertedChar == '.')
            {
                // Avoid completing for numbers "1.2" for example
                if (_cursor > 1 && char.IsDigit(_text[_cursor - 2]))
                {
                    for (var p = _cursor - 3; p >= 0; p--)
                    {
                        var c = _text[p];
                        if (Char.IsDigit(c))
                            continue;
                        if (c == '_')
                            return true;
                        if (Char.IsLetter(c) || Char.IsPunctuation(c) || Char.IsSymbol(c) || Char.IsControl(c))
                            return true;
                    }

                    return false;
                }

                return true;
            }
        }

        return false;
    }

    void HandleChar(char c)
    {
        if (_searching != 0)
            SearchAppend(c);
        else
        {
            var completing = _currentCompletion != null;
            HideCompletions();

            InsertChar(c);
            if (AutoCompleteEvent != null && HeuristicAutoComplete(completing, c))
                UpdateCompletionWindow();
        }
    }

    private void EditLoop(CancellationToken cancellationToken)
    {
        ConsoleKeyInfo cki;

        while (!_done)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConsoleModifiers mod;

            cki = Console.ReadKey(true);
            if (cki.Key == ConsoleKey.Escape)
            {
                if (_currentCompletion != null)
                {
                    HideCompletions();
                    continue;
                }

                cki = Console.ReadKey(true);
                mod = ConsoleModifiers.Alt;
            }
            else
                mod = cki.Modifiers;

            var handled = false;

            foreach (var handler in _handlers)
            {
                var t = handler.Cki;

                if (t.Key == cki.Key && t.Modifiers == mod)
                {
                    handled = true;
                    if (handler.ResetCompletion)
                        HideCompletions();
                    handler.KeyHandler();
                    _lastHandler = handler.KeyHandler;
                    break;
                }
                else if (t.KeyChar == cki.KeyChar && t.Key == ConsoleKey.Zoom)
                {
                    handled = true;
                    if (handler.ResetCompletion)
                        HideCompletions();

                    handler.KeyHandler();
                    _lastHandler = handler.KeyHandler;
                    break;
                }
            }

            if (handled)
            {
                if (_searching != 0)
                {
                    if (_lastHandler != CmdReverseSearch)
                    {
                        _searching = 0;
                        SetPrompt(_prompt);
                    }
                }

                continue;
            }

            if (cki.KeyChar != (char)0)
            {
                HandleChar(cki.KeyChar);
            }
        }
    }

    void InitText(string initial)
    {
        _text = new StringBuilder(initial);
        ComputeRendered();
        _cursor = _text.Length;
        Render();
        ForceCursor(_cursor);
    }

    void SetText(string newtext)
    {
        Console.SetCursorPosition(0, _homeRow);
        InitText(newtext);
    }

    void SetPrompt(string newprompt)
    {
        _shownPrompt = newprompt;
        Console.SetCursorPosition(0, _homeRow);
        Render();
        ForceCursor(_cursor);
    }

    public string Edit(string userPrompt, string initial)
    {
        _editThread = Thread.CurrentThread;
        _searching = 0;
        Console.CancelKeyPress += InterruptEdit;

        _done = false;
        _history.CursorToEnd();
        _maxRendered = 0;

        Prompt = userPrompt;
        _shownPrompt = userPrompt;
        InitText(initial);
        _history.Append(initial);

        _cancellationTokenSource = new CancellationTokenSource();

        do
        {
            try
            {
                EditLoop(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _cancellationTokenSource = new CancellationTokenSource(); // Reset cancellation token source
                _searching = 0;
                //Thread.ResetAbort();
                Console.WriteLine();
                SetPrompt(userPrompt);
                SetText("");
            }
        } while (!_done);

        Console.WriteLine();

        Console.CancelKeyPress -= InterruptEdit;

        if (_text == null)
        {
            _history.Close();
            return null;
        }

        var result = _text.ToString();
        if (result != "")
            _history.Accept(result);
        else
            _history.RemoveLast();

        return result;
    }

    public void SaveHistory()
    {
        if (_history != null)
        {
            _history.Close();
        }
    }

    public bool TabAtStartCompletes { get; set; }

    // Emulates the bash-like behavior, where edits done to the
    // history are recorded
    class History
    {
        private readonly string[] history;
        private int _head, _tail;
        private int _cursor, _count;
        private readonly string _histfile;

        public History(string app, int size)
        {
            if (size < 1)
                throw new ArgumentException("size");

            if (app != null)
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                //Console.WriteLine (dir);
                if (!Directory.Exists(dir))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch
                    {
                        app = null;
                    }
                }

                if (app != null)
                    _histfile = Path.Combine(dir, app) + ".history";
            }

            history = new string[size];
            _head = _tail = _cursor = 0;

            if (File.Exists(_histfile))
            {
                using var sr = File.OpenText(_histfile);

                while (sr.ReadLine() is { } line)
                {
                    if (line != "")
                        Append(line);
                }
            }
        }

        public void Close()
        {
            if (_histfile == null)
                return;

            try
            {
                using var sw = File.CreateText(_histfile);
                var start = (_count == history.Length) ? _head : _tail;
                for (var i = start; i < start + _count; i++)
                {
                    var p = i % history.Length;
                    sw.WriteLine(history[p]);
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Appends a value to the history
        /// </summary>
        /// <param name="s"></param>
        public void Append(string s)
        {
            //Console.WriteLine ("APPENDING {0} head={1} tail={2}", s, head, tail);
            history[_head] = s;
            _head = (_head + 1) % history.Length;
            if (_head == _tail)
                _tail = (_tail + 1) % history.Length;
            if (_count != history.Length)
                _count++;
            //Console.WriteLine ("DONE: head={1} tail={2}", s, head, tail);
        }

        /// <summary>
        /// Updates the current cursor location with the string,
        /// to support editing of history items.   For the current
        /// line to participate, an Append must be done before.
        /// </summary>
        public void Update(string s)
        {
            history[_cursor] = s;
        }

        public void RemoveLast()
        {
            _head = _head - 1;
            if (_head < 0)
                _head = history.Length - 1;
        }

        public void Accept(string s)
        {
            var t = _head - 1;
            if (t < 0)
                t = history.Length - 1;

            history[t] = s;
        }

        public bool PreviousAvailable()
        {
            if (_count == 0)
                return false;
            var next = _cursor - 1;
            if (next < 0)
                next = _count - 1;

            return next != _head;
        }

        public bool NextAvailable()
        {
            if (_count == 0)
                return false;
            var next = (_cursor + 1) % history.Length;
            return next != _head;
        }


        // Returns: a string with the previous line contents, or
        // nul if there is no data in the history to move to.
        public string Previous()
        {
            if (!PreviousAvailable())
                return null;

            _cursor--;
            if (_cursor < 0)
                _cursor = history.Length - 1;

            return history[_cursor];
        }

        public string Next()
        {
            if (!NextAvailable())
                return null;

            _cursor = (_cursor + 1) % history.Length;
            return history[_cursor];
        }

        public void CursorToEnd()
        {
            if (_head == _tail)
                return;

            _cursor = _head;
        }

        public void Dump()
        {
            Console.WriteLine("Head={0} Tail={1} Cursor={2} count={3}", _head, _tail, _cursor, _count);
            for (var i = 0; i < history.Length; i++)
            {
                Console.WriteLine(" {0} {1}: {2}", i == _cursor ? "==>" : "   ", i, history[i]);
            }
        }

        public string SearchBackward(string term)
        {
            for (var i = 0; i < _count; i++)
            {
                var slot = _cursor - i - 1;
                if (slot < 0)
                    slot = history.Length + slot;
                if (slot >= history.Length)
                    slot = 0;
                if (history[slot] != null && history[slot].IndexOf(term, StringComparison.Ordinal) != -1)
                {
                    _cursor = slot;
                    return history[slot];
                }
            }

            return null;
        }
    }
}