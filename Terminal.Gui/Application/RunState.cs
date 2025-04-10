﻿namespace Terminal.Gui;

/// <summary>The execution state for a <see cref="Toplevel"/> view.</summary>
public class RunState : IDisposable
{
    /// <summary>Initializes a new <see cref="RunState"/> class.</summary>
    /// <param name="view"></param>
    public RunState (Toplevel view) { Toplevel = view; }

    /// <summary>The <see cref="Toplevel"/> belonging to this <see cref="RunState"/>.</summary>
    public Toplevel Toplevel { get; internal set; }

    /// <summary>Releases all resource used by the <see cref="RunState"/> object.</summary>
    /// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="RunState"/>.</remarks>
    /// <remarks>
    ///     <see cref="Dispose()"/> method leaves the <see cref="RunState"/> in an unusable state. After calling
    ///     <see cref="Dispose()"/>, you must release all references to the <see cref="RunState"/> so the garbage collector can
    ///     reclaim the memory that the <see cref="RunState"/> was occupying.
    /// </remarks>
    public void Dispose ()
    {
        Dispose (true);
        GC.SuppressFinalize (this);
#if DEBUG_IDISPOSABLE
        if (View.DebugIDisposable)
        {
            WasDisposed = true;
        }
#endif
    }

    /// <summary>Releases all resource used by the <see cref="RunState"/> object.</summary>
    /// <param name="disposing">If set to <see langword="true"/> we are disposing and should dispose held objects.</param>
    protected virtual void Dispose (bool disposing)
    {
        if (Toplevel is { } && disposing)
        {
            // Previously we were requiring Toplevel be disposed here.
            // But that is not correct becaue `Begin` didn't create the TopLevel, `Init` did; thus
            // disposing should be done by `Shutdown`, not `End`.
            throw new InvalidOperationException (
                                                 "Toplevel must be null before calling Application.RunState.Dispose"
                                                );
        }
    }

#if DEBUG_IDISPOSABLE
    /// <summary>For debug (see DEBUG_IDISPOSABLE define) purposes to verify objects are being disposed properly</summary>
    public bool WasDisposed;

    /// <summary>For debug (see DEBUG_IDISPOSABLE define) purposes to verify objects are being disposed properly</summary>
    public int DisposedCount = 0;

    /// <summary>For debug (see DEBUG_IDISPOSABLE define) purposes; the runstate instances that have been created</summary>
    public static List<RunState> Instances = new ();

    /// <summary>Creates a new RunState object.</summary>
    public RunState ()
    {
        if (View.DebugIDisposable)
        {
            Instances.Add (this);
        }
    }
#endif
}
