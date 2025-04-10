﻿using UnitTests;
using Xunit.Abstractions;

namespace Terminal.Gui.DialogTests;

public class WizardTests ()
{
    // =========== Wizard Tests
    [Fact]
    public void DefaultConstructor_SizedProperly ()
    {
        var wizard = new Wizard ();
        Assert.NotEqual (0, wizard.Width);
        Assert.NotEqual (0, wizard.Height);
        wizard.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void Finish_Button_Closes ()
    {
        // https://github.com/gui-cs/Terminal.Gui/issues/1833
        var wizard = new Wizard ();
        var step1 = new WizardStep { Title = "step1" };
        wizard.AddStep (step1);

        var finishedFired = false;
        wizard.Finished += (s, args) => { finishedFired = true; };

        var closedFired = false;
        wizard.Closed += (s, e) => { closedFired = true; };

        RunState runstate = Application.Begin (wizard);
        var firstIteration = true;
        Application.RunIteration (ref runstate, firstIteration);

        wizard.NextFinishButton.InvokeCommand (Command.Accept);
        Application.RunIteration (ref runstate, firstIteration);
        Application.End (runstate);
        Assert.True (finishedFired);
        Assert.True (closedFired);
        step1.Dispose ();
        wizard.Dispose ();

        // Same test, but with two steps
        wizard = new ();
        firstIteration = false;
        step1 = new() { Title = "step1" };
        wizard.AddStep (step1);
        var step2 = new WizardStep { Title = "step2" };
        wizard.AddStep (step2);

        finishedFired = false;
        wizard.Finished += (s, args) => { finishedFired = true; };

        closedFired = false;
        wizard.Closed += (s, e) => { closedFired = true; };

        runstate = Application.Begin (wizard);
        Application.RunIteration (ref runstate, firstIteration);

        Assert.Equal (step1.Title, wizard.CurrentStep.Title);
        wizard.NextFinishButton.InvokeCommand (Command.Accept);
        Assert.False (finishedFired);
        Assert.False (closedFired);

        Assert.Equal (step2.Title, wizard.CurrentStep.Title);
        Assert.Equal (wizard.GetLastStep ().Title, wizard.CurrentStep.Title);
        wizard.NextFinishButton.InvokeCommand (Command.Accept);
        Application.End (runstate);
        Assert.True (finishedFired);
        Assert.True (closedFired);

        step1.Dispose ();
        step2.Dispose ();
        wizard.Dispose ();

        // Same test, but with two steps but the 1st one disabled
        wizard = new ();
        firstIteration = false;
        step1 = new() { Title = "step1" };
        wizard.AddStep (step1);
        step2 = new() { Title = "step2" };
        wizard.AddStep (step2);
        step1.Enabled = false;

        finishedFired = false;
        wizard.Finished += (s, args) => { finishedFired = true; };

        closedFired = false;
        wizard.Closed += (s, e) => { closedFired = true; };

        runstate = Application.Begin (wizard);
        Application.RunIteration (ref runstate, firstIteration);

        Assert.Equal (step2.Title, wizard.CurrentStep.Title);
        Assert.Equal (wizard.GetLastStep ().Title, wizard.CurrentStep.Title);
        wizard.NextFinishButton.InvokeCommand (Command.Accept);
        Application.End (runstate);
        Assert.True (finishedFired);
        Assert.True (closedFired);
        wizard.Dispose ();
    }

    [Fact]
    public void Navigate_GetFirstStep_Works ()
    {
        var wizard = new Wizard ();

        Assert.Null (wizard.GetFirstStep ());

        var step1 = new WizardStep { Title = "step1" };
        wizard.AddStep (step1);
        Assert.Equal (step1.Title, wizard.GetFirstStep ().Title);

        var step2 = new WizardStep { Title = "step2" };
        wizard.AddStep (step2);
        Assert.Equal (step1.Title, wizard.GetFirstStep ().Title);

        var step3 = new WizardStep { Title = "step3" };
        wizard.AddStep (step3);
        Assert.Equal (step1.Title, wizard.GetFirstStep ().Title);

        step1.Enabled = false;
        Assert.Equal (step2.Title, wizard.GetFirstStep ().Title);

        step1.Enabled = true;
        Assert.Equal (step1.Title, wizard.GetFirstStep ().Title);

        step1.Enabled = false;
        step2.Enabled = false;
        Assert.Equal (step3.Title, wizard.GetFirstStep ().Title);
        wizard.Dispose ();
    }

    [Fact]
    public void Navigate_GetLastStep_Works ()
    {
        var wizard = new Wizard ();

        Assert.Null (wizard.GetLastStep ());

        var step1 = new WizardStep { Title = "step1" };
        wizard.AddStep (step1);
        Assert.Equal (step1.Title, wizard.GetLastStep ().Title);

        var step2 = new WizardStep { Title = "step2" };
        wizard.AddStep (step2);
        Assert.Equal (step2.Title, wizard.GetLastStep ().Title);

        var step3 = new WizardStep { Title = "step3" };
        wizard.AddStep (step3);
        Assert.Equal (step3.Title, wizard.GetLastStep ().Title);

        step3.Enabled = false;
        Assert.Equal (step2.Title, wizard.GetLastStep ().Title);

        step3.Enabled = true;
        Assert.Equal (step3.Title, wizard.GetLastStep ().Title);

        step3.Enabled = false;
        step2.Enabled = false;
        Assert.Equal (step1.Title, wizard.GetLastStep ().Title);
        wizard.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void Navigate_GetNextStep_Correct ()
    {
        var wizard = new Wizard ();

        // If no steps should be null
        Assert.Null (wizard.GetNextStep ());

        var step1 = new WizardStep { Title = "step1" };
        wizard.AddStep (step1);

        // If no current step, should be first step
        Assert.Equal (step1.Title, wizard.GetNextStep ().Title);

        wizard.CurrentStep = step1;

        // If there is 1 step it's current step should be null
        Assert.Null (wizard.GetNextStep ());

        // If one disabled step should be null
        step1.Enabled = false;
        Assert.Null (wizard.GetNextStep ());

        // If two steps and at 1 and step 2 is `Enabled = true`should be step 2
        var step2 = new WizardStep { Title = "step2" };
        wizard.AddStep (step2);
        Assert.Equal (step2.Title, wizard.GetNextStep ().Title);

        // If two steps and at 1 and step 2 is `Enabled = false` should be null
        step1.Enabled = true;
        wizard.CurrentStep = step1;
        step2.Enabled = false;
        Assert.Null (wizard.GetNextStep ());

        // If three steps with Step2.Enabled = true
        //   At step 1 should be step 2
        //   At step 2 should be step 3
        //   At step 3 should be null
        var step3 = new WizardStep { Title = "step3" };
        wizard.AddStep (step3);
        step1.Enabled = true;
        wizard.CurrentStep = step1;
        step2.Enabled = true;
        step3.Enabled = true;
        Assert.Equal (step2.Title, wizard.GetNextStep ().Title);
        wizard.CurrentStep = step2;
        Assert.Equal (step3.Title, wizard.GetNextStep ().Title);
        wizard.CurrentStep = step3;
        Assert.Null (wizard.GetNextStep ());

        // If three steps with Step2.Enabled = false
        //   At step 1 should be step 3
        //   At step 3 should be null
        step1.Enabled = true;
        wizard.CurrentStep = step1;
        step2.Enabled = false;
        step3.Enabled = true;
        Assert.Equal (step3.Title, wizard.GetNextStep ().Title);
        wizard.CurrentStep = step3;
        Assert.Null (wizard.GetNextStep ());

        // If three steps with Step2.Enabled = false & Step3.Enabled = false
        //   At step 1 should be null
        step1.Enabled = true;
        wizard.CurrentStep = step1;
        step2.Enabled = false;
        step3.Enabled = false;
        Assert.Null (wizard.GetNextStep ());

        // If no current step, GetNextStep provides equivalent to GetFirstStep
        wizard.CurrentStep = null;
        step1.Enabled = true;
        step2.Enabled = true;
        step3.Enabled = true;
        Assert.Equal (step1.Title, wizard.GetNextStep ().Title);

        step1.Enabled = false;
        step2.Enabled = true;
        step3.Enabled = true;
        Assert.Equal (step2.Title, wizard.GetNextStep ().Title);

        step1.Enabled = false;
        step2.Enabled = false;
        step3.Enabled = true;
        Assert.Equal (step3.Title, wizard.GetNextStep ().Title);

        step1.Enabled = false;
        step2.Enabled = true;
        step3.Enabled = false;
        Assert.Equal (step2.Title, wizard.GetNextStep ().Title);

        step1.Enabled = true;
        step2.Enabled = false;
        step3.Enabled = false;
        Assert.Equal (step1.Title, wizard.GetNextStep ().Title);
        wizard.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void Navigate_GetPreviousStep_Correct ()
    {
        var wizard = new Wizard ();

        // If no steps should be null
        Assert.Null (wizard.GetPreviousStep ());

        var step1 = new WizardStep { Title = "step1" };
        wizard.AddStep (step1);

        // If no current step, should be last step
        Assert.Equal (step1.Title, wizard.GetPreviousStep ().Title);

        wizard.CurrentStep = step1;

        // If there is 1 step it's current step should be null
        Assert.Null (wizard.GetPreviousStep ());

        // If one disabled step should be null
        step1.Enabled = false;
        Assert.Null (wizard.GetPreviousStep ());

        // If two steps and at 2 and step 1 is `Enabled = true`should be step1
        var step2 = new WizardStep { Title = "step2" };
        wizard.AddStep (step2);
        wizard.CurrentStep = step2;
        step1.Enabled = true;
        Assert.Equal (step1.Title, wizard.GetPreviousStep ().Title);

        // If two steps and at 2 and step 1 is `Enabled = false` should be null
        step1.Enabled = false;
        Assert.Null (wizard.GetPreviousStep ());

        // If three steps with Step2.Enabled = true
        //   At step 1 should be null
        //   At step 2 should be step 1
        //   At step 3 should be step 2
        var step3 = new WizardStep { Title = "step3" };
        wizard.AddStep (step3);
        step1.Enabled = true;
        wizard.CurrentStep = step1;
        step2.Enabled = true;
        step3.Enabled = true;
        Assert.Null (wizard.GetPreviousStep ());
        wizard.CurrentStep = step2;
        Assert.Equal (step1.Title, wizard.GetPreviousStep ().Title);
        wizard.CurrentStep = step3;
        Assert.Equal (step2.Title, wizard.GetPreviousStep ().Title);

        // If three steps with Step2.Enabled = false
        //   At step 1 should be null
        //   At step 3 should be step1
        step1.Enabled = true;
        step2.Enabled = false;
        step3.Enabled = true;
        wizard.CurrentStep = step1;
        Assert.Null (wizard.GetPreviousStep ());
        wizard.CurrentStep = step3;
        Assert.Equal (step1.Title, wizard.GetPreviousStep ().Title);

        // If three steps with Step1.Enabled = false & Step2.Enabled = false
        //   At step 3 should be null

        // If no current step, GetPreviousStep provides equivalent to GetLastStep
        wizard.CurrentStep = null;
        step1.Enabled = true;
        step2.Enabled = true;
        step3.Enabled = true;
        Assert.Equal (step3.Title, wizard.GetPreviousStep ().Title);

        step1.Enabled = false;
        step2.Enabled = true;
        step3.Enabled = true;
        Assert.Equal (step3.Title, wizard.GetPreviousStep ().Title);

        step1.Enabled = false;
        step2.Enabled = false;
        step3.Enabled = true;
        Assert.Equal (step3.Title, wizard.GetPreviousStep ().Title);

        step1.Enabled = false;
        step2.Enabled = true;
        step3.Enabled = false;
        Assert.Equal (step2.Title, wizard.GetPreviousStep ().Title);

        step1.Enabled = true;
        step2.Enabled = false;
        step3.Enabled = false;
        Assert.Equal (step1.Title, wizard.GetPreviousStep ().Title);
        wizard.Dispose ();
    }

    [Fact]
    public void Navigate_GoBack_Works ()
    {
        // If zero steps do nothing

        // If one step do nothing (enabled or disabled)

        // If two steps
        //    If current is 1 does nothing
        //    If current is 2 does nothing
        //        If 1 is enabled 2 becomes current
        //        If 1 is disabled 1 stays current
    }

    [Fact]
    public void Navigate_GoNext_Works ()
    {
        // If zero steps do nothing

        // If one step do nothing (enabled or disabled)

        // If two steps
        //    If current is 1
        //        If 2 is enabled 2 becomes current
        //        If 2 is disabled 1 stays current
        //    If current is 2 does nothing
    }

    [Fact]
    [AutoInitShutdown]

    // This test verifies that a single step wizard shows the correct buttons
    // and that the title is correct
    public void OneStepWizard_Shows ()
    {
        var d = (FakeDriver)Application.Driver;

        var title = "1234";
        var stepTitle = "ABCD";

        var width = 30;
        var height = 7;
        d.SetBufferSize (width, height);

        //	var btnBackText = "Back";
        var btnBack = string.Empty; // $"{Glyphs.LeftBracket} {btnBackText} {Glyphs.RightBracket}";
        var btnNextText = "Finish"; // "Next";

        var btnNext =
            $"{
                Glyphs.LeftBracket
            }{
                Glyphs.LeftDefaultIndicator
            } {
                btnNextText
            } {
                Glyphs.RightDefaultIndicator
            }{
                Glyphs.RightBracket
            }";

        var topRow =
            $"{
                Glyphs.ULCornerDbl
            }╡{
                title
            } - {
                stepTitle
            }╞{
                new (Glyphs.HLineDbl.ToString () [0], width - title.Length - stepTitle.Length - 7)
            }{
                Glyphs.URCornerDbl
            }";
        var row2 = $"{Glyphs.VLineDbl}{new (' ', width - 2)}{Glyphs.VLineDbl}";
        string row3 = row2;
        string row4 = row3;

        var separatorRow =
            $"{Glyphs.VLineDbl}{new (Glyphs.HLine.ToString () [0], width - 2)}{Glyphs.VLineDbl}";

        var buttonRow =
            $"{
                Glyphs.VLineDbl
            }{
                btnBack
            }{
                new (' ', width - btnBack.Length - btnNext.Length - 2)
            }{
                btnNext
            }{
                Glyphs.VLineDbl
            }";

        var bottomRow =
            $"{
                Glyphs.LLCornerDbl
            }{
                new (Glyphs.HLineDbl.ToString () [0], width - 2)
            }{
                Glyphs.LRCornerDbl
            }";

        var wizard = new Wizard { Title = title, Width = width, Height = height };
        wizard.AddStep (new() { Title = stepTitle });

        //wizard.LayoutSubViews ();
        var firstIteration = false;
        RunState runstate = Application.Begin (wizard);
        Application.RunIteration (ref runstate, firstIteration);

        // TODO: Disabled until Dim.Auto is used in Dialog
        //DriverAsserts.AssertDriverContentsWithFrameAre (
        //                                              $"{topRow}\n{row2}\n{row3}\n{row4}\n{separatorRow}\n{buttonRow}\n{bottomRow}",
        //                                              _output
        //                                             );
        Application.End (runstate);
        wizard.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]

    // this test is needed because Wizard overrides Dialog's title behavior ("Title - StepTitle")
    public void Setting_Title_Works ()
    {
        var d = (FakeDriver)Application.Driver;

        var title = "1234";
        var stepTitle = " - ABCD";

        var width = 40;
        var height = 4;
        d.SetBufferSize (width, height);

        var btnNextText = "Finish";

        var btnNext =
            $"{
                Glyphs.LeftBracket
            }{
                Glyphs.LeftDefaultIndicator
            } {
                btnNextText
            } {
                Glyphs.RightDefaultIndicator
            }{
                Glyphs.RightBracket
            }";

        var topRow =
            $"{
                Glyphs.ULCornerDbl
            }╡{
                title
            }{
                stepTitle
            }╞{
                new (Glyphs.HLineDbl.ToString () [0], width - title.Length - stepTitle.Length - 4)
            }{
                Glyphs.URCornerDbl
            }";

        var separatorRow =
            $"{Glyphs.VLineDbl}{new (Glyphs.HLine.ToString () [0], width - 2)}{Glyphs.VLineDbl}";

        // Once this is fixed, revert to commented out line: https://github.com/gui-cs/Terminal.Gui/issues/1791
        var buttonRow =
            $"{Glyphs.VLineDbl}{new (' ', width - btnNext.Length - 2)}{btnNext}{Glyphs.VLineDbl}";

        //var buttonRow = $"{Glyphs.VDLine}{new String (' ', width - btnNext.Length - 2)}{btnNext}{Glyphs.VDLine}";
        var bottomRow =
            $"{
                Glyphs.LLCornerDbl
            }{
                new (Glyphs.HLineDbl.ToString () [0], width - 2)
            }{
                Glyphs.LRCornerDbl
            }";

        var wizard = new Wizard { Title = title, Width = width, Height = height };
        wizard.AddStep (new() { Title = "ABCD" });

        Application.End (Application.Begin (wizard));
        wizard.Dispose ();
    }

    [Fact]

    // This test verifies that the 2nd step in a wizard with more than 2 steps 
    // shows the correct buttons on all steps
    // and that the title is correct
    public void ThreeStepWizard_Next_Shows_Steps ()
    {
        // verify step one

        // Next

        // verify step two

        // Back

        // verify step one again
    }

    [Fact]

    // This test verifies that the 2nd step in a wizard with 2 steps 
    // shows the correct buttons on both steps
    // and that the title is correct
    public void TwoStepWizard_Next_Shows_SecondStep ()
    {
        // verify step one

        // Next

        // verify step two

        // Back

        // verify step one again
    }

    // =========== WizardStep Tests

    [Fact]
    public void WizardStep_ButtonText ()
    {
        // Verify default button text

        // Verify set actually changes property

        // Verify set actually changes buttons for the current step
    }

    [Fact]
    public void WizardStep_Set_Title_Fires_TitleChanged ()
    {
        var r = new Window ();
        Assert.Equal (string.Empty, r.Title);

        var expected = string.Empty;
        r.TitleChanged += (s, args) => { Assert.Equal (r.Title, args.CurrentValue); };

        expected = "title";
        r.Title = expected;
        Assert.Equal (expected, r.Title);

        expected = "another title";
        r.Title = expected;
        Assert.Equal (expected, r.Title);
        r.Dispose ();
    }

    [Fact]
    public void WizardStep_Set_Title_Fires_TitleChanging ()
    {
        var r = new Window ();
        Assert.Equal (string.Empty, r.Title);

        var expectedAfter = string.Empty;
        var expectedDuring = string.Empty;
        var cancel = false;

        r.TitleChanging += (s, args) =>
                           {
                               Assert.Equal (expectedDuring, args.NewValue);
                               args.Cancel = cancel;
                           };

        r.Title = expectedDuring = expectedAfter = "title";
        Assert.Equal (expectedAfter, r.Title);

        r.Title = expectedDuring = expectedAfter = "a different title";
        Assert.Equal (expectedAfter, r.Title);

        // Now setup cancelling the change and change it back to "title"
        cancel = true;
        r.Title = expectedDuring = "title";
        Assert.Equal (expectedAfter, r.Title);
        r.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]

    // Verify a zero-step wizard doesn't crash and shows a blank wizard
    // and that the title is correct
    public void ZeroStepWizard_Shows ()
    {
        var d = (FakeDriver)Application.Driver;

        var title = "1234";
        var stepTitle = "";

        var width = 30;
        var height = 6;
        d.SetBufferSize (width, height);

        var btnBackText = "Back";
        var btnBack = $"{Glyphs.LeftBracket} {btnBackText} {Glyphs.RightBracket}";
        var btnNextText = "Finish";

        var btnNext =
            $"{
                Glyphs.LeftBracket
            }{
                Glyphs.LeftDefaultIndicator
            } {
                btnNextText
            } {
                Glyphs.RightDefaultIndicator
            }{
                Glyphs.RightBracket
            }";

        var topRow =
            $"{
                Glyphs.ULCornerDbl
            }╡{
                title
            }{
                stepTitle
            }╞{
                new (Glyphs.HLineDbl.ToString () [0], width - title.Length - stepTitle.Length - 4)
            }{
                Glyphs.URCornerDbl
            }";
        var row2 = $"{Glyphs.VLineDbl}{new (' ', width - 2)}{Glyphs.VLineDbl}";
        string row3 = row2;

        var separatorRow =
            $"{Glyphs.VLineDbl}{new (Glyphs.HLine.ToString () [0], width - 2)}{Glyphs.VLineDbl}";

        var buttonRow =
            $"{
                Glyphs.VLineDbl
            }{
                btnBack
            }{
                new (' ', width - btnBack.Length - btnNext.Length - 2)
            }{
                btnNext
            }{
                Glyphs.VLineDbl
            }";

        var bottomRow =
            $"{
                Glyphs.LLCornerDbl
            }{
                new (Glyphs.HLineDbl.ToString () [0], width - 2)
            }{
                Glyphs.LRCornerDbl
            }";

        var wizard = new Wizard { Title = title, Width = width, Height = height };
        RunState runstate = Application.Begin (wizard);

        // TODO: Disabled until Dim.Auto is used in Dialog
        //DriverAsserts.AssertDriverContentsWithFrameAre (
        //                                              $"{topRow}\n{row2}\n{row3}\n{separatorRow}\n{buttonRow}\n{bottomRow}",
        //                                              _output
        //                                             );
        Application.End (runstate);
        wizard.Dispose ();
    }
}
