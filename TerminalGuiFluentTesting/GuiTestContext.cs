﻿using System.Text;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using Terminal.Gui.ConsoleDrivers;

namespace TerminalGuiFluentTesting;

public class GuiTestContext : IDisposable
{
    private readonly CancellationTokenSource _cts = new ();
    private readonly CancellationTokenSource _hardStop = new (With.Timeout);
    private readonly Task _runTask;
    private Exception _ex;
    private readonly FakeOutput _output = new ();
    private readonly FakeWindowsInput _winInput;
    private readonly FakeNetInput _netInput;
    private View? _lastView;
    private readonly StringBuilder _logsSb;

    internal GuiTestContext (Func<Toplevel> topLevelBuilder, int width, int height)
    {
        IApplication origApp = ApplicationImpl.Instance;
        ILogger? origLogger = Logging.Logger;
        _logsSb = new ();

        _netInput = new (_cts.Token);
        _winInput = new (_cts.Token);

        _output.Size = new (width, height);

        var v2 = new ApplicationV2 (
                                    () => _netInput,
                                    () => _output,
                                    () => _winInput,
                                    () => _output);

        var booting = new SemaphoreSlim (0, 1);

        // Start the application in a background thread
        _runTask = Task.Run (
                             () =>
                             {
                                 try
                                 {
                                     ApplicationImpl.ChangeInstance (v2);

                                     ILogger logger = LoggerFactory.Create (
                                                                            builder =>
                                                                                builder.SetMinimumLevel (LogLevel.Trace)
                                                                                       .AddProvider (new TextWriterLoggerProvider (new StringWriter (_logsSb))))
                                                                   .CreateLogger ("Test Logging");
                                     Logging.Logger = logger;

                                     v2.Init (null, "v2win");

                                     booting.Release ();

                                     Toplevel t = topLevelBuilder ();

                                     Application.Run (t); // This will block, but it's on a background thread now

                                     Application.Shutdown ();
                                 }
                                 catch (OperationCanceledException)
                                 { }
                                 catch (Exception ex)
                                 {
                                     _ex = ex;
                                 }
                                 finally
                                 {
                                     ApplicationImpl.ChangeInstance (origApp);
                                     Logging.Logger = origLogger;
                                 }
                             },
                             _cts.Token);

        // Wait for booting to complete with a timeout to avoid hangs
        if (!booting.WaitAsync (TimeSpan.FromSeconds (5)).Result)
        {
            throw new TimeoutException ("Application failed to start within the allotted time.");
        }

        WaitIteration ();
    }

    /// <summary>
    ///     Stops the application and waits for the background thread to exit.
    /// </summary>
    public GuiTestContext Stop ()
    {
        if (_runTask.IsCompleted)
        {
            return this;
        }

        Application.Invoke (() => Application.RequestStop ());

        // Wait for the application to stop, but give it a 1-second timeout
        if (!_runTask.Wait (TimeSpan.FromMilliseconds (1000)))
        {
            _cts.Cancel ();

            // Timeout occurred, force the task to stop
            _hardStop.Cancel ();

            throw new TimeoutException ("Application failed to stop within the allotted time.");
        }

        _cts.Cancel ();

        if (_ex != null)
        {
            throw _ex; // Propagate any exception that happened in the background task
        }

        return this;
    }

    // Cleanup to avoid state bleed between tests
    public void Dispose ()
    {
        Stop ();

        if (_hardStop.IsCancellationRequested)
        {
            throw new (
                       "Application was hard stopped, typically this means it timed out or did not shutdown gracefully. Ensure you call Stop in your test");
        }

        _hardStop.Cancel ();
    }

    /// <summary>
    ///     Adds the given <paramref name="v"/> to the current top level view
    ///     and performs layout.
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public GuiTestContext Add (View v)
    {
        WaitIteration (
                       () =>
                       {
                           Toplevel top = Application.Top ?? throw new ("Top was null so could not add view");
                           top.Add (v);
                           top.Layout ();
                           _lastView = v;
                       });

        return this;
    }

    public GuiTestContext ResizeConsole (int width, int height)
    {
        _output.Size = new (width, height);

        return WaitIteration ();
    }

    public GuiTestContext ScreenShot (string title, TextWriter writer)
    {
        writer.WriteLine (title + ":");
        var text = Application.ToString ();

        writer.WriteLine (text);

        return WaitIteration ();
    }

    public GuiTestContext WriteOutLogs (TextWriter writer)
    {
        writer.WriteLine (_logsSb.ToString ());

        return WaitIteration ();
    }

    public GuiTestContext WaitIteration (Action? a = null)
    {
        a ??= () => { };
        var ctsLocal = new CancellationTokenSource ();

        Application.Invoke (
                            () =>
                            {
                                a ();
                                ctsLocal.Cancel ();
                            });

        // Blocks until either the token or the hardStopToken is cancelled.
        WaitHandle.WaitAny (
                            new []
                            {
                                _cts.Token.WaitHandle,
                                _hardStop.Token.WaitHandle,
                                ctsLocal.Token.WaitHandle
                            });

        return this;
    }

    public GuiTestContext Then (Action doAction)
    {
        doAction ();

        return this;
    }

    public GuiTestContext RightClick (int screenX, int screenY) { return Click (WindowsConsole.ButtonState.Button3Pressed, screenX, screenY); }

    public GuiTestContext LeftClick (int screenX, int screenY) { return Click (WindowsConsole.ButtonState.Button1Pressed, screenX, screenY); }

    private GuiTestContext Click (WindowsConsole.ButtonState btn, int screenX, int screenY)
    {
        _winInput.InputBuffer.Enqueue (
                                       new ()
                                       {
                                           EventType = WindowsConsole.EventType.Mouse,
                                           MouseEvent = new ()
                                           {
                                               ButtonState = btn,
                                               MousePosition = new ((short)screenX, (short)screenY)
                                           }
                                       });

        _winInput.InputBuffer.Enqueue (
                                       new ()
                                       {
                                           EventType = WindowsConsole.EventType.Mouse,
                                           MouseEvent = new ()
                                           {
                                               ButtonState = WindowsConsole.ButtonState.NoButtonPressed,
                                               MousePosition = new ((short)screenX, (short)screenY)
                                           }
                                       });

        WaitIteration ();

        return this;
    }

    public GuiTestContext Down ()
    {
        SendWindowsKey (ConsoleKeyMapping.VK.DOWN);

        return this;
    }

    public GuiTestContext Right ()
    {
        SendWindowsKey (ConsoleKeyMapping.VK.RIGHT);

        return this;
    }

    public GuiTestContext Left ()
    {
        SendWindowsKey (ConsoleKeyMapping.VK.LEFT);

        return this;
    }

    public GuiTestContext Up ()
    {
        SendWindowsKey (ConsoleKeyMapping.VK.UP);

        return this;
    }

    public GuiTestContext Enter ()
    {
        SendWindowsKey (
                        new WindowsConsole.KeyEventRecord
                        {
                            UnicodeChar = '\r',
                            dwControlKeyState = WindowsConsole.ControlKeyState.NoControlKeyPressed,
                            wRepeatCount = 1,
                            wVirtualKeyCode = ConsoleKeyMapping.VK.RETURN,
                            wVirtualScanCode = 28
                        });

        return this;
    }

    /// <summary>
    ///     Send a full windows OS key including both down and up.
    /// </summary>
    /// <param name="fullKey"></param>
    private void SendWindowsKey (WindowsConsole.KeyEventRecord fullKey)
    {
        WindowsConsole.KeyEventRecord down = fullKey;
        WindowsConsole.KeyEventRecord up = fullKey; // because struct this is new copy

        down.bKeyDown = true;
        up.bKeyDown = false;

        _winInput.InputBuffer.Enqueue (
                                       new ()
                                       {
                                           EventType = WindowsConsole.EventType.Key,
                                           KeyEvent = down
                                       });

        _winInput.InputBuffer.Enqueue (
                                       new ()
                                       {
                                           EventType = WindowsConsole.EventType.Key,
                                           KeyEvent = up
                                       });

        WaitIteration ();
    }

    /// <summary>
    ///     Sends a special key e.g. cursor key that does not map to a specific character
    /// </summary>
    /// <param name="specialKey"></param>
    private void SendWindowsKey (ConsoleKeyMapping.VK specialKey)
    {
        _winInput.InputBuffer.Enqueue (
                                       new ()
                                       {
                                           EventType = WindowsConsole.EventType.Key,
                                           KeyEvent = new ()
                                           {
                                               bKeyDown = true,
                                               wRepeatCount = 0,
                                               wVirtualKeyCode = specialKey,
                                               wVirtualScanCode = 0,
                                               UnicodeChar = '\0',
                                               dwControlKeyState = WindowsConsole.ControlKeyState.NoControlKeyPressed
                                           }
                                       });

        _winInput.InputBuffer.Enqueue (
                                       new ()
                                       {
                                           EventType = WindowsConsole.EventType.Key,
                                           KeyEvent = new ()
                                           {
                                               bKeyDown = false,
                                               wRepeatCount = 0,
                                               wVirtualKeyCode = specialKey,
                                               wVirtualScanCode = 0,
                                               UnicodeChar = '\0',
                                               dwControlKeyState = WindowsConsole.ControlKeyState.NoControlKeyPressed
                                           }
                                       });

        WaitIteration ();
    }

    public GuiTestContext WithContextMenu (ContextMenu ctx, MenuBarItem menuItems)
    {
        LastView.MouseEvent += (s, e) =>
                               {
                                   if (e.Flags.HasFlag (MouseFlags.Button3Clicked))
                                   {
                                       ctx.Show (menuItems);
                                   }
                               };

        return this;
    }

    public View LastView => _lastView ?? Application.Top ?? throw new ("Could not determine which view to add to");
}
