﻿#nullable enable
using Terminal.Gui.Resources;

namespace Terminal.Gui;

/// <summary>
///     Provides navigation and a user interface (UI) to collect related data across multiple steps. Each step (
///     <see cref="WizardStep"/>) can host arbitrary <see cref="View"/>s, much like a <see cref="Dialog"/>. Each step also
///     has a pane for help text. Along the bottom of the Wizard view are customizable buttons enabling the user to
///     navigate forward and backward through the Wizard.
/// </summary>
/// <remarks>
///     The Wizard can be displayed either as a modal (pop-up) <see cref="Window"/> (like <see cref="Dialog"/>) or as
///     an embedded <see cref="View"/>. By default, <see cref="Wizard.Modal"/> is <c>true</c>. In this case launch the
///     Wizard with <c>Application.Run(wizard)</c>. See <see cref="Wizard.Modal"/> for more details.
/// </remarks>
/// <example>
///     <code>
/// using Terminal.Gui;
/// using System.Text;
/// 
/// Application.Init();
/// 
/// var wizard = new Wizard ($"Setup Wizard");
/// 
/// // Add 1st step
/// var firstStep = new WizardStep ("End User License Agreement");
/// wizard.AddStep(firstStep);
/// firstStep.NextButtonText = "Accept!";
/// firstStep.HelpText = "This is the End User License Agreement.";
/// 
/// // Add 2nd step
/// var secondStep = new WizardStep ("Second Step");
/// wizard.AddStep(secondStep);
/// secondStep.HelpText = "This is the help text for the Second Step.";
/// var lbl = new Label () { Text = "Name:" };
/// secondStep.Add(lbl);
/// 
/// var name = new TextField { X = Pos.Right (lbl) + 1, Width = Dim.Fill () - 1 };
/// secondStep.Add(name);
/// 
/// wizard.Finished += (args) =>
/// {
///     MessageBox.Query("Wizard", $"Finished. The Name entered is '{name.Text}'", "Ok");
///     Application.RequestStop();
/// };
/// 
/// Application.Top.Add (wizard);
/// Application.Run ();
/// Application.Shutdown ();
/// </code>
/// </example>
public class Wizard : Dialog
{
    private readonly LinkedList<WizardStep> _steps = new ();
    private WizardStep? _currentStep;
    private bool _finishedPressed;
    private string _wizardTitle = string.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Wizard"/> class.
    /// </summary>
    /// <remarks>
    ///     The Wizard will be vertically and horizontally centered in the container. After initialization use <c>X</c>,
    ///     <c>Y</c>, <c>Width</c>, and <c>Height</c> change size and position.
    /// </remarks>
    public Wizard ()
    {
        // TODO: LastEndRestStart will enable a "Quit" button to always appear at the far left
        ButtonAlignment = Alignment.Start;
        ButtonAlignmentModes |= AlignmentModes.IgnoreFirstOrLast;
        BorderStyle = LineStyle.Double;

        BackButton = new () { Text = Strings.wzBack };

        NextFinishButton = new ()
        {
            Text = Strings.wzFinish,
            IsDefault = true
        };

        //// Add a horiz separator
        var separator = new LineView (Orientation.Horizontal) { Y = Pos.Top (BackButton) - 1 };

        base.Add (separator);
        AddButton (BackButton);
        AddButton (NextFinishButton);

        BackButton.Accepting += BackBtn_Accepting;
        NextFinishButton.Accepting += NextFinishBtn_Accepting;

        Loaded += Wizard_Loaded;
        Closing += Wizard_Closing;
        TitleChanged += Wizard_TitleChanged;

        SetNeedsLayout ();
    }

    /// <summary>
    ///     If the <see cref="CurrentStep"/> is not the first step in the wizard, this button causes the
    ///     <see cref="MovingBack"/> event to be fired and the wizard moves to the previous step.
    /// </summary>
    /// <remarks>Use the <see cref="MovingBack"></see> event to be notified when the user attempts to go back.</remarks>
    public Button BackButton { get; }

    /// <summary>Gets or sets the currently active <see cref="WizardStep"/>.</summary>
    public WizardStep? CurrentStep
    {
        get => _currentStep;
        set => GoToStep (value);
    }

    /// <summary>
    ///     Determines whether the <see cref="Wizard"/> is displayed as modal pop-up or not. The default is
    ///     <see langword="true"/>. The Wizard will be shown with a frame and title and will behave like any
    ///     <see cref="Toplevel"/> window. If set to <c>false</c> the Wizard will have no frame and will behave like any
    ///     embedded <see cref="View"/>. To use Wizard as an embedded View
    ///     <list type="number">
    ///         <item>
    ///             <description>Set <see cref="Modal"/> to <c>false</c>.</description>
    ///         </item>
    ///         <item>
    ///             <description>Add the Wizard to a containing view with <see cref="View.Add(View)"/>.</description>
    ///         </item>
    ///     </list>
    ///     If a non-Modal Wizard is added to the application after
    ///     <see cref="Application.Run(Toplevel, Func{Exception, bool})"/> has
    ///     been called the first step must be explicitly set by setting <see cref="CurrentStep"/> to
    ///     <see cref="GetNextStep()"/>:
    ///     <code>
    ///    wizard.CurrentStep = wizard.GetNextStep();
    /// </code>
    /// </summary>
    public new bool Modal
    {
        get => base.Modal;
        set
        {
            base.Modal = value;

            foreach (WizardStep step in _steps)
            {
                SizeStep (step);
            }

            if (base.Modal)
            {
                ColorScheme = Colors.ColorSchemes ["Dialog"];
                BorderStyle = LineStyle.Rounded;
            }
            else
            {
                if (SuperView is { })
                {
                    ColorScheme = SuperView.ColorScheme;
                }
                else
                {
                    ColorScheme = Colors.ColorSchemes ["Base"];
                }

                CanFocus = true;
                BorderStyle = LineStyle.None;
            }
        }
    }

    /// <summary>
    ///     If the <see cref="CurrentStep"/> is the last step in the wizard, this button causes the <see cref="Finished"/>
    ///     event to be fired and the wizard to close. If the step is not the last step, the <see cref="MovingNext"/> event
    ///     will be fired and the wizard will move next step.
    /// </summary>
    /// <remarks>
    ///     Use the <see cref="MovingNext"></see> and <see cref="Finished"></see> events to be notified when the user
    ///     attempts go to the next step or finish the wizard.
    /// </remarks>
    public Button NextFinishButton { get; }

    /// <summary>
    ///     Adds a step to the wizard. The Next and Back buttons navigate through the added steps in the order they were
    ///     added.
    /// </summary>
    /// <param name="newStep"></param>
    /// <remarks>The "Next..." button of the last step added will read "Finish" (unless changed from default).</remarks>
    public void AddStep (WizardStep newStep)
    {
        SizeStep (newStep);

        newStep.EnabledChanged += (s, e) => UpdateButtonsAndTitle ();
        newStep.TitleChanged += (s, e) => UpdateButtonsAndTitle ();
        _steps.AddLast (newStep);
        Add (newStep);
        UpdateButtonsAndTitle ();
    }

    /// <summary>
    ///     Raised when the user has cancelled the <see cref="Wizard"/> by pressing the Esc key. To prevent a modal (
    ///     <see cref="Wizard.Modal"/> is <c>true</c>) Wizard from closing, cancel the event by setting
    ///     <see cref="WizardButtonEventArgs.Cancel"/> to <c>true</c> before returning from the event handler.
    /// </summary>
    public event EventHandler<WizardButtonEventArgs>? Cancelled;

    /// <summary>
    ///     Raised when the Next/Finish button in the <see cref="Wizard"/> is clicked. The Next/Finish button is always
    ///     the last button in the array of Buttons passed to the <see cref="Wizard"/> constructor, if any. This event is only
    ///     raised if the <see cref="CurrentStep"/> is the last Step in the Wizard flow (otherwise the <see cref="Finished"/>
    ///     event is raised).
    /// </summary>
    public event EventHandler<WizardButtonEventArgs>? Finished;

    /// <summary>Returns the first enabled step in the Wizard</summary>
    /// <returns>The last enabled step</returns>
    public WizardStep? GetFirstStep () { return _steps.FirstOrDefault (s => s.Enabled); }

    /// <summary>Returns the last enabled step in the Wizard</summary>
    /// <returns>The last enabled step</returns>
    public WizardStep? GetLastStep () { return _steps.LastOrDefault (s => s.Enabled); }

    /// <summary>
    ///     Returns the next enabled <see cref="WizardStep"/> after the current step. Takes into account steps which are
    ///     disabled. If <see cref="CurrentStep"/> is <c>null</c> returns the first enabled step.
    /// </summary>
    /// <returns>
    ///     The next step after the current step, if there is one; otherwise returns <c>null</c>, which indicates either
    ///     there are no enabled steps or the current step is the last enabled step.
    /// </returns>
    public WizardStep? GetNextStep ()
    {
        LinkedListNode<WizardStep>? step = null;

        if (CurrentStep is null)
        {
            // Get first step, assume it is next
            step = _steps.First;
        }
        else
        {
            // Get the step after current
            step = _steps.Find (CurrentStep);

            if (step is { })
            {
                step = step.Next;
            }
        }

        // step now points to the potential next step
        while (step is { })
        {
            if (step.Value.Enabled)
            {
                return step.Value;
            }

            step = step.Next;
        }

        return null;
    }

    /// <summary>
    ///     Returns the first enabled <see cref="WizardStep"/> before the current step. Takes into account steps which are
    ///     disabled. If <see cref="CurrentStep"/> is <c>null</c> returns the last enabled step.
    /// </summary>
    /// <returns>
    ///     The first step ahead of the current step, if there is one; otherwise returns <c>null</c>, which indicates
    ///     either there are no enabled steps or the current step is the first enabled step.
    /// </returns>
    public WizardStep? GetPreviousStep ()
    {
        LinkedListNode<WizardStep>? step = null;

        if (CurrentStep is null)
        {
            // Get last step, assume it is previous
            step = _steps.Last;
        }
        else
        {
            // Get the step before current
            step = _steps.Find (CurrentStep);

            if (step is { })
            {
                step = step.Previous;
            }
        }

        // step now points to the potential previous step
        while (step is { })
        {
            if (step.Value.Enabled)
            {
                return step.Value;
            }

            step = step.Previous;
        }

        return null;
    }

    /// <summary>
    ///     Causes the wizard to move to the previous enabled step (or first step if <see cref="CurrentStep"/> is not set).
    ///     If there is no previous step, does nothing.
    /// </summary>
    /// <returns><see langword="true"/> if the transition to the step succeeded. <see langword="false"/> if the step was not found or the operation was cancelled.</returns>
    public bool GoBack ()
    {
        WizardStep? previous = GetPreviousStep ();

        if (previous is { })
        {
           return GoToStep (previous);
        }

        return false;
    }

    /// <summary>
    ///     Causes the wizard to move to the next enabled step (or last step if <see cref="CurrentStep"/> is not set). If
    ///     there is no previous step, does nothing.
    /// </summary>
    /// <returns><see langword="true"/> if the transition to the step succeeded. <see langword="false"/> if the step was not found or the operation was cancelled.</returns>
    public bool GoNext ()
    {
        WizardStep? nextStep = GetNextStep ();

        if (nextStep is { })
        {
            return GoToStep (nextStep);
        }

        return false;
    }

    /// <summary>Changes to the specified <see cref="WizardStep"/>.</summary>
    /// <param name="newStep">The step to go to.</param>
    /// <returns><see langword="true"/> if the transition to the step succeeded. <see langword="false"/> if the step was not found or the operation was cancelled.</returns>
    public bool GoToStep (WizardStep? newStep)
    {
        if (OnStepChanging (_currentStep, newStep) || newStep is { Enabled: false })
        {
            return false;
        }

        // Hide all but the new step
        foreach (WizardStep step in _steps)
        {
            step.Visible = step == newStep;
            step.ShowHide ();
        }

        WizardStep? oldStep = _currentStep;
        _currentStep = newStep;

        UpdateButtonsAndTitle ();

        // Set focus on the contentview
        newStep?.SubViews.ToArray () [0].SetFocus ();

        if (OnStepChanged (oldStep, _currentStep))
        {
            // For correctness, we do this, but it's meaningless because there's nothing to cancel
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Raised when the Back button in the <see cref="Wizard"/> is clicked. The Back button is always the first button
    ///     in the array of Buttons passed to the <see cref="Wizard"/> constructor, if any.
    /// </summary>
    public event EventHandler<WizardButtonEventArgs>? MovingBack;

    /// <summary>
    ///     Raised when the Next/Finish button in the <see cref="Wizard"/> is clicked (or the user presses Enter). The
    ///     Next/Finish button is always the last button in the array of Buttons passed to the <see cref="Wizard"/>
    ///     constructor, if any. This event is only raised if the <see cref="CurrentStep"/> is the last Step in the Wizard flow
    ///     (otherwise the <see cref="Finished"/> event is raised).
    /// </summary>
    public event EventHandler<WizardButtonEventArgs>? MovingNext;

    /// <summary>
    ///     <see cref="Wizard"/> is derived from <see cref="Dialog"/> and Dialog causes <c>Esc</c> to call
    ///     <see cref="Application.RequestStop(Toplevel)"/>, closing the Dialog. Wizard overrides
    ///     <see cref="OnKeyDownNotHandled"/> to instead fire the <see cref="Cancelled"/> event when Wizard is being used as a
    ///     non-modal (see <see cref="Wizard.Modal"/>).
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    protected override bool OnKeyDownNotHandled (Key key)
    {
        //// BUGBUG: Why is this not handled by a key binding???
        if (!Modal)
        {
            if (key == Key.Esc)
            {
                var args = new WizardButtonEventArgs ();
                Cancelled?.Invoke (this, args);

                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Called when the <see cref="Wizard"/> has completed transition to a new <see cref="WizardStep"/>. Fires the
    ///     <see cref="StepChanged"/> event.
    /// </summary>
    /// <param name="oldStep">The step the Wizard changed from</param>
    /// <param name="newStep">The step the Wizard has changed to</param>
    /// <returns>True if the change is to be cancelled.</returns>
    public virtual bool OnStepChanged (WizardStep? oldStep, WizardStep? newStep)
    {
        var args = new StepChangeEventArgs (oldStep, newStep);
        StepChanged?.Invoke (this, args);

        return args.Cancel;
    }

    /// <summary>
    ///     Called when the <see cref="Wizard"/> is about to transition to another <see cref="WizardStep"/>. Fires the
    ///     <see cref="StepChanging"/> event.
    /// </summary>
    /// <param name="oldStep">The step the Wizard is about to change from</param>
    /// <param name="newStep">The step the Wizard is about to change to</param>
    /// <returns>True if the change is to be cancelled.</returns>
    public virtual bool OnStepChanging (WizardStep? oldStep, WizardStep? newStep)
    {
        var args = new StepChangeEventArgs (oldStep, newStep);
        StepChanging?.Invoke (this, args);

        return args.Cancel;
    }

    /// <summary>This event is raised after the <see cref="Wizard"/> has changed the <see cref="CurrentStep"/>.</summary>
    public event EventHandler<StepChangeEventArgs>? StepChanged;

    /// <summary>
    ///     This event is raised when the current <see cref="CurrentStep"/>) is about to change. Use
    ///     <see cref="StepChangeEventArgs.Cancel"/> to abort the transition.
    /// </summary>
    public event EventHandler<StepChangeEventArgs>? StepChanging;

    private void BackBtn_Accepting (object? sender, CommandEventArgs e)
    {
        var args = new WizardButtonEventArgs ();
        MovingBack?.Invoke (this, args);

        if (!args.Cancel)
        {
            e.Cancel = GoBack ();
        }
    }

    private void NextFinishBtn_Accepting (object? sender, CommandEventArgs e)
    {
        if (CurrentStep == GetLastStep ())
        {
            var args = new WizardButtonEventArgs ();
            Finished?.Invoke (this, args);

            if (!args.Cancel)
            {
                _finishedPressed = true;

                if (IsCurrentTop)
                {
                    Application.RequestStop (this);
                    e.Cancel = true;
                }

                // Wizard was created as a non-modal (just added to another View). 
                // Do nothing
            }
        }
        else
        {
            var args = new WizardButtonEventArgs ();
            MovingNext?.Invoke (this, args);

            if (!args.Cancel)
            {
                e.Cancel = GoNext ();
            }
        }
    }

    private void SizeStep (WizardStep step)
    {
        if (Modal)
        {
            // If we're modal, then we expand the WizardStep so that the top and side 
            // borders and not visible. The bottom border is the separator above the buttons.
            step.X = step.Y = 0;

            step.Height = Dim.Fill (
                                    Dim.Func (
                                              () => IsInitialized
                                                        ? SubViews.First (view => view.Y.Has<PosAnchorEnd> (out _)).Frame.Height + 1
                                                        : 1)); // for button frame (+1 for lineView)
            step.Width = Dim.Fill ();
        }
        else
        {
            // If we're not a modal, then we show the border around the WizardStep
            step.X = step.Y = 0;

            step.Height = Dim.Fill (
                                    Dim.Func (
                                              () => IsInitialized
                                                        ? SubViews.First (view => view.Y.Has<PosAnchorEnd> (out _)).Frame.Height + 1
                                                        : 2)); // for button frame (+1 for lineView)
            step.Width = Dim.Fill ();
        }
    }

    private void UpdateButtonsAndTitle ()
    {
        if (CurrentStep is null)
        {
            return;
        }

        Title = $"{_wizardTitle}{(_steps.Count > 0 ? " - " + CurrentStep.Title : string.Empty)}";

        // Configure the Back button
        BackButton.Text = CurrentStep.BackButtonText != string.Empty
                              ? CurrentStep.BackButtonText
                              : Strings.wzBack; // "_Back";
        BackButton.Visible = CurrentStep != GetFirstStep ();

        // Configure the Next/Finished button
        if (CurrentStep == GetLastStep ())
        {
            NextFinishButton.Text = CurrentStep.NextButtonText != string.Empty
                                        ? CurrentStep.NextButtonText
                                        : Strings.wzFinish; // "Fi_nish";
        }
        else
        {
            NextFinishButton.Text = CurrentStep.NextButtonText != string.Empty
                                        ? CurrentStep.NextButtonText
                                        : Strings.wzNext; // "_Next...";
        }

        SizeStep (CurrentStep);

        SetNeedsLayout ();
    }

    private void Wizard_Closing (object? sender, ToplevelClosingEventArgs obj)
    {
        if (!_finishedPressed)
        {
            var args = new WizardButtonEventArgs ();
            Cancelled?.Invoke (this, args);
        }
    }

    private void Wizard_Loaded (object? sender, EventArgs args)
    {
        CurrentStep = GetFirstStep ();

        // gets the first step if CurrentStep == null
    }

    private void Wizard_TitleChanged (object? sender, EventArgs<string> e)
    {
        if (string.IsNullOrEmpty (_wizardTitle))
        {
            _wizardTitle = e.CurrentValue;
        }
    }
}
