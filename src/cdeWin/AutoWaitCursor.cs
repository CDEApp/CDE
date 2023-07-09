using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
// ReSharper disable UnusedMember.Local

// This found at http://www.vbusers.com/codecsharp/codeget.asp?ThreadID=58&PostID=1&NumReplies=0
// StackOverflow question i asked in relation to problem.
// http://stackoverflow.com/questions/27763003/windows-forms-app-with-autowaitcursor-crashing-in-vs2013ce-debugger-if-platfor
// With a good response, fixed the crash in debugger.
// Also setup for ApplicationExit so splash form does not cause AutoWait to exit.
// Unless you are running blocking tasks on your gui thread AutoWait is not overly active.

namespace cdeWin;

/// <summary>
/// This static utility class can be used to automatically show a wait cursor when the application
/// is busy (ie not responding to user input). The class automatically monitors the application
/// state, removing the need for manually changing the cursor.
/// </summary>
/// <example>
/// To use, simply insert the following line in your Application startup code
///
///		private void Form1_Load(object sender, System.EventArgs e)
///		{
///			AutoWaitCursor.Cursor = Cursors.WaitCursor;
///			AutoWaitCursor.Delay = new TimeSpan(0, 0, 0, 0, 25);
///			// Set the window handle to the handle of the main form in your application
///			AutoWaitCursor.MainWindowHandle = this.Handle;
///			AutoWaitCursor.Start();
///		}
///
/// This installs changes to cursor after 100ms of blocking work (ie. work carried out on the main application thread).
///
/// Note, the above code GLOBALLY replaces the following:
///
/// public void DoWork()
/// {
///		try
///		{
///			Screen.Cursor = Cursors.Wait;
///			GetResultsFromDatabase();
///		}
///		finally
///		{
///			Screen.Cursor = Cursors.Default;
///		}
/// }
/// </example>
[DebuggerStepThrough]
public class AutoWaitCursor
{
    // ReSharper disable MemberHidesStaticFromOuterClass

    #region Member Variables

    private static readonly TimeSpan DefaultDelay = new(0, 0, 0, 0, 25);
    /// <summary>
    /// The application state monitor class (which monitors the application busy status).
    /// </summary>
    private static readonly ApplicationStateMonitor _appStateMonitor = new(Cursors.WaitCursor, DefaultDelay);

    #endregion

    #region Constructors

    /// <summary>
    /// Default Constructor.
    /// </summary>
    private AutoWaitCursor()
    {
        // Intentionally blank
    }

    #endregion

    #region Public Static Properties

    /// <summary>
    /// Returns the amount of time the application has been idle.
    /// </summary>
    public TimeSpan ApplicationIdleTime
    {
        get { return _appStateMonitor.ApplicationIdleTime; }
    }

    /// <summary>
    /// Returns true if the auto wait cursor has been started.
    /// </summary>
    public static bool IsStarted
    {
        get { return _appStateMonitor.IsStarted; }
    }

    /// <summary>
    /// Gets or sets the Cursor to use during Application busy periods.
    /// </summary>
    public static Cursor Cursor
    {
        get { return _appStateMonitor.Cursor; }
        set
        {
            _appStateMonitor.Cursor = value;
        }
    }

    /// <summary>
    /// Enables or disables the auto wait cursor.
    /// </summary>
    public static bool Enabled
    {
        get { return _appStateMonitor.Enabled; }
        set
        {
            _appStateMonitor.Enabled = value;
        }
    }

    /// <summary>
    /// Gets or sets the period of Time to wait before showing the WaitCursor whilst Application is working
    /// </summary>
    public static TimeSpan Delay
    {
        get { return _appStateMonitor.Delay; }
        set { _appStateMonitor.Delay = value; }
    }

    /// <summary>
    /// Gets or sets the main window handle of the application (ie the handle of an MDI form).
    /// This is the window handle monitored to detect when the application becomes busy.
    /// </summary>
    public static IntPtr MainWindowHandle
    {
        get { return _appStateMonitor.MainWindowHandle; }
        set { _appStateMonitor.MainWindowHandle = value; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts the auto wait cursor monitoring the application.
    /// </summary>
    public static void Start()
    {
        _appStateMonitor.Start();
    }

    /// <summary>
    /// Stops the auto wait cursor monitoring the application.
    /// </summary>
    public static void Stop()
    {
        _appStateMonitor.Stop();
    }

    #endregion

    #region Private Class ApplicationStateMonitor

    /// <summary>
    /// Private class that monitors the state of the application and automatically
    /// changes the cursor accordingly.
    /// </summary>
    private class ApplicationStateMonitor : IDisposable
    {
        #region Member Variables

        /// <summary>
        /// The time the application became inactive.
        /// </summary>
        private DateTime _inactiveStart = DateTime.Now;
        /// <summary>
        /// If the monitor has been started.
        /// </summary>
        private bool _isStarted;// = false;
        /// <summary>
        /// Delay to wait before calling back
        /// </summary>
        private TimeSpan _delay;
        /// <summary>
        /// The windows handle to the main process window.
        /// </summary>
        private IntPtr _mainWindowHandle = IntPtr.Zero;
        /// <summary>
        /// Thread to perform the wait and callback
        /// </summary>
        private Thread _callbackThread;// = null;
        /// <summary>
        /// Stores if the class has been disposed of.
        /// </summary>
        private bool _isDisposed;// = false;
        /// <summary>
        /// Stores if the class is enabled or not.
        /// </summary>
        private bool _enabled = true;
        /// <summary>
        /// GUI Thread Id .
        /// </summary>
        private readonly uint _mainThreadId;
        /// <summary>
        /// Callback Thread Id.
        /// </summary>
        private uint _callbackThreadId;
        /// <summary>
        /// Stores the old cursor.
        /// </summary>
        private Cursor _oldCursor;
        /// <summary>
        /// Stores the new cursor.
        /// </summary>
        private Cursor _waitCursor;

        #endregion

        #region PInvokes

        // FROM http://www.pinvoke.net/default.aspx/user32.SendMessageTimeout
        [Flags]
        public enum SendMessageTimeoutFlags : uint
        {
            SmtoNormal = 0x0,
            SmtoBlock = 0x1,
            SmtoAbortifhung = 0x2,
            SmtoNotimeoutifnothung = 0x8,
            SmtoErroronexit = 0x20
        }

        // FROM http://www.pinvoke.net/default.aspx/user32.SendMessageTimeout, this fixes the crash in vs2015 64bit Debug mode in vshost.exe
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr windowHandle,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            SendMessageTimeoutFlags flags,
            uint timeout,
            out IntPtr result);

        [DllImport("USER32.DLL")]
        private static extern uint AttachThreadInput(uint attachTo, uint attachFrom, bool attach);

        [DllImport("KERNEL32.DLL")]
        private static extern uint GetCurrentThreadId();

        // ReSharper disable InconsistentNaming
        //private const int SMTO_NORMAL = 0x0000;
        // private const int SMTO_BLOCK = 0x0001;
        //private const int SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;
        // ReSharper restore InconsistentNaming

        #endregion

        #region Constructors

        /// <summary>
        /// Default member initialising Constructor
        /// </summary>
        /// <param name="waitCursor">The wait cursor to use.</param>
        /// <param name="delay">The delay before setting the cursor to the wait cursor.</param>
        public ApplicationStateMonitor(Cursor waitCursor, TimeSpan delay)
        {
            // Constructor is called from (what is treated as) the main thread
            _mainThreadId = GetCurrentThreadId();
            _delay = delay;
            _waitCursor = waitCursor;
            // Gracefully shuts down the state monitor
            Application.ThreadExit += _OnApplicationThreadExit;
            //Application.ApplicationExit += _OnApplicationThreadExit;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// On Disposal terminates the Thread, calls Finish (on thread) if Start has been called
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            // Kills the Thread loop
            _isDisposed = true;
        }

        #endregion IDisposable

        #region Public Methods

        /// <summary>
        /// Starts the application state monitor.
        /// </summary>
        public void Start()
        {
            if (!_isStarted)
            {
                _isStarted = true;
                CreateMonitorThread();
            }
        }

        /// <summary>
        /// Stops the application state monitor.
        /// </summary>
        public void Stop()
        {
            if (_isStarted)
            {
                _isStarted = false;
            }
        }

        /// <summary>
        /// Set the Cursor to wait.
        /// </summary>
        public void SetWaitCursor()
        {
            // Start is called in a new Thread, grab the new Thread Id so we can attach to Main thread's input
            _callbackThreadId = GetCurrentThreadId();

            // Have to call this before calling Cursor.Current
            AttachThreadInput(_callbackThreadId, _mainThreadId, true);

            _oldCursor = Cursor.Current;
            Cursor.Current = _waitCursor;
        }

        /// <summary>
        /// Finish showing the Cursor (switch back to previous Cursor)
        /// </summary>
        public void RestoreCursor()
        {
            // Restore the cursor
            Cursor.Current = _oldCursor;
            // Detach from Main thread input
            AttachThreadInput(_callbackThreadId, _mainThreadId, false);
        }

        /// <summary>
        /// Enable/Disable the call to Start (note, once Start is called it *always* calls the paired Finish)
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Gets or sets the period of Time to wait before calling the Start method
        /// </summary>
        public TimeSpan Delay
        {
            get { return _delay; }
            set { _delay = value; }
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Returns true if the auto wait cursor has been started.
        /// </summary>
        public bool IsStarted
        {
            get { return _isStarted; }
        }

        /// <summary>
        /// Gets or sets the main window handle of the application (ie the handle of an MDI form).
        /// This is the window handle monitored to detect when the application becomes busy.
        /// </summary>
        public IntPtr MainWindowHandle
        {
            get { return _mainWindowHandle; }
            set { _mainWindowHandle = value; }
        }

        /// <summary>
        /// Gets or sets the Cursor to show
        /// </summary>
        public Cursor Cursor
        {
            get { return _waitCursor; }
            set { _waitCursor = value; }
        }

        /// <summary>
        /// Returns the amount of time the application has been idle.
        /// </summary>
        public TimeSpan ApplicationIdleTime
        {
            get { return DateTime.Now.Subtract(_inactiveStart); }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Prepares the class creating a Thread that monitors the main application state.
        /// </summary>
        private void CreateMonitorThread()
        {
            // Create the monitor thread
            _callbackThread = new Thread(ThreadCallbackLoop)
            {
                Name = "AutoWaitCursorCallback", 
                IsBackground = true
            };
            // Start the thread
            _callbackThread.Start();
        }

        /// <summary>
        /// Thread callback method.
        /// Loops calling SetWaitCursor and RestoreCursor until Disposed.
        /// </summary>
        private void ThreadCallbackLoop()
        {
            bool continueLoop = true;

            while (continueLoop && !_isDisposed && _isStarted)
            {
                try
                {
                    DoWorkOrSleep();
                }
                catch (ThreadAbortException)
                {
                    continueLoop = false;
                    // The thread is being aborted - exit gracefully.
                }
            }
        }

        private void DoWorkOrSleep()
        {
            if (!_enabled || _mainWindowHandle == IntPtr.Zero)
            {
                Thread.Sleep(_delay);
            }
            else if (_IsApplicationBusy(_delay, _mainWindowHandle))
            {
                ProcessCursorChangeAndIdleState();
            }
        }
        
        private void ProcessCursorChangeAndIdleState()
        {
            try
            {
                SetWaitCursor();
                WaitForIdle();
            }
            finally
            {
                // Always calls Finish (even if we are Disabled)
                RestoreCursor();
                // Store the time the application became inactive
                _inactiveStart = DateTime.Now;
                // Wait before checking again
                Thread.Sleep(25);
            }
        }

        /// <summary>
        /// Blocks until the application responds to a test message.
        /// If the application doesn't respond with the timespan, will return false,
        /// else returns true.
        /// </summary>
        private bool _IsApplicationBusy(TimeSpan delay, IntPtr windowHandle)
        {
            // ReSharper disable InconsistentNaming
            const int INFINITE = int.MaxValue;
            const int WM_NULL = 0;
            // ReSharper restore InconsistentNaming
            IntPtr result;// = 0;
            //bool success;

            // See if the application is responding
            if (delay == TimeSpan.MaxValue)
            {
                /*success = */
                SendMessageTimeout(windowHandle, WM_NULL, IntPtr.Zero, (IntPtr)null,
                    SendMessageTimeoutFlags.SmtoBlock, INFINITE, out result);
            }
            else
            {
                /*success = */
                SendMessageTimeout(windowHandle, WM_NULL, IntPtr.Zero, (IntPtr)null,
                    SendMessageTimeoutFlags.SmtoBlock, Convert.ToUInt32(delay.TotalMilliseconds), out result);
            }

            return result != IntPtr.Zero;
        }

        /// <summary>
        /// Waits for the ResetEvent (set by Dispose and Reset),
        /// since Start has been called we *have* to call RestoreCursor once the thread is idle again.
        /// </summary>
        private void WaitForIdle()
        {
            // Wait indefinitely until the application is idle
            _IsApplicationBusy(TimeSpan.MaxValue, _mainWindowHandle);
        }

        /// <summary>
        /// The application is closing, shut the state monitor down.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _OnApplicationThreadExit(object sender, EventArgs e)
        {
            Dispose();
        }

        #endregion
    }

    #endregion
    // ReSharper restore MemberHidesStaticFromOuterClass
}