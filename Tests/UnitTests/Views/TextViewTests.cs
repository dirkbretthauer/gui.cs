using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnitTests;
using Xunit.Abstractions;

namespace Terminal.Gui.ViewsTests;

public class TextViewTests
{
    private static TextView _textView;
    private readonly ITestOutputHelper _output;
    public TextViewTests (ITestOutputHelper output) { _output = output; }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void AllowsReturn_Setting_To_True_Changes_Multiline_To_True_If_It_Is_False ()
    {
        Assert.True (_textView.AllowsReturn);
        Assert.True (_textView.Multiline);
        Assert.Equal (4, _textView.TabWidth);
        Assert.True (_textView.AllowsTab);
        _textView.NewKeyDownEvent (Key.Enter);

        Assert.Equal (
                      Environment.NewLine + "TAB to jump between text fields.",
                      _textView.Text
                     );

        _textView.AllowsReturn = false;
        Assert.False (_textView.AllowsReturn);
        Assert.False (_textView.Multiline);
        Assert.Equal (0, _textView.TabWidth);
        Assert.False (_textView.AllowsTab);
        _textView.NewKeyDownEvent (Key.Enter);

        Assert.Equal (
                      Environment.NewLine + "TAB to jump between text fields.",
                      _textView.Text
                     );
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void AllowsTab_Setting_To_True_Changes_TabWidth_To_Default_If_It_Is_Zero ()
    {
        _textView.TabWidth = 0;
        Assert.Equal (0, _textView.TabWidth);
        Assert.True (_textView.AllowsTab);
        Assert.True (_textView.AllowsReturn);
        Assert.True (_textView.Multiline);
        _textView.AllowsTab = true;
        Assert.True (_textView.AllowsTab);
        Assert.Equal (4, _textView.TabWidth);
        Assert.True (_textView.AllowsReturn);
        Assert.True (_textView.Multiline);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void BackTab_Test_Follow_By_Tab ()
    {
        var top = new Toplevel ();
        top.Add (_textView);

        Application.Iteration += (s, a) =>
                                 {
                                     int width = _textView.Viewport.Width - 1;
                                     Assert.Equal (30, width + 1);
                                     Assert.Equal (10, _textView.Height);
                                     _textView.Text = "";

                                     for (var i = 0; i < 100; i++)
                                     {
                                         _textView.Text += "\t";
                                     }

                                     var col = 100;
                                     int tabWidth = _textView.TabWidth;
                                     int leftCol = _textView.LeftColumn;
                                     _textView.MoveEnd ();
                                     Assert.Equal (new (col, 0), _textView.CursorPosition);
                                     leftCol = GetLeftCol (leftCol);
                                     Assert.Equal (leftCol, _textView.LeftColumn);

                                     while (col > 0)
                                     {
                                         col--;
                                         _textView.NewKeyDownEvent (Key.Tab.WithShift);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     while (col < 100)
                                     {
                                         col++;
                                         _textView.NewKeyDownEvent (Key.Tab);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     Application.Top.Remove (_textView);
                                     Application.RequestStop ();
                                 };

        Application.Run (top);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void CanFocus_False_Wont_Focus_With_Mouse ()
    {
        Toplevel top = new ();
        var tv = new TextView { Width = Dim.Fill (), CanFocus = false, ReadOnly = true, Text = "some text" };

        var fv = new FrameView
        {
            Width = Dim.Fill (), Height = Dim.Fill (), CanFocus = false, Title = "I shouldn't get focus"
        };
        fv.Add (tv);
        top.Add (fv);

        Application.Begin (top);

        Assert.False (tv.CanFocus);
        Assert.False (tv.HasFocus);
        Assert.False (fv.CanFocus);
        Assert.False (fv.HasFocus);

        tv.NewMouseEvent (new() { Position = new (1, 0), Flags = MouseFlags.Button1DoubleClicked });

        Assert.Empty (tv.SelectedText);
        Assert.False (tv.CanFocus);
        Assert.False (tv.HasFocus);
        Assert.False (fv.CanFocus);
        Assert.False (fv.HasFocus);

        fv.CanFocus = true;
        tv.CanFocus = true;
        tv.NewMouseEvent (new() { Position = new (1, 0), Flags = MouseFlags.Button1DoubleClicked });

        Assert.Equal ("some ", tv.SelectedText);
        Assert.True (tv.CanFocus);
        Assert.True (tv.HasFocus);
        Assert.True (fv.CanFocus);
        Assert.True (fv.HasFocus);

        fv.CanFocus = false;
        tv.NewMouseEvent (new() { Position = new (1, 0), Flags = MouseFlags.Button1DoubleClicked });

        Assert.Equal ("some ", tv.SelectedText); // Setting CanFocus to false don't change the SelectedText
        Assert.True (tv.CanFocus); // v2: CanFocus is not longer automatically changed
        Assert.False (tv.HasFocus);
        Assert.False (fv.CanFocus);
        Assert.False (fv.HasFocus);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Changing_Selection_Or_CursorPosition_Update_SelectedLength_And_SelectedText ()
    {
        _textView.SelectionStartColumn = 2;
        _textView.SelectionStartRow = 0;
        Assert.Equal (0, _textView.CursorPosition.X);
        Assert.Equal (0, _textView.CursorPosition.Y);
        Assert.Equal (2, _textView.SelectedLength);
        Assert.Equal ("TA", _textView.SelectedText);
        _textView.CursorPosition = new (20, 0);
        Assert.Equal (2, _textView.SelectionStartColumn);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (18, _textView.SelectedLength);
        Assert.Equal ("B to jump between ", _textView.SelectedText);
    }

    [Fact]
    public void CloseFile_Throws_If_FilePath_Is_Null ()
    {
        var tv = new TextView ();
        Assert.Throws<ArgumentNullException> (() => tv.CloseFile ());
    }

    [Fact]
    public void ContentsChanged_Event_Fires_ClearHistoryChanges ()
    {
        var eventcount = 0;

        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 50, Height = 10, Text = text };
        tv.ContentsChanged += (s, e) => { eventcount++; };

        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);

        var expectedEventCount = 1; // for ENTER key
        Assert.Equal (expectedEventCount, eventcount);

        tv.ClearHistoryChanges ();
        expectedEventCount = 2;
        Assert.Equal (expectedEventCount, eventcount);
    }

    [Fact]
    public void ContentsChanged_Event_Fires_LoadStream_By_Calling_HistoryText_Clear ()
    {
        var eventcount = 0;

        var tv = new TextView { Width = 50, Height = 10 };
        tv.ContentsChanged += (s, e) => { eventcount++; };

        var text = "This is the first line.\r\nThis is the second line.\r\n";
        tv.Load (new MemoryStream (Encoding.ASCII.GetBytes (text)));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );

        Assert.Equal (1, eventcount);
    }

    [Fact]
    [AutoInitShutdown]
    public void ContentsChanged_Event_Fires_On_Init ()
    {
        Application.Iteration += (s, a) => { Application.RequestStop (); };

        var expectedRow = 0;
        var expectedCol = 0;
        var eventcount = 0;

        var tv = new TextView { Width = 50, Height = 10 };

        tv.ContentsChanged += (s, e) =>
                              {
                                  eventcount++;
                                  Assert.Equal (expectedRow, e.Row);
                                  Assert.Equal (expectedCol, e.Col);
                              };

        var top = new Toplevel ();
        top.Add (tv);
        Application.Begin (top);
        Assert.Equal (1, eventcount);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void ContentsChanged_Event_Fires_On_InsertText ()
    {
        var eventcount = 0;

        var tv = new TextView { Width = 50, Height = 10 };
        tv.CursorPosition = Point.Empty;

        tv.ContentsChanged += (s, e) => { eventcount++; };

        Assert.Equal (0, eventcount);

        tv.InsertText ("a");
        Assert.Equal (1, eventcount);

        tv.CursorPosition = Point.Empty;
        tv.InsertText ("bcd");
        Assert.Equal (4, eventcount);

        tv.InsertText ("e");
        Assert.Equal (5, eventcount);

        tv.InsertText ("\n");
        Assert.Equal (6, eventcount);

        tv.InsertText ("1234");
        Assert.Equal (10, eventcount);
    }

    [Fact]
    public void ContentsChanged_Event_Fires_On_LoadFile_By_Calling_HistoryText_Clear ()
    {
        var eventcount = 0;

        var tv = new TextView { Width = 50, Height = 10 };
        tv.BeginInit ();
        tv.EndInit ();

        tv.ContentsChanged += (s, e) => { eventcount++; };

        var fileName = "textview.txt";
        File.WriteAllText (fileName, "This is the first line.\r\nThis is the second line.\r\n");

        tv.Load (fileName);
        Assert.Equal (1, eventcount);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
    }

    [Fact]
    [AutoInitShutdown]
    public void ContentsChanged_Event_Fires_On_Set_Text ()
    {
        Application.Iteration += (s, a) => { Application.RequestStop (); };
        var eventcount = 0;

        var expectedRow = 0;
        var expectedCol = 0;

        var tv = new TextView
        {
            Width = 50,
            Height = 10,

            // you'd think col would be 3, but it's 0 because TextView sets
            // row/col = 0 when you set Text
            Text = "abc"
        };

        tv.ContentsChanged += (s, e) =>
                              {
                                  eventcount++;
                                  Assert.Equal (expectedRow, e.Row);
                                  Assert.Equal (expectedCol, e.Col);
                              };

        Assert.Equal ("abc", tv.Text);

        var top = new Toplevel ();
        top.Add (tv);
        RunState rs = Application.Begin (top);
        Assert.Equal (1, eventcount); // for Initialize

        expectedCol = 0;
        tv.Text = "defg";
        Assert.Equal (2, eventcount); // for set Text = "defg"
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void ContentsChanged_Event_Fires_On_Typing ()
    {
        Application.Iteration += (s, a) => { Application.RequestStop (); };
        var eventcount = 0;

        var expectedRow = 0;
        var expectedCol = 0;

        var tv = new TextView { Width = 50, Height = 10 };

        tv.ContentsChanged += (s, e) =>
                              {
                                  eventcount++;
                                  Assert.Equal (expectedRow, e.Row);
                                  Assert.Equal (expectedCol, e.Col);
                              };

        var top = new Toplevel ();
        top.Add (tv);
        RunState rs = Application.Begin (top);
        Assert.Equal (1, eventcount); // for Initialize

        expectedCol = 0;
        tv.Text = "ay";
        Assert.Equal (2, eventcount);

        expectedCol = 1;
        tv.NewKeyDownEvent (Key.Y.WithShift);
        Assert.Equal (3, eventcount);
        Assert.Equal ("Yay", tv.Text);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void ContentsChanged_Event_Fires_On_Undo_Redo ()
    {
        var eventcount = 0;
        var expectedEventCount = 0;

        _textView.ContentsChanged += (s, e) => { eventcount++; };

        expectedEventCount++;
        _textView.Text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount++;
        Assert.True (_textView.NewKeyDownEvent (Key.Enter));
        Assert.Equal (expectedEventCount, eventcount);

        // Undo
        expectedEventCount++;
        Assert.True (_textView.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal (expectedEventCount, eventcount);

        // Redo
        expectedEventCount++;
        Assert.True (_textView.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal (expectedEventCount, eventcount);

        // Undo
        expectedEventCount++;
        Assert.True (_textView.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal (expectedEventCount, eventcount);

        // Redo
        expectedEventCount++;
        Assert.True (_textView.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal (expectedEventCount, eventcount);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void ContentsChanged_Event_Fires_Using_Copy_Or_Cut_Tests ()
    {
        var eventcount = 0;

        _textView.ContentsChanged += (s, e) => { eventcount++; };

        var expectedEventCount = 1;

        // reset
        _textView.Text = TextViewTestsAutoInitShutdown.Txt;
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount += 3;
        Copy_Or_Cut_And_Paste_With_No_Selection ();
        Assert.Equal (expectedEventCount, eventcount);

        // reset
        expectedEventCount += 1;
        _textView.Text = TextViewTestsAutoInitShutdown.Txt;
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount += 3;
        Copy_Or_Cut_And_Paste_With_Selection ();
        Assert.Equal (expectedEventCount, eventcount);

        // reset
        expectedEventCount += 1;
        _textView.Text = TextViewTestsAutoInitShutdown.Txt;
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount += 1;
        Copy_Or_Cut_Not_Null_If_Has_Selection ();
        Assert.Equal (expectedEventCount, eventcount);

        // reset
        expectedEventCount += 1;
        _textView.Text = TextViewTestsAutoInitShutdown.Txt;
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount += 1;
        Copy_Or_Cut_Null_If_No_Selection ();
        Assert.Equal (expectedEventCount, eventcount);

        // reset
        expectedEventCount += 1;
        _textView.Text = TextViewTestsAutoInitShutdown.Txt;
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount += 4;
        Copy_Without_Selection ();
        Assert.Equal (expectedEventCount, eventcount);

        // reset
        expectedEventCount += 1;
        _textView.Text = TextViewTestsAutoInitShutdown.Txt;
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount += 4;
        Copy_Without_Selection ();
        Assert.Equal (expectedEventCount, eventcount);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void ContentsChanged_Event_Fires_Using_Kill_Delete_Tests ()
    {
        var eventcount = 0;

        _textView.ContentsChanged += (s, e) => { eventcount++; };

        var expectedEventCount = 1;
        Kill_Delete_WordForward ();
        Assert.Equal (expectedEventCount, eventcount); // for Initialize

        expectedEventCount += 1;
        Kill_Delete_WordBackward ();
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount += 2;
        Kill_To_End_Delete_Forwards_Copy_To_The_Clipboard_And_Paste ();
        Assert.Equal (expectedEventCount, eventcount);

        expectedEventCount += 2;
        Kill_To_Start_Delete_Backwards_Copy_To_The_Clipboard_And_Paste ();
        Assert.Equal (expectedEventCount, eventcount);
    }

    [Fact]
    [AutoInitShutdown]
    public void ContentsChanged_Event_NoFires_On_CursorPosition ()
    {
        var eventcount = 0;

        var tv = new TextView { Width = 50, Height = 10 };

        tv.ContentsChanged += (s, e) => { eventcount++; };
        Assert.Equal (0, eventcount);

        tv.CursorPosition = Point.Empty;

        Assert.Equal (0, eventcount);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Copy_Or_Cut_And_Paste_With_No_Selection ()
    {
        _textView.SelectionStartColumn = 20;
        _textView.SelectionStartRow = 0;
        _textView.CursorPosition = new (24, 0);
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        Assert.Equal ("text", _textView.SelectedText);
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        _textView.SelectionStartColumn = 0;
        _textView.SelectionStartRow = 0;
        Assert.Equal (new (24, 0), _textView.CursorPosition);
        Assert.True (_textView.IsSelecting);
        _textView.IsSelecting = false;
        _textView.NewKeyDownEvent (Key.Y.WithCtrl); // Paste
        Assert.Equal (new (28, 0), _textView.CursorPosition);
        Assert.False (_textView.IsSelecting);
        Assert.Equal ("TAB to jump between texttext fields.", _textView.Text);
        _textView.SelectionStartColumn = 24;
        _textView.SelectionStartRow = 0;
        _textView.NewKeyDownEvent (Key.W.WithCtrl); // Cut
        Assert.Equal (new (24, 0), _textView.CursorPosition);
        Assert.False (_textView.IsSelecting);
        Assert.Equal ("", _textView.SelectedText);
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        _textView.SelectionStartColumn = 0;
        _textView.SelectionStartRow = 0;
        _textView.IsSelecting = false;
        _textView.NewKeyDownEvent (Key.Y.WithCtrl); // Paste
        Assert.Equal (new (28, 0), _textView.CursorPosition);
        Assert.False (_textView.IsSelecting);
        Assert.Equal ("TAB to jump between texttext fields.", _textView.Text);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Copy_Or_Cut_And_Paste_With_Selection ()
    {
        _textView.SelectionStartColumn = 20;
        _textView.SelectionStartRow = 0;
        _textView.CursorPosition = new (24, 0);
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        Assert.Equal ("text", _textView.SelectedText);
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.Y.WithCtrl); // Paste
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        _textView.SelectionStartColumn = 20;
        _textView.SelectionStartRow = 0;
        _textView.NewKeyDownEvent (Key.W.WithCtrl); // Cut
        _textView.NewKeyDownEvent (Key.Y.WithCtrl); // Paste
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Copy_Or_Cut_Not_Null_If_Has_Selection ()
    {
        _textView.SelectionStartColumn = 20;
        _textView.SelectionStartRow = 0;
        _textView.CursorPosition = new (24, 0);
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        Assert.Equal ("text", _textView.SelectedText);
        _textView.NewKeyDownEvent (Key.W.WithCtrl); // Cut
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Copy_Or_Cut_Null_If_No_Selection ()
    {
        _textView.SelectionStartColumn = 0;
        _textView.SelectionStartRow = 0;
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        Assert.Equal ("", _textView.SelectedText);
        _textView.NewKeyDownEvent (Key.W.WithCtrl); // Cut
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Copy_Paste_Surrogate_Pairs ()
    {
        _textView.Text = "TextView with some more test text. Unicode shouldn't 𝔹Aℝ𝔽!";
        _textView.SelectAll ();
        _textView.Cut ();

        Assert.Equal (
                      "TextView with some more test text. Unicode shouldn't 𝔹Aℝ𝔽!",
                      Application.Driver?.Clipboard.GetClipboardData ()
                     );
        Assert.Equal (string.Empty, _textView.Text);
        _textView.Paste ();
        Assert.Equal ("TextView with some more test text. Unicode shouldn't 𝔹Aℝ𝔽!", _textView.Text);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Copy_Without_Selection ()
    {
        _textView.Text = "This is the first line.\nThis is the second line.\n";
        _textView.CursorPosition = new (0, _textView.Lines - 1);
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        _textView.NewKeyDownEvent (Key.Y.WithCtrl); // Paste

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}{Environment.NewLine}",
                      _textView.Text
                     );
        _textView.CursorPosition = new (3, 1);
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        _textView.NewKeyDownEvent (Key.Y.WithCtrl); // Paste

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the second line.{Environment.NewLine}{Environment.NewLine}",
                      _textView.Text
                     );
        Assert.Equal (new (3, 2), _textView.CursorPosition);
        _textView.NewKeyDownEvent (Key.Y.WithCtrl); // Paste

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the second line.{Environment.NewLine}{Environment.NewLine}",
                      _textView.Text
                     );
        Assert.Equal (new (3, 3), _textView.CursorPosition);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Cursor_Position_Multiline_False_Initialization ()
    {
        Assert.False (_textView.IsInitialized);
        Assert.True (_textView.Multiline);
        _textView.Multiline = false;
        Assert.Equal (32, _textView.CursorPosition.X);
        Assert.Equal (0, _textView.CursorPosition.Y);
        Assert.Equal (0, _textView.SelectedLength);
        Assert.Equal ("", _textView.SelectedText);
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void CursorPosition_With_Value_Greater_Than_Text_Length_Changes_To_Text_Length ()
    {
        _textView.CursorPosition = new (33, 1);
        Assert.Equal (32, _textView.CursorPosition.X);
        Assert.Equal (0, _textView.CursorPosition.Y);
        Assert.Equal (0, _textView.SelectedLength);
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void CursorPosition_With_Value_Less_Than_Zero_Changes_To_Zero ()
    {
        _textView.CursorPosition = new (-1, -1);
        Assert.Equal (0, _textView.CursorPosition.X);
        Assert.Equal (0, _textView.CursorPosition.Y);
        Assert.Equal (0, _textView.SelectedLength);
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Cut_Not_Allowed_If_ReadOnly_Is_True ()
    {
        _textView.ReadOnly = true;
        _textView.SelectionStartColumn = 20;
        _textView.SelectionStartRow = 0;
        _textView.CursorPosition = new (24, 0);
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        Assert.Equal ("text", _textView.SelectedText);

        _textView.NewKeyDownEvent (
                                   Key.W.WithCtrl
                                  ); // Selecting is set to false after Cut.
        Assert.Equal ("", _textView.SelectedText);
        _textView.ReadOnly = false;
        Assert.False (_textView.IsSelecting);
        _textView.IsSelecting = true; // Needed to set Selecting to true.
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        Assert.Equal ("text", _textView.SelectedText);
        _textView.NewKeyDownEvent (Key.W.WithCtrl); // Cut
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    [AutoInitShutdown]
    public void DeleteTextBackwards_WordWrap_False_Return_Undo ()
    {
        const string text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView { Width = Dim.Fill (), Height = Dim.Fill (), Text = text };
        string envText = tv.Text;
        var top = new Toplevel ();
        top.Add (tv);
        RunState rs = Application.Begin (top);
        Application.RunIteration (ref rs);

        Assert.False (tv.WordWrap);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is the first line. 
This is the second line.
",
                                                       _output
                                                      );

        tv.CursorPosition = new (3, 0);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.RunIteration (ref rs);
        Assert.Equal (new (2, 0), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.  
This is the second line.
",
                                                       _output
                                                      );

        tv.CursorPosition = new (0, 1);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.RunIteration (ref rs);
        Assert.Equal (new (22, 0), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.This is the second line.
",
                                                       _output
                                                      );

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Application.RunIteration (ref rs);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.  
This is the second line.
",
                                                       _output
                                                      );

        while (tv.Text != envText)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Application.RunIteration (ref rs);
        Assert.Equal (envText, tv.Text);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void DeleteTextBackwards_WordWrap_True_Return_Undo ()
    {
        const string text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView { Width = Dim.Fill (), Height = Dim.Fill (), Text = text, WordWrap = true };
        string envText = tv.Text;
        var top = new Toplevel ();
        top.Add (tv);
        RunState rs = Application.Begin (top);
        Application.RunIteration (ref rs);

        Assert.True (tv.WordWrap);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is the first line. 
This is the second line.
",
                                                       _output
                                                      );

        tv.CursorPosition = new (3, 0);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.RunIteration (ref rs);
        Assert.Equal (new (2, 0), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.  
This is the second line.
",
                                                       _output
                                                      );

        tv.CursorPosition = new (0, 1);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.RunIteration (ref rs);
        Assert.Equal (new (22, 0), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.This is the second line.
",
                                                       _output
                                                      );

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Application.RunIteration (ref rs);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.  
This is the second line.
",
                                                       _output
                                                      );

        while (tv.Text != envText)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Application.RunIteration (ref rs);

        Assert.Equal (envText, tv.Text);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void DeleteTextForwards_WordWrap_False_Return_Undo ()
    {
        const string text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView { Width = Dim.Fill (), Height = Dim.Fill (), Text = text };
        string envText = tv.Text;
        var top = new Toplevel ();
        top.Add (tv);
        RunState rs = Application.Begin (top);
        Application.RunIteration (ref rs);

        Assert.False (tv.WordWrap);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is the first line. 
This is the second line.
",
                                                       _output
                                                      );

        tv.CursorPosition = new (2, 0);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Delete));
        Application.RunIteration (ref rs);
        Assert.Equal (new (2, 0), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.  
This is the second line.
",
                                                       _output
                                                      );

        tv.CursorPosition = new (22, 0);
        Assert.Equal (new (22, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Delete));
        Application.RunIteration (ref rs);
        Assert.Equal (new (22, 0), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.This is the second line.
",
                                                       _output
                                                      );

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Application.RunIteration (ref rs);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.  
This is the second line.
",
                                                       _output
                                                      );

        while (tv.Text != envText)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (envText, tv.Text);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void DeleteTextForwards_WordWrap_True_Return_Undo ()
    {
        const string text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView { Width = Dim.Fill (), Height = Dim.Fill (), Text = text, WordWrap = true };
        string envText = tv.Text;
        var top = new Toplevel ();
        top.Add (tv);
        RunState rs = Application.Begin (top);
        Application.RunIteration (ref rs);

        Assert.True (tv.WordWrap);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is the first line. 
This is the second line.
",
                                                       _output
                                                      );

        tv.CursorPosition = new (2, 0);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Delete));
        Application.RunIteration (ref rs);
        Assert.Equal (new (2, 0), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.  
This is the second line.
",
                                                       _output
                                                      );

        tv.CursorPosition = new (22, 0);
        Assert.Equal (new (22, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Delete));
        Application.RunIteration (ref rs);
        Assert.Equal (new (22, 0), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.This is the second line.
",
                                                       _output
                                                      );

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Application.RunIteration (ref rs);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Ths is the first line.  
This is the second line.
",
                                                       _output
                                                      );

        while (tv.Text != envText)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Application.RunIteration (ref rs);

        Assert.Equal (envText, tv.Text);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void DesiredCursorVisibility_Horizontal_Navigation ()
    {
        var text = "";

        for (var i = 0; i < 12; i++)
        {
            text += $"{i.ToString () [^1]}";
        }

        var tv = new TextView { Width = 10, Height = 10 };
        tv.Text = text;
        var top = new Toplevel ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (0, tv.LeftColumn);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Application.PositionCursor ();
        Assert.Equal (CursorVisibility.Default, tv.CursorVisibility);

        for (var i = 0; i < 12; i++)
        {
            tv.NewMouseEvent (new() { Flags = MouseFlags.WheeledRight });
            Assert.Equal (Math.Min (i + 1, 11), tv.LeftColumn);
            Application.PositionCursor ();
            Application.Driver!.GetCursorVisibility (out CursorVisibility cursorVisibility);
            Assert.Equal (CursorVisibility.Invisible, cursorVisibility);
        }

        for (var i = 11; i > 0; i--)
        {
            tv.NewMouseEvent (new() { Flags = MouseFlags.WheeledLeft });
            Assert.Equal (i - 1, tv.LeftColumn);

            Application.PositionCursor ();
            Application.Driver!.GetCursorVisibility (out CursorVisibility cursorVisibility);

            if (i - 1 == 0)
            {
                Assert.Equal (CursorVisibility.Default, cursorVisibility);
            }
            else
            {
                Assert.Equal (CursorVisibility.Invisible, cursorVisibility);
            }
        }

        top.Dispose ();
        Application.Shutdown ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void DesiredCursorVisibility_Vertical_Navigation ()
    {
        var text = "";

        for (var i = 0; i < 12; i++)
        {
            text += $"This is the line {i}\n";
        }

        var tv = new TextView { Width = 10, Height = 10 };
        tv.Text = text;
        var top = new Toplevel ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (0, tv.TopRow);
        Application.PositionCursor ();
        Assert.Equal (CursorVisibility.Default, tv.CursorVisibility);

        for (var i = 0; i < 12; i++)
        {
            tv.NewMouseEvent (new() { Flags = MouseFlags.WheeledDown });
            Application.PositionCursor ();
            Assert.Equal (i + 1, tv.TopRow);
            Application.Driver!.GetCursorVisibility (out CursorVisibility cursorVisibility);
            Assert.Equal (CursorVisibility.Invisible, cursorVisibility);
        }

        for (var i = 12; i > 0; i--)
        {
            tv.NewMouseEvent (new() { Flags = MouseFlags.WheeledUp });
            Application.PositionCursor ();
            Assert.Equal (i - 1, tv.TopRow);

            Application.PositionCursor ();
            Application.Driver!.GetCursorVisibility (out CursorVisibility cursorVisibility);

            if (i - 1 == 0)
            {
                Assert.Equal (CursorVisibility.Default, cursorVisibility);
            }
            else
            {
                Assert.Equal (CursorVisibility.Invisible, cursorVisibility);
            }
        }

        top.Dispose ();
        Application.Shutdown ();
    }

    [Fact]
    public void GetRegion_StringFromRunes_Environment_NewLine ()
    {
        var tv = new TextView { Text = $"1{Environment.NewLine}2" };

        Assert.Equal ($"1{Environment.NewLine}2", tv.Text);
        Assert.Equal ("", tv.SelectedText);

        tv.SelectAll ();
        Assert.Equal ($"1{Environment.NewLine}2", tv.Text);
        Assert.Equal ($"1{Environment.NewLine}2", tv.SelectedText);
    }

    [Fact]
    public void HistoryText_ClearHistoryChanges ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        tv.ClearHistoryChanges ();

        Assert.Equal (
                      $"{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.False (tv.IsDirty);
        Assert.False (tv.HasHistoryChanges);
    }

    [Fact]
    public void HistoryText_Exceptions ()
    {
        var ht = new HistoryText ();

        foreach (object ls in Enum.GetValues (typeof (HistoryText.LineStatus)))
        {
            if ((HistoryText.LineStatus)ls != HistoryText.LineStatus.Original)
            {
                Assert.Throws<ArgumentException> (
                                                  () => ht.Add (
                                                                new() { new () },
                                                                Point.Empty,
                                                                (HistoryText.LineStatus)ls
                                                               )
                                                 );
            }
        }

        Assert.Null (Record.Exception (() => ht.Add (new() { new () }, Point.Empty)));
    }

    [Fact]
    public void HistoryText_IsDirty_HasHistoryChanges ()
    {
        var tv = new TextView ();

        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);
        Assert.False (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.D1));
        Assert.Equal ("1", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"1{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.D2));
        Assert.Equal ($"1{Environment.NewLine}2", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Assert.Equal ($"1{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Assert.Equal ("1", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // IsDirty cannot be based on HasHistoryChanges because HasHistoryChanges is greater than 0
        // The only way is comparing from the original text
        Assert.False (tv.IsDirty);

        // Still true because HasHistoryChanges is greater than 0
        Assert.True (tv.HasHistoryChanges);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Changing_On_Middle_Clear_History_Forwards ()
    {
        var tv = new TextView ();

        Assert.True (tv.NewKeyDownEvent (Key.D1));
        Assert.Equal ("1", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.D2));
        Assert.Equal ("12", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.D3));
        Assert.Equal ("123", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("12", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.D4));
        Assert.Equal ("124", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("124", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown (useFakeClipboard: true)]
    public void HistoryText_Undo_Redo_Copy_Without_Selection_Multi_Line_Paste ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.CursorPosition = new (23, 0);

        Assert.True (tv.NewKeyDownEvent (Key.C.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal ("This is the first line.", Clipboard.Contents);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (23, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 0), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (23, 1), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Cut_Multi_Line_Another_Selected_Paste ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (17, 0);

        Assert.True (tv.NewKeyDownEvent (Key.W.WithCtrl));

        Assert.Equal (
                      $"This is the  line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (12, 0), tv.CursorPosition);

        tv.SelectionStartColumn = 12;
        tv.SelectionStartRow = 1;
        tv.CursorPosition = new (18, 1);

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"This is the  line.{Environment.NewLine}This is the first line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the  line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (12, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (12, 0), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the  line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (12, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the  line.{Environment.NewLine}This is the first line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 1), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Cut_Multi_Line_Selected_Paste ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (17, 0);

        Assert.True (tv.NewKeyDownEvent (Key.W.WithCtrl));

        Assert.Equal (
                      $"This is the  line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (12, 0), tv.CursorPosition);

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (11, 1);

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));
        Assert.Equal ($"This is the first second line.{Environment.NewLine}This is the third line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the  line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (12, 0), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the first second line.{Environment.NewLine}This is the third line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Cut_Simple_Paste_Starting ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (18, 1);

        Assert.True (tv.NewKeyDownEvent (Key.W.WithCtrl));
        Assert.Equal ($"This is the  line.{Environment.NewLine}This is the third line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (12, 0), tv.CursorPosition);

        tv.IsSelecting = false;

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"This is the  line.{Environment.NewLine}This is the third line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (12, 0), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Disabled_On_WordWrap ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.\n";
        var tv = new TextView { Width = 80, Height = 5, Text = text };

        Assert.False (tv.WordWrap);
        tv.WordWrap = true;

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (12, 2);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"This is the {Environment.NewLine}third line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.A));
        Assert.Equal ($"This is the {Environment.NewLine}athird line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine}third line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine}athird line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Empty_Copy_Without_Selection_Multi_Line_Selected_Paste ()
    {
        var text = "\nThis is the first line.\nThis is the second line.";
        var tv = new TextView { Text = text };

        Assert.True (tv.NewKeyDownEvent (Key.C.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.",
                      tv.Text
                     );
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}{Environment.NewLine}This is the first line.{Environment.NewLine}This is the second line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Ending_With_Newline_Multi_Line_Selected_Almost_All_Return_And_InsertText ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.\n";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (12, 2);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"This is the {Environment.NewLine}third line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.A));
        Assert.Equal ($"This is the {Environment.NewLine}athird line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine}third line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.{Environment.NewLine}",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (12, 2), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine}third line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine}athird line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine}third line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.{Environment.NewLine}",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (12, 2), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine}third line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine}athird line.{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_First_Line_Selected_Return_And_InsertText ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (17, 0);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.A));

        Assert.Equal (
                      $"This is the {Environment.NewLine}a line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine}a line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine}a line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_KillToEndOfLine ()
    {
        var text = "First line.\nSecond line.";
        var tv = new TextView { Text = text };

        Assert.True (tv.NewKeyDownEvent (Key.K.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal ("First line.", Clipboard.Contents);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.K.WithCtrl));
        Assert.Equal ("Second line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal ($"First line.{Environment.NewLine}", Clipboard.Contents);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.K.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal ($"First line.{Environment.NewLine}Second line.", Clipboard.Contents);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("Second line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("Second line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_KillToLeftStart ()
    {
        var text = "First line.\nSecond line.";
        var tv = new TextView { Text = text };

        Assert.True (tv.NewKeyDownEvent (Key.End.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (12, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl.WithShift));
        Assert.Equal ($"First line.{Environment.NewLine}", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal ("Second line.", Clipboard.Contents);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl.WithShift));
        Assert.Equal ("First line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal ($"Second line.{Environment.NewLine}", Clipboard.Contents);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (11, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl.WithShift));
        Assert.Equal ("", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal ($"Second line.{Environment.NewLine}First line.", Clipboard.Contents);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("First line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (11, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (12, 1), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("First line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (11, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_KillWordBackward ()
    {
        var text = "First line.\nSecond line.";
        var tv = new TextView { Text = text };

        Assert.True (tv.NewKeyDownEvent (Key.End.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (12, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (11, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second ", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl));
        Assert.Equal ("First line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (11, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl));
        Assert.Equal ("First line", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (10, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl));
        Assert.Equal ("First ", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (6, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("First ", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (6, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("First line", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (10, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("First line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (11, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second ", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (11, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (12, 1), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (11, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second ", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("First line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (11, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("First line", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (10, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("First ", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (6, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_KillWordForward ()
    {
        var text = "First line.\nSecond line.";
        var tv = new TextView { Text = text };

        Assert.True (tv.NewKeyDownEvent (Key.Delete.WithCtrl));
        Assert.Equal ($"line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Delete.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Delete.WithCtrl));
        Assert.Equal ("Second line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Delete.WithCtrl));
        Assert.Equal ("line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Delete.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("Second line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"line.{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}Second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("Second line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_DeleteCharLeft ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var ntimes = 3;
        tv.CursorPosition = new (7, 1);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharLeft ();
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 1), tv.CursorPosition);

        tv.CursorPosition = new (7, 0);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharLeft ();
        }

        Assert.Equal (
                      $"This the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 0), tv.CursorPosition);

        tv.CursorPosition = new (7, 2);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharLeft ();
        }

        Assert.Equal (
                      $"This the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 2), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

            switch (i)
            {
                case 0:
                    Assert.Equal (
                                  $"This the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This  the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (5, 2), tv.CursorPosition);

                    break;
                case 1:
                    Assert.Equal (
                                  $"This the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This i the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (6, 2), tv.CursorPosition);

                    break;
                case 2:
                    Assert.Equal (
                                  $"This the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (7, 2), tv.CursorPosition);

                    break;
            }
        }

        Assert.Equal (
                      $"This the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 2), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

            switch (i)
            {
                case 0:
                    Assert.Equal (
                                  $"This  the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (5, 0), tv.CursorPosition);

                    break;
                case 1:
                    Assert.Equal (
                                  $"This i the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (6, 0), tv.CursorPosition);

                    break;
                case 2:
                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (7, 0), tv.CursorPosition);

                    break;
            }
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

            switch (i)
            {
                case 0:
                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This  the second line.{Environment.NewLine}This is the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (5, 1), tv.CursorPosition);

                    break;
                case 1:
                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This i the second line.{Environment.NewLine}This is the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (6, 1), tv.CursorPosition);

                    break;
                case 2:
                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (7, 1), tv.CursorPosition);

                    break;
            }
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 1), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 0), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 2), tv.CursorPosition);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_DeleteCharRight ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var ntimes = 3;
        tv.CursorPosition = new (7, 1);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharRight ();
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        tv.CursorPosition = new (7, 0);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharRight ();
        }

        Assert.Equal (
                      $"This ise first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);

        tv.CursorPosition = new (7, 2);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharRight ();
        }

        Assert.Equal (
                      $"This ise first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This ise third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 2), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This ise first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 2), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This ise first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This ise first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This ise third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 2), tv.CursorPosition);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_InsertText ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var messy = " messy";
        tv.CursorPosition = new (7, 1);
        tv.InsertText (messy);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 1), tv.CursorPosition);

        tv.CursorPosition = new (7, 0);
        tv.InsertText (messy);

        Assert.Equal (
                      $"This is messy the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 0), tv.CursorPosition);

        tv.CursorPosition = new (7, 2);
        tv.InsertText (messy);

        Assert.Equal (
                      $"This is messy the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is messy the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 2), tv.CursorPosition);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is messy the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 2), tv.CursorPosition);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 1), tv.CursorPosition);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is messy the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 0), tv.CursorPosition);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is messy the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is messy the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 2), tv.CursorPosition);
        top.Dispose ();
    }

    [Fact]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_All_Return_And_InsertText ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        Assert.True (tv.NewKeyDownEvent (Key.End.WithCtrl.WithShift));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 2), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.A));
        Assert.Equal ($"{Environment.NewLine}a", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}a", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"{Environment.NewLine}a", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_Copy_Simple_Paste_Starting_On_Letter ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (18, 1);

        Assert.True (tv.NewKeyDownEvent (Key.C.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal ($"first line.{Environment.NewLine}This is the second", tv.SelectedText);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);

        tv.IsSelecting = false;
        tv.CursorPosition = new (17, 1);

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the seconfirst line.{Environment.NewLine}This is the secondd line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (18, 2), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 1), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the seconfirst line.{Environment.NewLine}This is the secondd line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (18, 2), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown (useFakeClipboard: true)]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_Copy_Simple_Paste_Starting_On_Space ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (18, 1);

        Assert.True (tv.NewKeyDownEvent (Key.C.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal ($"first line.{Environment.NewLine}This is the second", tv.SelectedText);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);

        tv.IsSelecting = false;

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the secondfirst line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (18, 2), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the secondfirst line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (18, 2), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_DeleteCharLeft_All ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);
        Assert.False (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.End.WithCtrl.WithShift));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.SelectedText
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.Equal (70 + Environment.NewLine.Length * 2, tv.SelectedLength);
        Assert.False (tv.IsDirty);
        Assert.False (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Assert.Equal ("", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.False (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_DeleteCharRight_All ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);
        Assert.False (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.End.WithCtrl.WithShift));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.SelectedText
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.Equal (70 + Environment.NewLine.Length * 2, tv.SelectedLength);
        Assert.False (tv.IsDirty);
        Assert.False (tv.HasHistoryChanges);

        Assert.True (tv.NewKeyDownEvent (Key.Delete));
        Assert.Equal ("", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.False (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);
        Assert.True (tv.HasHistoryChanges);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_InsertText ()
    {
        var text =
            $"This is the first line.{Environment.NewLine}This is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var messy = " messy";
        tv.CursorPosition = new (7, 0);
        tv.SelectionStartColumn = 11;
        tv.SelectionStartRow = 2;
        Assert.Equal (51 + Environment.NewLine.Length * 2, tv.SelectedLength);

        for (var i = 0; i < messy.Length; i++)
        {
            tv.InsertText (messy [i].ToString ());

            switch (i)
            {
                case 0:
                    Assert.Equal ("This is  third line.", tv.Text);
                    Assert.Equal (new (8, 0), tv.CursorPosition);

                    break;
                case 1:
                    Assert.Equal ("This is m third line.", tv.Text);
                    Assert.Equal (new (9, 0), tv.CursorPosition);

                    break;
                case 2:
                    Assert.Equal ("This is me third line.", tv.Text);
                    Assert.Equal (new (10, 0), tv.CursorPosition);

                    break;
                case 3:
                    Assert.Equal ("This is mes third line.", tv.Text);
                    Assert.Equal (new (11, 0), tv.CursorPosition);

                    break;
                case 4:
                    Assert.Equal ("This is mess third line.", tv.Text);
                    Assert.Equal (new (12, 0), tv.CursorPosition);

                    break;
                case 5:
                    Assert.Equal ("This is messy third line.", tv.Text);
                    Assert.Equal (new (13, 0), tv.CursorPosition);

                    break;
            }
        }

        Assert.Equal ("This is messy third line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (13, 0), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (2, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

            switch (i)
            {
                case 0:
                    Assert.Equal ("This is mess third line.", tv.Text);
                    Assert.Equal (new (12, 0), tv.CursorPosition);

                    break;
                case 1:
                    Assert.Equal ("This is mes third line.", tv.Text);
                    Assert.Equal (new (11, 0), tv.CursorPosition);

                    break;
                case 2:
                    Assert.Equal ("This is me third line.", tv.Text);
                    Assert.Equal (new (10, 0), tv.CursorPosition);

                    break;
                case 3:
                    Assert.Equal ("This is m third line.", tv.Text);
                    Assert.Equal (new (9, 0), tv.CursorPosition);

                    break;
                case 4:
                    Assert.Equal ("This is  third line.", tv.Text);
                    Assert.Equal (new (8, 0), tv.CursorPosition);

                    break;
                case 5:
                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                                  tv.Text
                                 );
                    Assert.Equal (new (7, 0), tv.CursorPosition);

                    break;
            }
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (2, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

            switch (i)
            {
                case 0:
                    Assert.Equal ("This is  third line.", tv.Text);
                    Assert.Equal (new (8, 0), tv.CursorPosition);

                    break;
                case 1:
                    Assert.Equal ("This is m third line.", tv.Text);
                    Assert.Equal (new (9, 0), tv.CursorPosition);

                    break;
                case 2:
                    Assert.Equal ("This is me third line.", tv.Text);
                    Assert.Equal (new (10, 0), tv.CursorPosition);

                    break;
                case 3:
                    Assert.Equal ("This is mes third line.", tv.Text);
                    Assert.Equal (new (11, 0), tv.CursorPosition);

                    break;
                case 4:
                    Assert.Equal ("This is mess third line.", tv.Text);
                    Assert.Equal (new (12, 0), tv.CursorPosition);

                    break;
                case 5:
                    Assert.Equal ("This is messy third line.", tv.Text);
                    Assert.Equal (new (13, 0), tv.CursorPosition);

                    break;
            }
        }

        Assert.Equal ("This is messy third line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (13, 0), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (2, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_InsertText_Twice_On_Same_Line ()
    {
        var text = "One\nTwo\nThree";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);

        tv.SelectionStartColumn = 0;
        tv.SelectionStartRow = 0;
        tv.CursorPosition = new (0, 1);
        Assert.Equal (3 + Environment.NewLine.Length, tv.SelectedLength);

        Assert.True (tv.NewKeyDownEvent (Key.D1));
        Assert.Equal ($"1Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        tv.SelectionStartColumn = 1;
        tv.SelectionStartRow = 0;
        tv.CursorPosition = new (1, 1);
        Assert.Equal (4 + Environment.NewLine.Length, tv.SelectedLength);

        Assert.True (tv.NewKeyDownEvent (Key.D2));
        Assert.Equal ("12hree", tv.Text);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"1Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.False (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"1Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("12hree", tv.Text);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_InsertText_Twice_On_Same_Line_With_End_Line ()
    {
        var text = "One\nTwo\nThree\n";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (4, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);

        tv.SelectionStartColumn = 0;
        tv.SelectionStartRow = 0;
        tv.CursorPosition = new (0, 1);
        Assert.Equal (3 + Environment.NewLine.Length, tv.SelectedLength);

        Assert.True (tv.NewKeyDownEvent (Key.D1));
        Assert.Equal ($"1Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        tv.SelectionStartColumn = 1;
        tv.SelectionStartRow = 0;
        tv.CursorPosition = new (1, 1);
        Assert.Equal (4 + Environment.NewLine.Length, tv.SelectedLength);

        Assert.True (tv.NewKeyDownEvent (Key.D2));
        Assert.Equal ($"12hree{Environment.NewLine}", tv.Text);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"1Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.False (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"1Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"12hree{Environment.NewLine}", tv.Text);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_Selected_With_Empty_Text ()
    {
        var tv = new TextView { Width = 10, Height = 2 };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.O.WithShift));
        Assert.Equal ("O", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.N));
        Assert.Equal ("On", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.E));
        Assert.Equal ("One", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"One{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.T.WithShift));
        Assert.Equal ($"One{Environment.NewLine}T", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.W));
        Assert.Equal ($"One{Environment.NewLine}Tw", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (2, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.O));
        Assert.Equal ($"One{Environment.NewLine}Two", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (3, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.T.WithShift));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}T", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.H));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Th", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (2, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thr", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (3, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.E));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thre", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.E));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (5, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 3), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        tv.SelectionStartColumn = 0;
        tv.SelectionStartRow = 0;
        tv.CursorPosition = new (0, 1);
        Assert.Equal (3 + Environment.NewLine.Length, tv.SelectedLength);

        Assert.True (tv.NewKeyDownEvent (Key.D1));
        Assert.Equal ($"1Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        tv.SelectionStartColumn = 1;
        tv.SelectionStartRow = 0;
        tv.CursorPosition = new (1, 1);
        Assert.Equal (4 + Environment.NewLine.Length, tv.SelectedLength);

        Assert.True (tv.NewKeyDownEvent (Key.D2));
        Assert.Equal ($"12hree{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        // Undoing
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"1Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (5, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thre", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thr", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (3, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Th", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (2, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}T", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (3, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Tw", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (2, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}T", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("One", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("On", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("O", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.False (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.False (tv.IsDirty);

        // Redoing
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("O", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("On", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("One", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}T", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Tw", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (2, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (3, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}T", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Th", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (2, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thr", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (3, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thre", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (5, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 3), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"1Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"12hree{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.True (tv.IsDirty);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Multi_Line_With_Empty_Text ()
    {
        var tv = new TextView { Width = 10, Height = 2 };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.O.WithShift));
        Assert.Equal ("O", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.N));
        Assert.Equal ("On", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.E));
        Assert.Equal ("One", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"One{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.T.WithShift));
        Assert.Equal ($"One{Environment.NewLine}T", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.W));
        Assert.Equal ($"One{Environment.NewLine}Tw", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (2, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.O));
        Assert.Equal ($"One{Environment.NewLine}Two", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (3, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.T.WithShift));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}T", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.H));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Th", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (2, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thr", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (3, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.E));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thre", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.E));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (5, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 3), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        // Undoing
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (5, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thre", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thr", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (3, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Th", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (2, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}T", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (3, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Tw", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (2, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}T", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("One", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("On", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("O", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redoing
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("O", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("On", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (2, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("One", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (3, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}T", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (1, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Tw", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (2, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (3, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}T", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Th", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (2, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thr", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (3, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Thre", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (5, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 3), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"One{Environment.NewLine}Two{Environment.NewLine}Three{Environment.NewLine}", tv.Text);
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 3), tv.CursorPosition);
        Assert.True (tv.IsDirty);
        top.Dispose ();
    }

    [Fact]
    public void HistoryText_Undo_Redo_Multiline_Selected_Tab_BackTab ()
    {
        var text = "First line.\nSecond line.\nThird line.";
        var tv = new TextView { Width = 80, Height = 5, Text = text };

        tv.SelectionStartColumn = 6;
        tv.CursorPosition = new (6, 2);

        Assert.True (tv.NewKeyDownEvent (Key.Tab));
        Assert.Equal ("First \tline.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Tab.WithShift));
        Assert.Equal ("First line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (6, 0), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ("First \tline.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.{Environment.NewLine}Third line.", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (6, 2), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("First \tline.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (7, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ("First line.", tv.Text);
        Assert.Equal (1, tv.Lines);
        Assert.Equal (new (6, 0), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Multiline_Simples_Tab_BackTab ()
    {
        var text = "First line.\nSecond line.\nThird line.";
        var tv = new TextView { Width = 80, Height = 5, Text = text };

        Assert.True (tv.NewKeyDownEvent (Key.Tab));

        Assert.Equal (
                      $"\tFirst line.{Environment.NewLine}Second line.{Environment.NewLine}Third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Tab.WithShift));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.{Environment.NewLine}Third line.", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"\tFirst line.{Environment.NewLine}Second line.{Environment.NewLine}Third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.{Environment.NewLine}Third line.", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"\tFirst line.{Environment.NewLine}Second line.{Environment.NewLine}Third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (1, 0), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"First line.{Environment.NewLine}Second line.{Environment.NewLine}Third line.", tv.Text);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Setting_Clipboard_Multi_Line_Selected_Paste ()
    {
        var text = "This is the first line.\nThis is the second line.";
        var tv = new TextView { Text = text };

        Clipboard.Contents = "Inserted\nNewLine";

        Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.", tv.Text);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"Inserted{Environment.NewLine}NewLineThis is the first line.{Environment.NewLine}This is the second line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"Inserted{Environment.NewLine}NewLineThis is the first line.{Environment.NewLine}This is the second line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown (useFakeClipboard: true)]
    public void HistoryText_Undo_Redo_Simple_Copy_Multi_Line_Selected_Paste ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (17, 0);

        Assert.True (tv.NewKeyDownEvent (Key.C.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal ("first", tv.SelectedText);
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (11, 1);

        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));
        Assert.Equal ($"This is the first second line.{Environment.NewLine}This is the third line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (12, 0), tv.CursorPosition);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the first second line.{Environment.NewLine}This is the third line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Single_Line_DeleteCharLeft ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var ntimes = 3;
        tv.CursorPosition = new (7, 1);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharLeft ();
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 1), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (4, 1), tv.CursorPosition);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Single_Line_DeleteCharRight ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var ntimes = 3;
        tv.CursorPosition = new (7, 1);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharRight ();
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This ise second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Single_Line_InsertText ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var messy = " messy";
        tv.CursorPosition = new (7, 1);
        tv.InsertText (messy);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 1), tv.CursorPosition);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is messy the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 1), tv.CursorPosition);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Single_Line_Selected_DeleteCharLeft ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var ntimes = 3;
        tv.CursorPosition = new (7, 1);
        tv.SelectionStartColumn = 11;
        tv.SelectionStartRow = 1;
        Assert.Equal (4, tv.SelectedLength);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharLeft ();
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This  second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (5, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This  second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (5, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Single_Line_Selected_DeleteCharRight ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var ntimes = 3;
        tv.CursorPosition = new (7, 1);
        tv.SelectionStartColumn = 11;
        tv.SelectionStartRow = 1;
        Assert.Equal (4, tv.SelectedLength);

        for (var i = 0; i < ntimes; i++)
        {
            tv.DeleteCharRight ();
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This isecond line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);

        for (var i = 0; i < ntimes; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This isecond line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HistoryText_Undo_Redo_Single_Line_Selected_InsertText ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        var messy = " messy";
        tv.CursorPosition = new (7, 1);
        tv.SelectionStartColumn = 11;
        tv.SelectionStartRow = 1;
        Assert.Equal (4, tv.SelectedLength);
        tv.InsertText (messy);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is messy second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (7, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);

        for (var i = 0; i < messy.Length; i++)
        {
            Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        }

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is messy second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (13, 1), tv.CursorPosition);
        Assert.Equal (11, tv.SelectionStartColumn);
        Assert.Equal (1, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);
        top.Dispose ();
    }

    [Fact]
    public void HistoryText_Undo_Redo_Single_Line_Selected_Return ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (17, 0);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Single_Second_Line_Selected_Return ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.SelectionStartRow = 1;
        tv.CursorPosition = new (18, 1);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Single_Second_Line_Selected_Return_And_InsertText ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.SelectionStartRow = 1;
        tv.CursorPosition = new (18, 1);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.A));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine}a line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine}a line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (0, 2), tv.CursorPosition);

        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the {Environment.NewLine}a line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (4, tv.Lines);
        Assert.Equal (new (1, 2), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Three_Line_Selected_Return ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (17, 2);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));
        Assert.Equal ($"This is the {Environment.NewLine} line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 2), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine} line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (17, 2), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        Assert.Equal ($"This is the {Environment.NewLine} line.", tv.Text);
        Assert.Equal (2, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_Two_Line_Selected_Return ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (18, 1);

        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        Assert.Equal (
                      $"This is the {Environment.NewLine} line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (new (0, 1), tv.CursorPosition);
    }

    [Fact]
    public void HistoryText_Undo_Redo_ApplyCellsAttribute ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Text = text };

        tv.SelectionStartColumn = 12;
        tv.CursorPosition = new (18, 1);

        if (Environment.NewLine.Length == 2)
        {
            Assert.Equal (31, tv.SelectedLength);
        }
        else
        {
            Assert.Equal (30, tv.SelectedLength);
        }

        Assert.Equal ($"first line.{Environment.NewLine}This is the second", tv.SelectedText);
        Assert.Equal ($"first line.{Environment.NewLine}This is the second", Cell.ToString (tv.SelectedCellsList));
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        AssertNullAttribute ();

        tv.ApplyCellsAttribute (new (Color.Red, Color.Green));

        AssertRedGreenAttribute ();

        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.Equal ($"first line.{Environment.NewLine}This is the second", Cell.ToString (tv.SelectedCellsList));
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        // Undo
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));

        AssertNullAttribute ();

        Assert.Equal (12, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.Empty (tv.SelectedCellsList);
        Assert.Equal (new (12, 0), tv.CursorPosition);
        Assert.False (tv.IsDirty);

        // Redo
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));

        AssertRedGreenAttribute ();

        Assert.Equal (12, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.Empty (tv.SelectedCellsList);
        Assert.Equal (new (12, 0), tv.CursorPosition);
        Assert.True (tv.IsDirty);

        void AssertNullAttribute ()
        {
            tv.GetRegion (out List<List<Cell>> region, 0, 12, 1, 18);

            foreach (List<Cell> cells in region)
            {
                foreach (Cell cell in cells)
                {
                    Assert.Null (cell.Attribute);
                }
            }
        }

        void AssertRedGreenAttribute ()
        {
            tv.GetRegion (out List<List<Cell>> region, 0, 12, 1, 18);

            foreach (List<Cell> cells in region)
            {
                foreach (Cell cell in cells)
                {
                    Assert.Equal ("[Red,Green]", cell.Attribute.ToString ());
                }
            }
        }
    }

    [Fact]
    public void Internal_Tests ()
    {
        var txt = "This is a text.";
        List<Cell> txtRunes = Cell.StringToCells (txt);
        Assert.Equal (txt.Length, txtRunes.Count);
        Assert.Equal ('T', txtRunes [0].Rune.Value);
        Assert.Equal ('h', txtRunes [1].Rune.Value);
        Assert.Equal ('i', txtRunes [2].Rune.Value);
        Assert.Equal ('s', txtRunes [3].Rune.Value);
        Assert.Equal (' ', txtRunes [4].Rune.Value);
        Assert.Equal ('i', txtRunes [5].Rune.Value);
        Assert.Equal ('s', txtRunes [6].Rune.Value);
        Assert.Equal (' ', txtRunes [7].Rune.Value);
        Assert.Equal ('a', txtRunes [8].Rune.Value);
        Assert.Equal (' ', txtRunes [9].Rune.Value);
        Assert.Equal ('t', txtRunes [10].Rune.Value);
        Assert.Equal ('e', txtRunes [11].Rune.Value);
        Assert.Equal ('x', txtRunes [12].Rune.Value);
        Assert.Equal ('t', txtRunes [13].Rune.Value);
        Assert.Equal ('.', txtRunes [^1].Rune.Value);

        var col = 0;
        Assert.True (TextModel.SetCol (ref col, 80, 79));
        Assert.False (TextModel.SetCol (ref col, 80, 80));
        Assert.Equal (79, col);

        var start = 0;
        var x = 8;
        Assert.Equal (8, TextModel.GetColFromX (txtRunes, start, x));
        Assert.Equal ('a', txtRunes [start + x].Rune.Value);
        start = 1;
        x = 7;
        Assert.Equal (7, TextModel.GetColFromX (txtRunes, start, x));
        Assert.Equal ('a', txtRunes [start + x].Rune.Value);

        Assert.Equal ((15, 15), TextModel.DisplaySize (txtRunes));
        Assert.Equal ((6, 6), TextModel.DisplaySize (txtRunes, 1, 7));

        Assert.Equal (0, TextModel.CalculateLeftColumn (txtRunes, 0, 7, 8));
        Assert.Equal (1, TextModel.CalculateLeftColumn (txtRunes, 0, 8, 8));
        Assert.Equal (2, TextModel.CalculateLeftColumn (txtRunes, 0, 9, 8));

        var tm = new TextModel ();
        tm.AddLine (0, Cell.StringToCells ("This is first line."));
        tm.AddLine (1, Cell.StringToCells ("This is last line."));
        Assert.Equal ((new Point (2, 0), true), tm.FindNextText ("is", out bool gaveFullTurn));
        Assert.False (gaveFullTurn);
        Assert.Equal ((new Point (5, 0), true), tm.FindNextText ("is", out gaveFullTurn));
        Assert.False (gaveFullTurn);
        Assert.Equal ((new Point (2, 1), true), tm.FindNextText ("is", out gaveFullTurn));
        Assert.False (gaveFullTurn);
        Assert.Equal ((new Point (5, 1), true), tm.FindNextText ("is", out gaveFullTurn));
        Assert.False (gaveFullTurn);
        Assert.Equal ((new Point (2, 0), true), tm.FindNextText ("is", out gaveFullTurn));
        Assert.True (gaveFullTurn);
        tm.ResetContinuousFind (Point.Empty);
        Assert.Equal ((new Point (5, 1), true), tm.FindPreviousText ("is", out gaveFullTurn));
        Assert.False (gaveFullTurn);
        Assert.Equal ((new Point (2, 1), true), tm.FindPreviousText ("is", out gaveFullTurn));
        Assert.False (gaveFullTurn);
        Assert.Equal ((new Point (5, 0), true), tm.FindPreviousText ("is", out gaveFullTurn));
        Assert.False (gaveFullTurn);
        Assert.Equal ((new Point (2, 0), true), tm.FindPreviousText ("is", out gaveFullTurn));
        Assert.False (gaveFullTurn);
        Assert.Equal ((new Point (5, 1), true), tm.FindPreviousText ("is", out gaveFullTurn));
        Assert.True (gaveFullTurn);

        Assert.Equal ((new Point (9, 1), true), tm.ReplaceAllText ("is", false, false, "really"));
        Assert.Equal (Cell.StringToCells ("Threally really first line."), tm.GetLine (0));
        Assert.Equal (Cell.StringToCells ("Threally really last line."), tm.GetLine (1));
        tm = new ();
        tm.AddLine (0, Cell.StringToCells ("This is first line."));
        tm.AddLine (1, Cell.StringToCells ("This is last line."));
        Assert.Equal ((new Point (5, 1), true), tm.ReplaceAllText ("is", false, true, "really"));
        Assert.Equal (Cell.StringToCells ("This really first line."), tm.GetLine (0));
        Assert.Equal (Cell.StringToCells ("This really last line."), tm.GetLine (1));
    }

    [Fact]
    [AutoInitShutdown (useFakeClipboard: true)]
    public void KeyBindings_Command ()
    {
        var text = "This is the first line.\nThis is the second line.\nThis is the third line.";
        var tv = new TextView { Width = 10, Height = 2, Text = text };
        Toplevel top = new ();
        top.Add (tv);
        Application.Begin (top);

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (3, tv.Lines);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.ReadOnly);
        Assert.True (tv.CanFocus);

        var g = (SingleWordSuggestionGenerator)tv.Autocomplete.SuggestionGenerator;

        tv.CanFocus = false;
        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft));
        tv.CanFocus = true;
        Assert.False (tv.NewKeyDownEvent (Key.CursorLeft));
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight));
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.End.WithCtrl));
        Assert.Equal (2, tv.CurrentRow);
        Assert.Equal (23, tv.CurrentColumn);
        Assert.Equal (tv.CurrentColumn, tv.GetCurrentLine ().Count);
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.False (tv.NewKeyDownEvent (Key.CursorRight));
        Assert.NotNull (tv.Autocomplete);
        Assert.Empty (g.AllSuggestions);
        Assert.True (tv.NewKeyDownEvent (Key.F.WithShift));
        tv.Draw ();

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.F",
                      tv.Text
                     );
        Assert.Equal (new (24, 2), tv.CursorPosition);
        Assert.Empty (tv.Autocomplete.Suggestions);
        Assert.True (tv.NewKeyDownEvent (Key.Z.WithCtrl));
        tv.Draw ();

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.Empty (tv.Autocomplete.Suggestions);
        Assert.True (tv.NewKeyDownEvent (Key.R.WithCtrl));
        tv.Draw ();

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.F",
                      tv.Text
                     );
        Assert.Equal (new (24, 2), tv.CursorPosition);
        Assert.Empty (tv.Autocomplete.Suggestions);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.",
                      tv.Text
                     );
        Assert.Equal (new (23, 2), tv.CursorPosition);

        g.AllSuggestions = Regex.Matches (tv.Text, "\\w+")
                                .Select (s => s.Value)
                                .Distinct ()
                                .ToList ();
        Assert.Equal (7, g.AllSuggestions.Count);
        Assert.Equal ("This", g.AllSuggestions [0]);
        Assert.Equal ("is", g.AllSuggestions [1]);
        Assert.Equal ("the", g.AllSuggestions [2]);
        Assert.Equal ("first", g.AllSuggestions [3]);
        Assert.Equal ("line", g.AllSuggestions [4]);
        Assert.Equal ("second", g.AllSuggestions [5]);
        Assert.Equal ("third", g.AllSuggestions [^1]);
        Assert.True (tv.NewKeyDownEvent (Key.F.WithShift));
        tv.Draw ();

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.F",
                      tv.Text
                     );
        Assert.Equal (new (24, 2), tv.CursorPosition);
        Assert.Single (tv.Autocomplete.Suggestions);
        Assert.Equal ("first", tv.Autocomplete.Suggestions [0].Replacement);
        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (28, 2), tv.CursorPosition);
        Assert.Empty (tv.Autocomplete.Suggestions);
        Assert.False (tv.Autocomplete.Visible);
        g.AllSuggestions = new ();
        tv.Autocomplete.ClearSuggestions ();
        Assert.Empty (g.AllSuggestions);
        Assert.Empty (tv.Autocomplete.Suggestions);
        Assert.True (tv.NewKeyDownEvent (Key.PageUp));
        Assert.Equal (24, tv.GetCurrentLine ().Count);
        Assert.Equal (new (24, 1), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (new (Key.PageUp)));
        Assert.Equal (23, tv.GetCurrentLine ().Count);
        Assert.Equal (new (23, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.PageDown));
        Assert.Equal (24, tv.GetCurrentLine ().Count);
        Assert.Equal (new (23, 1), tv.CursorPosition); // gets the previous length
        Assert.True (tv.NewKeyDownEvent (Key.V.WithCtrl));
        Assert.Equal (28, tv.GetCurrentLine ().Count);
        Assert.Equal (new (23, 2), tv.CursorPosition); // gets the previous length
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.PageUp.WithShift));
        Assert.Equal (24, tv.GetCurrentLine ().Count);
        Assert.Equal (new (23, 1), tv.CursorPosition); // gets the previous length
        Assert.Equal (24 + Environment.NewLine.Length, tv.SelectedLength);
        Assert.Equal ($".{Environment.NewLine}This is the third line.", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.PageDown.WithShift));
        Assert.Equal (28, tv.GetCurrentLine ().Count);
        Assert.Equal (new (23, 2), tv.CursorPosition); // gets the previous length
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.Home.WithCtrl));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.N.WithCtrl));
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.P.WithCtrl));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.CursorDown));
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.CursorUp));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.CursorDown.WithShift));
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (23 + Environment.NewLine.Length, tv.SelectedLength);
        Assert.Equal ($"This is the first line.{Environment.NewLine}", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.CursorUp.WithShift));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.F.WithCtrl));
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.B.WithCtrl));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight));
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight.WithShift));
        Assert.Equal (new (1, 0), tv.CursorPosition);
        Assert.Equal (1, tv.SelectedLength);
        Assert.Equal ("T", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft.WithShift));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.Delete));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.Delete));

        Assert.Equal (
                      $"his is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.D.WithCtrl));

        Assert.Equal (
                      $"is is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.End));

        Assert.Equal (
                      $"is is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (21, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace));

        Assert.Equal (
                      $"is is the first line{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (20, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace));

        Assert.Equal (
                      $"is is the first lin{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Home));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.End.WithShift));
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (19, tv.SelectedLength);
        Assert.Equal ("is is the first lin", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.Home.WithShift));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.E.WithCtrl));
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.Home));
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.K.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal ("is is the first lin", Clipboard.Contents);
        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"is is the first lin{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal ("is is the first lin", Clipboard.Contents);
        tv.CursorPosition = Point.Empty;
        Assert.True (tv.NewKeyDownEvent (Key.Delete.WithCtrl.WithShift));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal ("is is the first lin", Clipboard.Contents);
        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"is is the first lin{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal ("is is the first lin", Clipboard.Contents);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl.WithShift));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        tv.ReadOnly = true;
        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        tv.ReadOnly = false;
        Assert.True (tv.NewKeyDownEvent (Key.Y.WithCtrl));

        Assert.Equal (
                      $"is is the first lin{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal (0, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.True (tv.NewKeyDownEvent (Key.Space.WithCtrl));

        Assert.Equal (
                      $"is is the first lin{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.Equal (19, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.True (tv.NewKeyDownEvent (Key.Space.WithCtrl));

        Assert.Equal (
                      $"is is the first lin{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal (19, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        tv.SelectionStartColumn = 0;
        Assert.True (tv.NewKeyDownEvent (Key.C.WithCtrl));

        Assert.Equal (
                      $"is is the first lin{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (19, tv.SelectedLength);
        Assert.Equal ("is is the first lin", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.Equal (0, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.True (tv.NewKeyDownEvent (Key.C.WithCtrl));

        Assert.Equal (
                      $"is is the first lin{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (19, 0), tv.CursorPosition);
        Assert.Equal (19, tv.SelectedLength);
        Assert.Equal ("is is the first lin", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.Equal (0, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.True (tv.NewKeyDownEvent (Key.X.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal (0, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.Equal ("is is the first lin", Clipboard.Contents);
        Assert.True (tv.NewKeyDownEvent (Key.W.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal (0, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.Equal ("", Clipboard.Contents);
        Assert.True (tv.NewKeyDownEvent (Key.X.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.Equal (0, tv.SelectionStartColumn);
        Assert.Equal (0, tv.SelectionStartRow);
        Assert.Equal ("", Clipboard.Contents);
        Assert.True (tv.NewKeyDownEvent (Key.End.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (28, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (18, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft.WithShift.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (12, 2), tv.CursorPosition);
        Assert.Equal (6, tv.SelectedLength);
        Assert.Equal ("third ", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (8, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (12, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight.WithShift.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (18, 2), tv.CursorPosition);
        Assert.Equal (6, tv.SelectedLength);
        Assert.Equal ("third ", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (22, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (23, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (new (28, 2), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.Home.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third line.first",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.Delete.WithCtrl));
        Assert.Equal ($"This is the second line.{Environment.NewLine}This is the third line.first", tv.Text);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.End.WithCtrl));
        Assert.Equal ($"This is the second line.{Environment.NewLine}This is the third line.first", tv.Text);
        Assert.Equal (new (28, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace.WithCtrl));
        Assert.Equal ($"This is the second line.{Environment.NewLine}This is the third ", tv.Text);
        Assert.Equal (new (18, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.AllowsReturn);

        tv.AllowsReturn = false;
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.False (tv.IsSelecting);
        Assert.False (tv.NewKeyDownEvent (Key.Enter)); // Accepted event not handled
        Assert.Equal ($"This is the second line.{Environment.NewLine}This is the third ", tv.Text);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.False (tv.AllowsReturn);

        tv.AllowsReturn = true;
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.True (tv.NewKeyDownEvent (Key.Enter));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third ",
                      tv.Text
                     );
        Assert.Equal (new (0, 1), tv.CursorPosition);
        Assert.Equal (0, tv.SelectedLength);
        Assert.Equal ("", tv.SelectedText);
        Assert.False (tv.IsSelecting);
        Assert.True (tv.AllowsReturn);
        Assert.True (tv.NewKeyDownEvent (Key.End.WithShift.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third ",
                      tv.Text
                     );
        Assert.Equal (new (18, 2), tv.CursorPosition);
        Assert.Equal (42 + Environment.NewLine.Length, tv.SelectedLength);
        Assert.Equal ($"This is the second line.{Environment.NewLine}This is the third ", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.Home.WithShift.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third ",
                      tv.Text
                     );
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (Environment.NewLine.Length, tv.SelectedLength);
        Assert.Equal ($"{Environment.NewLine}", tv.SelectedText);
        Assert.True (tv.IsSelecting);
        Assert.True (tv.NewKeyDownEvent (Key.A.WithCtrl));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third ",
                      tv.Text
                     );
        Assert.Equal (new (18, 2), tv.CursorPosition);
        Assert.Equal (42 + Environment.NewLine.Length * 2, tv.SelectedLength);

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third ",
                      tv.SelectedText
                     );
        Assert.True (tv.IsSelecting);
        Assert.True (tv.Used);
        Assert.True (tv.NewKeyDownEvent (Key.InsertChar));
        Assert.False (tv.Used);
        Assert.True (tv.AllowsTab);
        Assert.Equal (new (18, 2), tv.CursorPosition);
        tv.AllowsTab = false;
        Assert.False (tv.NewKeyDownEvent (Key.Tab));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third ",
                      tv.Text
                     );
        Assert.False (tv.AllowsTab);
        tv.AllowsTab = true;
        Assert.Equal (new (18, 2), tv.CursorPosition);
        Assert.True (tv.IsSelecting);
        tv.IsSelecting = false;
        Assert.True (tv.NewKeyDownEvent (Key.Tab));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third \t",
                      tv.Text
                     );
        Assert.True (tv.AllowsTab);
        tv.AllowsTab = false;
        Assert.False (tv.NewKeyDownEvent (Key.Tab.WithShift));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third \t",
                      tv.Text
                     );
        Assert.False (tv.AllowsTab);
        tv.AllowsTab = true;
        Assert.True (tv.NewKeyDownEvent (Key.Tab.WithShift));

        Assert.Equal (
                      $"{Environment.NewLine}This is the second line.{Environment.NewLine}This is the third ",
                      tv.Text
                     );
        Assert.True (tv.AllowsTab);
        Assert.False (tv.NewKeyDownEvent (Key.F6));
        Assert.False (tv.NewKeyDownEvent (Application.NextTabGroupKey));
        Assert.False (tv.NewKeyDownEvent (Key.F6.WithShift));
        Assert.False (tv.NewKeyDownEvent (Application.PrevTabGroupKey));

        Assert.True (tv.NewKeyDownEvent (PopoverMenu.DefaultKey));
        Assert.True (tv.ContextMenu != null && tv.ContextMenu.Visible);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Kill_Delete_WordBackward ()
    {
        _textView.Text = "This is the first line.";
        _textView.MoveEnd ();
        var iteration = 0;
        var iterationsFinished = false;

        while (!iterationsFinished)
        {
            _textView.NewKeyDownEvent (Key.Backspace.WithCtrl);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (22, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the first line", _textView.Text);

                    break;
                case 1:
                    Assert.Equal (18, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the first ", _textView.Text);

                    break;
                case 2:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the ", _textView.Text);

                    break;
                case 3:
                    Assert.Equal (8, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is ", _textView.Text);

                    break;
                case 4:
                    Assert.Equal (5, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This ", _textView.Text);

                    break;
                case 5:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("", _textView.Text);

                    break;
                default:
                    iterationsFinished = true;

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Kill_Delete_WordBackward_Multiline ()
    {
        _textView.Text = "This is the first line.\nThis is the second line.";
        _textView.Width = 4;
        _textView.MoveEnd ();
        var iteration = 0;
        var iterationsFinished = false;

        while (!iterationsFinished)
        {
            _textView.NewKeyDownEvent (Key.Backspace.WithCtrl);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (23, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "This is the first line."
                                  + Environment.NewLine
                                  + "This is the second line",
                                  _textView.Text
                                 );

                    break;
                case 1:
                    Assert.Equal (19, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "This is the first line."
                                  + Environment.NewLine
                                  + "This is the second ",
                                  _textView.Text
                                 );

                    break;
                case 2:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "This is the first line."
                                  + Environment.NewLine
                                  + "This is the ",
                                  _textView.Text
                                 );

                    break;
                case 3:
                    Assert.Equal (8, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "This is the first line."
                                  + Environment.NewLine
                                  + "This is ",
                                  _textView.Text
                                 );

                    break;
                case 4:
                    Assert.Equal (5, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "This is the first line."
                                  + Environment.NewLine
                                  + "This ",
                                  _textView.Text
                                 );

                    break;
                case 5:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the first line." + Environment.NewLine, _textView.Text);

                    break;
                case 6:
                    Assert.Equal (23, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the first line.", _textView.Text);

                    break;
                case 7:
                    Assert.Equal (22, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the first line", _textView.Text);

                    break;
                case 8:
                    Assert.Equal (18, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the first ", _textView.Text);

                    break;
                case 9:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the ", _textView.Text);

                    break;
                case 10:
                    Assert.Equal (8, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is ", _textView.Text);

                    break;
                case 11:
                    Assert.Equal (5, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This ", _textView.Text);

                    break;
                case 12:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("", _textView.Text);

                    break;
                default:
                    iterationsFinished = true;

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Kill_Delete_WordForward ()
    {
        _textView.Text = "This is the first line.";
        var iteration = 0;
        var iterationsFinished = false;

        while (!iterationsFinished)
        {
            _textView.NewKeyDownEvent (Key.Delete.WithCtrl);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("is the first line.", _textView.Text);

                    break;
                case 1:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("the first line.", _textView.Text);

                    break;
                case 2:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("first line.", _textView.Text);

                    break;
                case 3:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("line.", _textView.Text);

                    break;
                case 4:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("", _textView.Text);

                    break;
                default:
                    iterationsFinished = true;

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Kill_Delete_WordForward_Multiline ()
    {
        _textView.Text = "This is the first line.\nThis is the second line.";
        _textView.Width = 4;
        var iteration = 0;
        var iterationsFinished = false;

        while (!iterationsFinished)
        {
            _textView.NewKeyDownEvent (Key.Delete.WithCtrl);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "is the first line."
                                  + Environment.NewLine
                                  + "This is the second line.",
                                  _textView.Text
                                 );

                    break;
                case 1:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "the first line."
                                  + Environment.NewLine
                                  + "This is the second line.",
                                  _textView.Text
                                 );

                    break;
                case 2:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "first line."
                                  + Environment.NewLine
                                  + "This is the second line.",
                                  _textView.Text
                                 );

                    break;
                case 3:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  "line."
                                  + Environment.NewLine
                                  + "This is the second line.",
                                  _textView.Text
                                 );

                    break;
                case 4:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);

                    Assert.Equal (
                                  ""
                                  + Environment.NewLine
                                  + "This is the second line.",
                                  _textView.Text
                                 );

                    break;
                case 5:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the second line.", _textView.Text);

                    break;
                case 6:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("is the second line.", _textView.Text);

                    break;
                case 7:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("the second line.", _textView.Text);

                    break;
                case 8:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("second line.", _textView.Text);

                    break;
                case 9:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("line.", _textView.Text);

                    break;
                case 10:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("", _textView.Text);

                    break;
                default:
                    iterationsFinished = true;

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Kill_To_End_Delete_Forwards_Copy_To_The_Clipboard_And_Paste ()
    {
        _textView.Text = "This is the first line.\nThis is the second line.";
        var iteration = 0;
        var iterationsFinished = false;

        while (!iterationsFinished)
        {
            switch (iteration)
            {
                case 0:
                    _textView.NewKeyDownEvent (Key.K.WithCtrl);
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ($"{Environment.NewLine}This is the second line.", _textView.Text);
                    Assert.Equal ("This is the first line.", Clipboard.Contents);

                    break;
                case 1:
                    _textView.NewKeyDownEvent (
                                               new (
                                                    KeyCode.Delete | KeyCode.CtrlMask | KeyCode.ShiftMask
                                                   )
                                              );
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the second line.", _textView.Text);
                    Assert.Equal ($"This is the first line.{Environment.NewLine}", Clipboard.Contents);

                    break;
                case 2:
                    _textView.NewKeyDownEvent (Key.K.WithCtrl);
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("", _textView.Text);

                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is the second line.",
                                  Clipboard.Contents
                                 );

                    // Paste
                    _textView.NewKeyDownEvent (Key.Y.WithCtrl);

                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is the second line.",
                                  _textView.Text
                                 );

                    break;
                default:
                    iterationsFinished = true;

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Kill_To_Start_Delete_Backwards_Copy_To_The_Clipboard_And_Paste ()
    {
        _textView.Text = "This is the first line.\nThis is the second line.";
        _textView.MoveEnd ();
        var iteration = 0;
        var iterationsFinished = false;

        while (!iterationsFinished)
        {
            switch (iteration)
            {
                case 0:
                    _textView.NewKeyDownEvent (Key.Backspace.WithCtrl.WithShift);
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal ($"This is the first line.{Environment.NewLine}", _textView.Text);
                    Assert.Equal ("This is the second line.", Clipboard.Contents);

                    break;
                case 1:
                    _textView.NewKeyDownEvent (
                                               new (
                                                    KeyCode.Backspace | KeyCode.CtrlMask | KeyCode.ShiftMask
                                                   )
                                              );
                    Assert.Equal (23, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("This is the first line.", _textView.Text);
                    Assert.Equal ($"This is the second line.{Environment.NewLine}", Clipboard.Contents);

                    break;
                case 2:
                    _textView.NewKeyDownEvent (Key.Backspace.WithCtrl.WithShift);
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal ("", _textView.Text);

                    Assert.Equal (
                                  $"This is the second line.{Environment.NewLine}This is the first line.",
                                  Clipboard.Contents
                                 );

                    // Paste inverted
                    _textView.NewKeyDownEvent (Key.Y.WithCtrl);

                    Assert.Equal (
                                  $"This is the second line.{Environment.NewLine}This is the first line.",
                                  _textView.Text
                                 );

                    break;
                default:
                    iterationsFinished = true;

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    public void LeftColumn_Add_One_If_Text_Length_Is_Equal_To_Width ()
    {
        var tv = new TextView { Width = 10, Text = "1234567890" };

        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.LeftColumn);

        tv.CursorPosition = new (9, 0);
        Assert.Equal (new (9, 0), tv.CursorPosition);
        Assert.Equal (0, tv.LeftColumn);

        Assert.True (tv.NewKeyDownEvent (Key.CursorRight));
        tv.CursorPosition = new (10, 0);
        Assert.Equal (new (10, 0), tv.CursorPosition);
        Assert.Equal (1, tv.LeftColumn);
    }

    [Fact]
    public void LoadFile_Throws_If_File_Is_Empty ()
    {
        var result = false;
        var tv = new TextView ();
        Assert.Throws<ArgumentException> (() => result = tv.Load (""));
        Assert.False (result);
    }

    [Fact]
    public void LoadFile_Throws_If_File_Is_Null ()
    {
        var result = false;
        var tv = new TextView ();
        Assert.Throws<ArgumentNullException> (() => result = tv.Load ((string)null));
        Assert.False (result);
    }

    [Fact]
    public void LoadFile_Throws_If_File_Not_Exist ()
    {
        var result = false;
        var tv = new TextView ();
        Assert.Throws<FileNotFoundException> (() => result = tv.Load ("blabla"));
        Assert.False (result);
    }

    [Fact]
    public void LoadStream_CRLF ()
    {
        var text = "This is the first line.\r\nThis is the second line.\r\n";
        var tv = new TextView ();
        tv.Load (new MemoryStream (Encoding.ASCII.GetBytes (text)));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
    }

    [Fact]
    public void LoadStream_IsDirty ()
    {
        var text = "Testing";

        using (var stream = new MemoryStream ())
        {
            var writer = new StreamWriter (stream);
            writer.Write (text);
            writer.Flush ();
            stream.Position = 0;

            var tv = new TextView ();
            tv.Load (stream);

            Assert.Equal (7, text.Length);
            Assert.Equal (text.Length, tv.Text.Length);
            Assert.Equal (text, tv.Text);
            Assert.False (tv.IsDirty);
        }
    }

    [Fact]
    public void LoadStream_IsDirty_With_Null_On_The_Text ()
    {
        var text = "Test\0ing";

        using (var stream = new MemoryStream ())
        {
            var writer = new StreamWriter (stream);
            writer.Write (text);
            writer.Flush ();
            stream.Position = 0;

            var tv = new TextView ();
            tv.Load (stream);

            Assert.Equal (8, text.Length);
            Assert.Equal (text.Length, tv.Text.Length);
            Assert.Equal (8, text.Length);
            Assert.Equal (8, tv.Text.Length);
            Assert.Equal (text, tv.Text);
            Assert.False (tv.IsDirty);
            Assert.Equal ((Rune)'\u2400', ((Rune)tv.Text [4]).MakePrintable ());
        }
    }

    [Fact]
    public void LoadStream_LF ()
    {
        var text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView ();
        tv.Load (new MemoryStream (Encoding.ASCII.GetBytes (text)));

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
    }

    [Fact]
    public void LoadStream_Stream_Is_Empty ()
    {
        var tv = new TextView ();
        tv.Load (new MemoryStream ());
        Assert.Equal ("", tv.Text);
    }

    [Fact]
    public void LoadStream_Throws_If_Stream_Is_Null ()
    {
        var tv = new TextView ();
        Assert.Throws<ArgumentNullException> (() => tv.Load ((Stream)null));
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Mouse_Button_Shift_Preserves_Selection ()
    {
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);

        Assert.True (
                     _textView.NewMouseEvent (
                                              new() { Position = new (12, 0), Flags = MouseFlags.Button1Pressed | MouseFlags.ButtonShift }
                                             )
                    );
        Assert.Equal (0, _textView.SelectionStartColumn);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (new (12, 0), _textView.CursorPosition);
        Assert.True (_textView.IsSelecting);
        Assert.Equal ("TAB to jump ", _textView.SelectedText);

        Assert.True (_textView.NewMouseEvent (new() { Position = new (12, 0), Flags = MouseFlags.Button1Clicked }));
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (new (12, 0), _textView.CursorPosition);
        Assert.True (_textView.IsSelecting);
        Assert.Equal ("TAB to jump ", _textView.SelectedText);

        Assert.True (
                     _textView.NewMouseEvent (
                                              new() { Position = new (19, 0), Flags = MouseFlags.Button1Pressed | MouseFlags.ButtonShift }
                                             )
                    );
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (new (19, 0), _textView.CursorPosition);
        Assert.True (_textView.IsSelecting);
        Assert.Equal ("TAB to jump between", _textView.SelectedText);

        Assert.True (_textView.NewMouseEvent (new() { Position = new (19, 0), Flags = MouseFlags.Button1Clicked }));
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (new (19, 0), _textView.CursorPosition);
        Assert.True (_textView.IsSelecting);
        Assert.Equal ("TAB to jump between", _textView.SelectedText);

        Assert.True (
                     _textView.NewMouseEvent (
                                              new() { Position = new (24, 0), Flags = MouseFlags.Button1Pressed | MouseFlags.ButtonShift }
                                             )
                    );
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (new (24, 0), _textView.CursorPosition);
        Assert.True (_textView.IsSelecting);
        Assert.Equal ("TAB to jump between text", _textView.SelectedText);

        Assert.True (_textView.NewMouseEvent (new() { Position = new (24, 0), Flags = MouseFlags.Button1Clicked }));
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (new (24, 0), _textView.CursorPosition);
        Assert.True (_textView.IsSelecting);
        Assert.Equal ("TAB to jump between text", _textView.SelectedText);

        Assert.True (_textView.NewMouseEvent (new() { Position = new (24, 0), Flags = MouseFlags.Button1Pressed }));
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (new (24, 0), _textView.CursorPosition);
        Assert.True (_textView.IsSelecting);
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    [AutoInitShutdown]
    public void MoveDown_By_Setting_CursorPosition ()
    {
        var tv = new TextView { Width = 10, Height = 5 };

        // add 100 lines of wide text to view
        for (var i = 0; i < 100; i++)
        {
            tv.Text += new string ('x', 100) + (i == 99 ? "" : Environment.NewLine);
        }

        Assert.Equal (Point.Empty, tv.CursorPosition);
        tv.CursorPosition = new (5, 50);
        Assert.Equal (new (5, 50), tv.CursorPosition);

        tv.CursorPosition = new (200, 200);
        Assert.Equal (new (100, 99), tv.CursorPosition);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Multiline_Setting_Changes_AllowsReturn_AllowsTab_Height_WordWrap ()
    {
        Assert.True (_textView.Multiline);
        Assert.True (_textView.AllowsReturn);
        Assert.Equal (4, _textView.TabWidth);
        Assert.True (_textView.AllowsTab);
        Assert.False (_textView.WordWrap);

        _textView.WordWrap = true;
        Assert.True (_textView.WordWrap);
        _textView.Multiline = false;
        Assert.False (_textView.Multiline);
        Assert.False (_textView.AllowsReturn);
        Assert.Equal (0, _textView.TabWidth);
        Assert.False (_textView.AllowsTab);
        Assert.False (_textView.WordWrap);

        _textView.WordWrap = true;
        Assert.False (_textView.WordWrap);
        _textView.Multiline = true;
        Assert.True (_textView.Multiline);
        Assert.True (_textView.AllowsReturn);
        Assert.Equal (4, _textView.TabWidth);
        Assert.True (_textView.AllowsTab);
        Assert.False (_textView.WordWrap);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Paste_Always_Clear_The_SelectedText ()
    {
        _textView.SelectionStartColumn = 20;
        _textView.SelectionStartRow = 0;
        _textView.CursorPosition = new (24, 0);
        _textView.NewKeyDownEvent (Key.C.WithCtrl); // Copy
        Assert.Equal ("text", _textView.SelectedText);
        _textView.NewKeyDownEvent (Key.Y.WithCtrl); // Paste
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    public void ReplaceAllText_Does_Not_Throw_Exception ()
    {
        var textToFind = "hello! hello!";
        var textToReplace = "hello!";
        var tv = new TextView { Width = 20, Height = 3, Text = textToFind };

        Exception exception = Record.Exception (() => tv.ReplaceAllText (textToFind, false, false, textToReplace));
        Assert.Null (exception);
        Assert.Equal (textToReplace, tv.Text);
    }

    [Fact]
    [AutoInitShutdown]
    public void ScrollDownTillCaretOffscreen_ThenType ()
    {
        var tv = new TextView { Width = 10, Height = 5 };

        // add 100 lines of wide text to view
        for (var i = 0; i < 100; i++)
        {
            tv.Text += new string ('x', 100) + Environment.NewLine;
        }

        Assert.Equal (0, tv.CursorPosition.Y);
        tv.ScrollTo (50);
        Assert.Equal (0, tv.CursorPosition.Y);

        tv.NewKeyDownEvent (Key.P);
    }

    [Fact]
    [AutoInitShutdown]
    public void ScrollTo_CursorPosition ()
    {
        var tv = new TextView { Width = 10, Height = 5 };

        // add 100 lines of wide text to view
        for (var i = 0; i < 100; i++)
        {
            tv.Text += new string ('x', 100) + (i == 99 ? "" : Environment.NewLine);
        }

        Assert.Equal (Point.Empty, tv.CursorPosition);
        tv.ScrollTo (50);
        Assert.Equal (Point.Empty, tv.CursorPosition);

        tv.CursorPosition = new (tv.LeftColumn, tv.TopRow);
        Assert.Equal (new (0, 50), tv.CursorPosition);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Selected_Text_Shows ()
    {
        // Proves #3022 is fixed (TextField selected text does not show in v2)
        var top = new Toplevel ();
        top.Add (_textView);
        RunState rs = Application.Begin (top);

        _textView.CursorPosition = Point.Empty;
        _textView.SelectionStartColumn = 0;
        _textView.SelectionStartRow = 0;

        Attribute [] attributes =
        {
            _textView.ColorScheme.Focus,
            new (_textView.ColorScheme.Focus.Background, _textView.ColorScheme.Focus.Foreground)
        };

        //                                             TAB to jump between text fields.
        DriverAssert.AssertDriverAttributesAre ("0000000", _output, Application.Driver, attributes);
        Assert.Empty (_textView.SelectedCellsList);

        _textView.NewKeyDownEvent (Key.CursorRight.WithCtrl.WithShift);

        Application.RunIteration (ref rs, true);
        Assert.Equal (new (4, 0), _textView.CursorPosition);

        //                                             TAB to jump between text fields.
        DriverAssert.AssertDriverAttributesAre ("1111000", _output, Application.Driver, attributes);
        Assert.Equal ("TAB ", Cell.ToString (_textView.SelectedCellsList [^1]));
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Selection_And_CursorPosition_With_Value_Greater_Than_Text_Length_Changes_Both_To_Text_Length ()
    {
        _textView.CursorPosition = new (33, 2);
        _textView.SelectionStartColumn = 33;
        _textView.SelectionStartRow = 33;
        Assert.Equal (32, _textView.CursorPosition.X);
        Assert.Equal (0, _textView.CursorPosition.Y);
        Assert.Equal (32, _textView.SelectionStartColumn);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectedLength);
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Selection_With_Empty_Text ()
    {
        _textView = new ();
        _textView.CursorPosition = new (2, 0);
        _textView.SelectionStartColumn = 33;
        _textView.SelectionStartRow = 1;
        Assert.Equal (0, _textView.SelectionStartColumn);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectedLength);
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Selection_With_Value_Greater_Than_Text_Length_Changes_To_Text_Length ()
    {
        _textView.CursorPosition = new (2, 0);
        _textView.SelectionStartColumn = 33;
        _textView.SelectionStartRow = 1;
        Assert.Equal (32, _textView.SelectionStartColumn);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (30, _textView.SelectedLength);
        Assert.Equal ("B to jump between text fields.", _textView.SelectedText);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Selection_With_Value_Less_Than_Zero_Changes_To_Zero ()
    {
        _textView.SelectionStartColumn = -2;
        _textView.SelectionStartRow = -2;
        Assert.Equal (0, _textView.SelectionStartColumn);
        Assert.Equal (0, _textView.SelectionStartRow);
        Assert.Equal (0, _textView.SelectedLength);
        Assert.Equal ("", _textView.SelectedText);
    }

    [Fact]
    public void StringToRunes_Slipts_CRLF ()
    {
        var text = "This is the first line.\r\nThis is the second line.\r\n";
        var tv = new TextView ();
        tv.Text = text;

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
    }

    [Fact]
    public void StringToRunes_Slipts_LF ()
    {
        var text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView ();
        tv.Text = text;

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Tab_Test_Follow_By_BackTab ()
    {
        var top = new Toplevel ();
        top.Add (_textView);

        Application.Iteration += (s, a) =>
                                 {
                                     int width = _textView.Viewport.Width - 1;
                                     Assert.Equal (30, width + 1);
                                     Assert.Equal (10, _textView.Height);
                                     _textView.Text = "";
                                     var col = 0;
                                     var leftCol = 0;
                                     int tabWidth = _textView.TabWidth;

                                     while (col < 100)
                                     {
                                         col++;
                                         _textView.NewKeyDownEvent (Key.Tab);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     while (col > 0)
                                     {
                                         col--;
                                         _textView.NewKeyDownEvent (Key.Tab.WithShift);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     top.Remove (_textView);
                                     Application.RequestStop ();
                                 };

        Application.Run (top);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Tab_Test_Follow_By_BackTab_With_Text ()
    {
        var top = new Toplevel ();
        top.Add (_textView);

        Application.Iteration += (s, a) =>
                                 {
                                     int width = _textView.Viewport.Width - 1;
                                     Assert.Equal (30, width + 1);
                                     Assert.Equal (10, _textView.Height);
                                     var col = 0;
                                     var leftCol = 0;
                                     Assert.Equal (new (col, 0), _textView.CursorPosition);
                                     Assert.Equal (leftCol, _textView.LeftColumn);

                                     while (col < 100)
                                     {
                                         col++;
                                         _textView.NewKeyDownEvent (Key.Tab);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     while (col > 0)
                                     {
                                         col--;
                                         _textView.NewKeyDownEvent (Key.Tab.WithShift);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     top.Remove (_textView);
                                     Application.RequestStop ();
                                 };

        Application.Run (top);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Tab_Test_Follow_By_CursorLeft_And_Then_Follow_By_CursorRight ()
    {
        var top = new Toplevel ();
        top.Add (_textView);

        Application.Iteration += (s, a) =>
                                 {
                                     int width = _textView.Viewport.Width - 1;
                                     Assert.Equal (30, width + 1);
                                     Assert.Equal (10, _textView.Height);
                                     _textView.Text = "";
                                     var col = 0;
                                     var leftCol = 0;
                                     int tabWidth = _textView.TabWidth;

                                     while (col < 100)
                                     {
                                         col++;
                                         _textView.NewKeyDownEvent (Key.Tab);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     while (col > 0)
                                     {
                                         col--;
                                         _textView.NewKeyDownEvent (Key.CursorLeft);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     while (col < 100)
                                     {
                                         col++;
                                         _textView.NewKeyDownEvent (Key.CursorRight);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     top.Remove (_textView);
                                     Application.RequestStop ();
                                 };

        Application.Run (top);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Tab_Test_Follow_By_CursorLeft_And_Then_Follow_By_CursorRight_With_Text ()
    {
        var top = new Toplevel ();
        top.Add (_textView);

        Application.Iteration += (s, a) =>
                                 {
                                     int width = _textView.Viewport.Width - 1;
                                     Assert.Equal (30, width + 1);
                                     Assert.Equal (10, _textView.Height);
                                     Assert.Equal ("TAB to jump between text fields.", _textView.Text);
                                     var col = 0;
                                     var leftCol = 0;
                                     int tabWidth = _textView.TabWidth;

                                     while (col < 100)
                                     {
                                         col++;
                                         _textView.NewKeyDownEvent (Key.Tab);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     Assert.Equal (132, _textView.Text.Length);

                                     while (col > 0)
                                     {
                                         col--;
                                         _textView.NewKeyDownEvent (Key.CursorLeft);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     while (col < 100)
                                     {
                                         col++;
                                         _textView.NewKeyDownEvent (Key.CursorRight);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     top.Remove (_textView);
                                     Application.RequestStop ();
                                 };

        Application.Run (top);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Tab_Test_Follow_By_Home_And_Then_Follow_By_End_And_Then_Follow_By_BackTab_With_Text ()
    {
        var top = new Toplevel ();
        top.Add (_textView);

        Application.Iteration += (s, a) =>
                                 {
                                     int width = _textView.Viewport.Width - 1;
                                     Assert.Equal (30, width + 1);
                                     Assert.Equal (10, _textView.Height);
                                     var col = 0;
                                     var leftCol = 0;
                                     Assert.Equal (new (col, 0), _textView.CursorPosition);
                                     Assert.Equal (leftCol, _textView.LeftColumn);
                                     Assert.Equal ("TAB to jump between text fields.", _textView.Text);
                                     Assert.Equal (32, _textView.Text.Length);

                                     while (col < 100)
                                     {
                                         col++;
                                         _textView.NewKeyDownEvent (Key.Tab);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     _textView.NewKeyDownEvent (Key.Home);
                                     col = 0;
                                     Assert.Equal (new (col, 0), _textView.CursorPosition);
                                     leftCol = 0;
                                     Assert.Equal (leftCol, _textView.LeftColumn);

                                     _textView.NewKeyDownEvent (Key.End);
                                     col = _textView.Text.Length;
                                     Assert.Equal (132, _textView.Text.Length);
                                     Assert.Equal (new (col, 0), _textView.CursorPosition);
                                     leftCol = GetLeftCol (leftCol);
                                     Assert.Equal (leftCol, _textView.LeftColumn);
                                     string txt = _textView.Text;

                                     while (col - 1 > 0 && txt [col - 1] != '\t')
                                     {
                                         col--;
                                     }

                                     _textView.CursorPosition = new (col, 0);
                                     leftCol = GetLeftCol (leftCol);

                                     while (col > 0)
                                     {
                                         col--;
                                         _textView.NewKeyDownEvent (Key.Tab.WithShift);
                                         Assert.Equal (new (col, 0), _textView.CursorPosition);
                                         leftCol = GetLeftCol (leftCol);
                                         Assert.Equal (leftCol, _textView.LeftColumn);
                                     }

                                     Assert.Equal ("TAB to jump between text fields.", _textView.Text);
                                     Assert.Equal (32, _textView.Text.Length);

                                     top.Remove (_textView);
                                     Application.RequestStop ();
                                 };

        Application.Run (top);
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void TabWidth_Setting_To_Zero_Keeps_AllowsTab ()
    {
        var top = new Toplevel ();
        top.Add (_textView);
        Application.Begin (top);

        Assert.Equal (4, _textView.TabWidth);
        Assert.True (_textView.AllowsTab);
        Assert.True (_textView.AllowsReturn);
        Assert.True (_textView.Multiline);
        _textView.TabWidth = -1;
        Assert.Equal (0, _textView.TabWidth);
        Assert.True (_textView.AllowsTab);
        Assert.True (_textView.AllowsReturn);
        Assert.True (_textView.Multiline);
        _textView.NewKeyDownEvent (Key.Tab);
        Assert.Equal ("\tTAB to jump between text fields.", _textView.Text);
        Application.LayoutAndDraw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
TAB to jump between text field",
                                                       _output
                                                      );

        _textView.TabWidth = 4;
        Application.LayoutAndDraw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
    TAB to jump between text f",
                                                       _output
                                                      );

        _textView.NewKeyDownEvent (Key.Tab.WithShift);
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        Assert.True (_textView.NeedsDraw);
        Application.LayoutAndDraw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
TAB to jump between text field",
                                                       _output
                                                      );
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void TextChanged_Event ()
    {
        _textView.TextChanged += (s, e) =>
                                 {
                                     if (_textView.Text == "changing")
                                     {
                                         Assert.Equal ("changing", _textView.Text);
                                         _textView.Text = "changed";
                                     }
                                 };

        _textView.Text = "changing";
        Assert.Equal ("changed", _textView.Text);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void TextChanged_Event_NoFires_OnTyping ()
    {
        var eventcount = 0;
        _textView.TextChanged += (s, e) => { eventcount++; };

        _textView.Text = "ay";
        Assert.Equal (1, eventcount);
        _textView.NewKeyDownEvent (Key.Y.WithShift);
        Assert.Equal (1, eventcount);
        Assert.Equal ("Yay", _textView.Text);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void TextView_InsertText_Newline_CRLF ()
    {
        var tv = new TextView { Width = 10, Height = 10 };
        tv.InsertText ("\r\naaa\r\nbbb");
        PlatformID p = Environment.OSVersion.Platform;

        if (p == PlatformID.Win32NT || p == PlatformID.Win32S || p == PlatformID.Win32Windows)
        {
            Assert.Equal ("\r\naaa\r\nbbb", tv.Text);
        }
        else
        {
            Assert.Equal ("\naaa\nbbb", tv.Text);
        }

        Assert.Equal ($"{Environment.NewLine}aaa{Environment.NewLine}bbb", tv.Text);

        var win = new Window ();
        win.Add (tv);
        var top = new Toplevel ();
        top.Add (win);
        Application.Begin (top);
        ((FakeDriver)Application.Driver!).SetBufferSize (15, 15);
        Application.LayoutAndDraw ();

        //this passes
        Rectangle pos = DriverAssert.AssertDriverContentsWithFrameAre (
                                                                       @"
┌─────────────┐
│             │
│aaa          │
│bbb          │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
└─────────────┘",
                                                                       _output
                                                                      );

        Assert.Equal (new (0, 0, 15, 15), pos);

        Assert.True (tv.Used);
        tv.Used = false;
        tv.CursorPosition = Point.Empty;
        tv.InsertText ("\r\naaa\r\nbbb");
        Application.LayoutAndDraw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
┌─────────────┐
│             │
│aaa          │
│bbb          │
│aaa          │
│bbb          │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
└─────────────┘",
                                                       _output
                                                      );
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void TextView_InsertText_Newline_LF ()
    {
        var tv = new TextView { Width = 10, Height = 10 };
        tv.InsertText ("\naaa\nbbb");
        PlatformID p = Environment.OSVersion.Platform;

        if (p == PlatformID.Win32NT || p == PlatformID.Win32S || p == PlatformID.Win32Windows)
        {
            Assert.Equal ("\r\naaa\r\nbbb", tv.Text);
        }
        else
        {
            Assert.Equal ("\naaa\nbbb", tv.Text);
        }

        Assert.Equal ($"{Environment.NewLine}aaa{Environment.NewLine}bbb", tv.Text);

        var win = new Window ();
        win.Add (tv);
        var top = new Toplevel ();
        top.Add (win);
        Application.Begin (top);
        ((FakeDriver)Application.Driver!).SetBufferSize (15, 15);
        Application.LayoutAndDraw ();

        //this passes
        Rectangle pos = DriverAssert.AssertDriverContentsWithFrameAre (
                                                                       @"
┌─────────────┐
│             │
│aaa          │
│bbb          │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
└─────────────┘",
                                                                       _output
                                                                      );

        Assert.Equal (new (0, 0, 15, 15), pos);

        Assert.True (tv.Used);
        tv.Used = false;
        tv.CursorPosition = Point.Empty;
        tv.InsertText ("\naaa\nbbb");
        Application.LayoutAndDraw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
┌─────────────┐
│             │
│aaa          │
│bbb          │
│aaa          │
│bbb          │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
│             │
└─────────────┘",
                                                       _output
                                                      );
        top.Dispose ();
    }

    [Fact]
    public void TextView_MultiLine_But_Without_Tabs ()
    {
        var view = new TextView ();

        // the default for TextView
        Assert.True (view.Multiline);

        view.AllowsTab = false;
        Assert.False (view.AllowsTab);

        Assert.True (view.Multiline);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void TextView_SpaceHandling ()
    {
        var tv = new TextView { Width = 10, Text = " " };

        var ev = new MouseEventArgs { Position = new (0, 0), Flags = MouseFlags.Button1DoubleClicked };

        tv.NewMouseEvent (ev);
        Assert.Equal (1, tv.SelectedLength);

        ev = new() { Position = new (1, 0), Flags = MouseFlags.Button1DoubleClicked };

        tv.NewMouseEvent (ev);
        Assert.Equal (1, tv.SelectedLength);
    }

    [Fact]
    [AutoInitShutdown]
    public void UnwrappedCursorPosition_Event ()
    {
        var cp = Point.Empty;

        var tv = new TextView
        {
            Width = Dim.Fill (), Height = Dim.Fill (), Text = "This is the first line.\nThis is the second line.\n"
        };
        tv.UnwrappedCursorPosition += (s, e) => { cp = e; };
        var top = new Toplevel ();
        top.Add (tv);
        Application.Begin (top);
        Application.LayoutAndDraw ();

        Assert.False (tv.WordWrap);
        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (Point.Empty, cp);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is the first line. 
This is the second line.
",
                                                       _output
                                                      );

        tv.WordWrap = true;
        tv.CursorPosition = new (12, 0);
        tv.Draw ();
        Assert.Equal (new (12, 0), tv.CursorPosition);
        Assert.Equal (new (12, 0), cp);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is the first line. 
This is the second line.
",
                                                       _output
                                                      );

        ((FakeDriver)Application.Driver).SetBufferSize (6, 25);
        tv.SetRelativeLayout (Application.Screen.Size);
        tv.Draw ();
        Assert.Equal (new (4, 2), tv.CursorPosition);
        Assert.Equal (new (12, 0), cp);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This 
is   
the  
first
     
line.
This 
is   
the  
secon
d    
line.
",
                                                       _output
                                                      );

        Assert.True (tv.NewKeyDownEvent (Key.CursorRight));
        tv.Draw ();
        Assert.Equal (new (0, 3), tv.CursorPosition);
        Assert.Equal (new (12, 0), cp);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This 
is   
the  
first
     
line.
This 
is   
the  
secon
d    
line.
",
                                                       _output
                                                      );

        Assert.True (tv.NewKeyDownEvent (Key.CursorRight));
        tv.Draw ();
        Assert.Equal (new (1, 3), tv.CursorPosition);
        Assert.Equal (new (13, 0), cp);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This 
is   
the  
first
     
line.
This 
is   
the  
secon
d    
line.
",
                                                       _output
                                                      );

        Assert.True (tv.NewMouseEvent (new() { Position = new (0, 3), Flags = MouseFlags.Button1Pressed }));
        tv.Draw ();
        Assert.Equal (new (0, 3), tv.CursorPosition);
        Assert.Equal (new (13, 0), cp);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This 
is   
the  
first
     
line.
This 
is   
the  
secon
d    
line.
",
                                                       _output
                                                      );
        top.Dispose ();
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Used_Is_False ()
    {
        _textView.Used = false;
        _textView.CursorPosition = new (10, 0);
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.U); // u
        Assert.Equal ("TAB to jumu between text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.S); // s
        Assert.Equal ("TAB to jumusbetween text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.E); // e
        Assert.Equal ("TAB to jumuseetween text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.D); // d
        Assert.Equal ("TAB to jumusedtween text fields.", _textView.Text);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void Used_Is_True_By_Default ()
    {
        _textView.CursorPosition = new (10, 0);
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.U); // u
        Assert.Equal ("TAB to jumup between text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.S); // s
        Assert.Equal ("TAB to jumusp between text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.E); // e
        Assert.Equal ("TAB to jumusep between text fields.", _textView.Text);
        _textView.NewKeyDownEvent (Key.D); // d
        Assert.Equal ("TAB to jumusedp between text fields.", _textView.Text);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordBackward_Multiline_With_Selection ()
    {
        //		          4         3          2         1
        //		  87654321098765432109876 54321098765432109876543210-Length
        //			    1         2              1         2
        //                01234567890123456789012  0123456789012345678901234
        _textView.Text = "This is the first line.\nThis is the second line.";

        _textView.MoveEnd ();
        _textView.SelectionStartColumn = _textView.CurrentColumn;
        _textView.SelectionStartRow = _textView.CurrentRow;
        var iteration = 0;
        var iterationsFinished = false;

        while (!iterationsFinished)
        {
            _textView.NewKeyDownEvent (Key.CursorLeft.WithCtrl.WithShift);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (23, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (1, _textView.SelectedLength);
                    Assert.Equal (".", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (19, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (5, _textView.SelectedLength);
                    Assert.Equal ("line.", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (12, _textView.SelectedLength);
                    Assert.Equal ("second line.", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (8, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (16, _textView.SelectedLength);
                    Assert.Equal ("the second line.", _textView.SelectedText);

                    break;
                case 4:
                    Assert.Equal (5, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (19, _textView.SelectedLength);
                    Assert.Equal ("is the second line.", _textView.SelectedText);

                    break;
                case 5:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (24, _textView.SelectedLength);
                    Assert.Equal ("This is the second line.", _textView.SelectedText);

                    break;
                case 6:
                    Assert.Equal (23, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (24 + Environment.NewLine.Length, _textView.SelectedLength);
                    Assert.Equal ($"{Environment.NewLine}This is the second line.", _textView.SelectedText);

                    break;
                case 7:
                    Assert.Equal (22, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (25 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $".{Environment.NewLine}This is the second line.",
                                  _textView.SelectedText
                                 );

                    break;
                case 8:
                    Assert.Equal (18, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (29 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"line.{Environment.NewLine}This is the second line.",
                                  _textView.SelectedText
                                 );

                    break;
                case 9:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (35 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"first line.{Environment.NewLine}This is the second line.",
                                  _textView.SelectedText
                                 );

                    break;
                case 10:
                    Assert.Equal (8, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (39 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"the first line.{Environment.NewLine}This is the second line.",
                                  _textView.SelectedText
                                 );

                    break;
                case 11:
                    Assert.Equal (5, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (42 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"is the first line.{Environment.NewLine}This is the second line.",
                                  _textView.SelectedText
                                 );

                    break;
                case 12:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (24, _textView.SelectionStartColumn);
                    Assert.Equal (1, _textView.SelectionStartRow);
                    Assert.Equal (47 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is the second line.",
                                  _textView.SelectedText
                                 );

                    break;
                default:
                    iterationsFinished = true;

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordBackward_With_No_Selection ()
    {
        _textView.CursorPosition = new (_textView.Text.Length, 0);
        var iteration = 0;

        while (_textView.CursorPosition.X > 0)
        {
            _textView.NewKeyDownEvent (Key.CursorLeft.WithCtrl);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (31, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (25, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (20, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 4:
                    Assert.Equal (7, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 5:
                    Assert.Equal (4, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 6:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordBackward_With_No_Selection_And_With_More_Than_Only_One_Whitespace_And_With_Only_One_Letter ()
    {
        //                          1         2         3         4         5    
        //                0123456789012345678901234567890123456789012345678901234=55 (Length)
        _textView.Text = "TAB   t  o  jump         b  etween    t ext   f ields .";
        _textView.CursorPosition = new (_textView.Text.Length, 0);
        var iteration = 0;

        while (_textView.CursorPosition.X > 0)
        {
            _textView.NewKeyDownEvent (Key.CursorLeft.WithCtrl);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (54, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (48, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (46, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (40, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 4:
                    Assert.Equal (38, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 5:
                    Assert.Equal (28, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 6:
                    Assert.Equal (25, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 7:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 8:
                    Assert.Equal (9, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 9:
                    Assert.Equal (6, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 10:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordBackward_With_Selection ()
    {
        _textView.CursorPosition = new (_textView.Text.Length, 0);
        _textView.SelectionStartColumn = _textView.Text.Length;
        _textView.SelectionStartRow = 0;
        var iteration = 0;

        while (_textView.CursorPosition.X > 0)
        {
            _textView.NewKeyDownEvent (Key.CursorLeft.WithCtrl.WithShift);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (31, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (32, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (1, _textView.SelectedLength);
                    Assert.Equal (".", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (25, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (32, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (7, _textView.SelectedLength);
                    Assert.Equal ("fields.", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (20, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (32, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (12, _textView.SelectedLength);
                    Assert.Equal ("text fields.", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (32, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (20, _textView.SelectedLength);
                    Assert.Equal ("between text fields.", _textView.SelectedText);

                    break;
                case 4:
                    Assert.Equal (7, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (32, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (25, _textView.SelectedLength);
                    Assert.Equal ("jump between text fields.", _textView.SelectedText);

                    break;
                case 5:
                    Assert.Equal (4, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (32, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (28, _textView.SelectedLength);
                    Assert.Equal ("to jump between text fields.", _textView.SelectedText);

                    break;
                case 6:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (32, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (32, _textView.SelectedLength);
                    Assert.Equal ("TAB to jump between text fields.", _textView.SelectedText);

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void
        WordBackward_With_The_Same_Values_For_SelectedStart_And_CursorPosition_And_Not_Starting_At_Beginning_Of_The_Text ()
    {
        _textView.CursorPosition = new (10, 0);
        _textView.SelectionStartColumn = 10;
        _textView.SelectionStartRow = 0;
        var iteration = 0;

        while (_textView.CursorPosition.X > 0)
        {
            _textView.NewKeyDownEvent (Key.CursorLeft.WithCtrl.WithShift);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (7, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (10, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (3, _textView.SelectedLength);
                    Assert.Equal ("jum", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (4, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (10, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (6, _textView.SelectedLength);
                    Assert.Equal ("to jum", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (10, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (10, _textView.SelectedLength);
                    Assert.Equal ("TAB to jum", _textView.SelectedText);

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    public void WordBackward_WordForward_Limits_Return_Null ()
    {
        var model = new TextModel ();
        model.LoadString ("Test");
        (int col, int row)? newPos = model.WordBackward (0, 0);
        Assert.Null (newPos);
        newPos = model.WordForward (4, 0);
        Assert.Null (newPos);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordForward_Multiline_With_Selection ()
    {
        //			    1         2          3         4
        //		  01234567890123456789012 34567890123456789012345678-Length
        //			    1         2              1         2
        //                01234567890123456789012  0123456789012345678901234
        _textView.Text = "This is the first line.\nThis is the second line.";

        _textView.SelectionStartColumn = _textView.CurrentColumn;
        _textView.SelectionStartRow = _textView.CurrentRow;
        var iteration = 0;
        var iterationsFinished = false;

        while (!iterationsFinished)
        {
            _textView.NewKeyDownEvent (
                                       new (
                                            KeyCode.CursorRight | KeyCode.CtrlMask | KeyCode.ShiftMask
                                           )
                                      );

            switch (iteration)
            {
                case 0:
                    Assert.Equal (5, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (5, _textView.SelectedLength);
                    Assert.Equal ("This ", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (8, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (8, _textView.SelectedLength);
                    Assert.Equal ("This is ", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (12, _textView.SelectedLength);
                    Assert.Equal ("This is the ", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (18, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (18, _textView.SelectedLength);
                    Assert.Equal ("This is the first ", _textView.SelectedText);

                    break;
                case 4:
                    Assert.Equal (23, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (23, _textView.SelectedLength);
                    Assert.Equal ("This is the first line.", _textView.SelectedText);

                    break;
                case 5:
                    Assert.Equal (0, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (23 + Environment.NewLine.Length, _textView.SelectedLength);
                    Assert.Equal ($"This is the first line.{Environment.NewLine}", _textView.SelectedText);

                    break;
                case 6:
                    Assert.Equal (5, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (28 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This ",
                                  _textView.SelectedText
                                 );

                    break;
                case 7:
                    Assert.Equal (8, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (31 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is ",
                                  _textView.SelectedText
                                 );

                    break;
                case 8:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (35 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is the ",
                                  _textView.SelectedText
                                 );

                    break;
                case 9:
                    Assert.Equal (19, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (42 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is the second ",
                                  _textView.SelectedText
                                 );

                    break;
                case 10:
                    Assert.Equal (24, _textView.CursorPosition.X);
                    Assert.Equal (1, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (47 + Environment.NewLine.Length, _textView.SelectedLength);

                    Assert.Equal (
                                  $"This is the first line.{Environment.NewLine}This is the second line.",
                                  _textView.SelectedText
                                 );

                    break;
                default:
                    iterationsFinished = true;

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordForward_With_No_Selection ()
    {
        _textView.CursorPosition = Point.Empty;
        var iteration = 0;

        while (_textView.CursorPosition.X < _textView.Text.Length)
        {
            _textView.NewKeyDownEvent (Key.CursorRight.WithCtrl);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (4, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (7, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (20, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 4:
                    Assert.Equal (25, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 5:
                    Assert.Equal (32, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordForward_With_No_Selection_And_With_More_Than_Only_One_Whitespace_And_With_Only_One_Letter ()
    {
        //                          1         2         3         4         5    
        //                0123456789012345678901234567890123456789012345678901234=55 (Length)
        _textView.Text = "TAB   t  o  jump         b  etween    t ext   f ields .";
        _textView.CursorPosition = Point.Empty;
        var iteration = 0;

        while (_textView.CursorPosition.X < _textView.Text.Length)
        {
            _textView.NewKeyDownEvent (Key.CursorRight.WithCtrl);

            switch (iteration)
            {
                case 0:
                    Assert.Equal (6, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (9, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (25, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 4:
                    Assert.Equal (28, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 5:
                    Assert.Equal (38, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 6:
                    Assert.Equal (40, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 7:
                    Assert.Equal (46, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 8:
                    Assert.Equal (48, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 9:
                    Assert.Equal (54, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
                case 10:
                    Assert.Equal (55, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (0, _textView.SelectedLength);
                    Assert.Equal ("", _textView.SelectedText);

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordForward_With_Selection ()
    {
        _textView.CursorPosition = Point.Empty;
        _textView.SelectionStartColumn = 0;
        _textView.SelectionStartRow = 0;
        var iteration = 0;

        while (_textView.CursorPosition.X < _textView.Text.Length)
        {
            _textView.NewKeyDownEvent (
                                       new (
                                            KeyCode.CursorRight | KeyCode.CtrlMask | KeyCode.ShiftMask
                                           )
                                      );

            switch (iteration)
            {
                case 0:
                    Assert.Equal (4, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (4, _textView.SelectedLength);
                    Assert.Equal ("TAB ", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (7, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (7, _textView.SelectedLength);
                    Assert.Equal ("TAB to ", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (12, _textView.SelectedLength);
                    Assert.Equal ("TAB to jump ", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (20, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (20, _textView.SelectedLength);
                    Assert.Equal ("TAB to jump between ", _textView.SelectedText);

                    break;
                case 4:
                    Assert.Equal (25, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (25, _textView.SelectedLength);
                    Assert.Equal ("TAB to jump between text ", _textView.SelectedText);

                    break;
                case 5:
                    Assert.Equal (32, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (0, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (32, _textView.SelectedLength);
                    Assert.Equal ("TAB to jump between text fields.", _textView.SelectedText);

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void
        WordForward_With_The_Same_Values_For_SelectedStart_And_CursorPosition_And_Not_Starting_At_Beginning_Of_The_Text ()
    {
        _textView.CursorPosition = new (10, 0);
        _textView.SelectionStartColumn = 10;
        _textView.SelectionStartRow = 0;
        var iteration = 0;

        while (_textView.CursorPosition.X < _textView.Text.Length)
        {
            _textView.NewKeyDownEvent (
                                       new (
                                            KeyCode.CursorRight | KeyCode.CtrlMask | KeyCode.ShiftMask
                                           )
                                      );

            switch (iteration)
            {
                case 0:
                    Assert.Equal (12, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (10, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (2, _textView.SelectedLength);
                    Assert.Equal ("p ", _textView.SelectedText);

                    break;
                case 1:
                    Assert.Equal (20, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (10, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (10, _textView.SelectedLength);
                    Assert.Equal ("p between ", _textView.SelectedText);

                    break;
                case 2:
                    Assert.Equal (25, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (10, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (15, _textView.SelectedLength);
                    Assert.Equal ("p between text ", _textView.SelectedText);

                    break;
                case 3:
                    Assert.Equal (32, _textView.CursorPosition.X);
                    Assert.Equal (0, _textView.CursorPosition.Y);
                    Assert.Equal (10, _textView.SelectionStartColumn);
                    Assert.Equal (0, _textView.SelectionStartRow);
                    Assert.Equal (22, _textView.SelectedLength);
                    Assert.Equal ("p between text fields.", _textView.SelectedText);

                    break;
            }

            iteration++;
        }
    }

    [Fact]
    [AutoInitShutdown]
    public void WordWrap_Deleting_Backwards ()
    {
        var tv = new TextView { Width = 5, Height = 2, WordWrap = true, Text = "aaaa" };
        var top = new Toplevel ();
        top.Add (tv);
        Application.Begin (top);
        Application.LayoutAndDraw ();

        Assert.Equal (Point.Empty, tv.CursorPosition);
        Assert.Equal (0, tv.LeftColumn);

        DriverAssert.AssertDriverContentsAre (
                                              @"
aaaa
",
                                              _output
                                             );

        tv.CursorPosition = new (5, 0);
        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.LayoutAndDraw ();
        Assert.Equal (0, tv.LeftColumn);

        DriverAssert.AssertDriverContentsAre (
                                              @"
aaa
",
                                              _output
                                             );

        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.LayoutAndDraw ();
        Assert.Equal (0, tv.LeftColumn);

        DriverAssert.AssertDriverContentsAre (
                                              @"
aa
",
                                              _output
                                             );

        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.LayoutAndDraw ();
        Assert.Equal (0, tv.LeftColumn);

        DriverAssert.AssertDriverContentsAre (
                                              @"
a
",
                                              _output
                                             );

        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.LayoutAndDraw ();
        Assert.Equal (0, tv.LeftColumn);

        DriverAssert.AssertDriverContentsAre (
                                              @"

",
                                              _output
                                             );

        Assert.True (tv.NewKeyDownEvent (Key.Backspace));
        Application.LayoutAndDraw ();
        Assert.Equal (0, tv.LeftColumn);

        DriverAssert.AssertDriverContentsAre (
                                              @"

",
                                              _output
                                             );
        top.Dispose ();
    }

    [Theory]
    [TextViewTestsAutoInitShutdown]
    [InlineData (KeyCode.Delete)]
    public void WordWrap_Draw_Typed_Keys_After_Text_Is_Deleted (KeyCode del)
    {
        var top = new Toplevel ();
        top.Add (_textView);
        _textView.Text = "Line 1.\nLine 2.";
        _textView.WordWrap = true;
        Application.Begin (top);
        Application.LayoutAndDraw ();

        Assert.True (_textView.WordWrap);

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
Line 1.
Line 2.",
                                                       _output
                                                      );

        Assert.True (_textView.NewKeyDownEvent (Key.End.WithShift));
        Assert.Equal ("Line 1.", _textView.SelectedText);

        Assert.True (_textView.NewKeyDownEvent (new (del)));
        Application.LayoutAndDraw ();
        DriverAssert.AssertDriverContentsWithFrameAre ("Line 2.", _output);

        Assert.True (_textView.NewKeyDownEvent (Key.H.WithShift));
        Assert.NotEqual (Rectangle.Empty, _textView._needsDrawRect);
        Application.LayoutAndDraw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
H      
Line 2.",
                                                       _output
                                                      );
        top.Dispose ();
    }

    [Fact]
    public void WordWrap_Gets_Sets ()
    {
        var tv = new TextView { WordWrap = true };
        Assert.True (tv.WordWrap);
        tv.WordWrap = false;
        Assert.False (tv.WordWrap);
    }

    [Fact]
    [AutoInitShutdown]
    public void WordWrap_Not_Throw_If_Width_Is_Less_Than_Zero ()
    {
        Exception exception = Record.Exception (
                                                () =>
                                                {
                                                    var tv = new TextView
                                                    {
                                                        Width = Dim.Fill (),
                                                        Height = Dim.Fill (),
                                                        WordWrap = true,
                                                        Text = "これは、左右のクリップ境界をテストするための非常に長いテキストです。"
                                                    };
                                                }
                                               );
        Assert.Null (exception);
    }

    [Fact]
    [AutoInitShutdown]
    public void WordWrap_ReadOnly_CursorPosition_SelectedText_Copy ()
    {
        //          0123456789
        var text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView { Width = 11, Height = 9 };
        tv.Text = text;

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
        tv.WordWrap = true;

        var top = new Toplevel ();
        top.Add (tv);
        top.Layout ();
        tv.Draw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is  
the first
line.    
This is  
the      
second   
line.    
",
                                                       _output
                                                      );

        tv.ReadOnly = true;
        tv.CursorPosition = new (6, 2);
        Assert.Equal (new (5, 2), tv.CursorPosition);
        top.LayoutSubViews ();
        View.SetClipToScreen ();
        tv.Draw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is   
the first 
line.     
This is   
the second
line.     
",
                                                       _output
                                                      );

        tv.SelectionStartRow = 0;
        tv.SelectionStartColumn = 0;
        Assert.Equal ("This is the first line.", tv.SelectedText);

        tv.Copy ();
        Assert.Equal ("This is the first line.", Clipboard.Contents);
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordWrap_True_LoadStream_New_Text ()
    {
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        _textView.WordWrap = true;
        Assert.Equal ("TAB to jump between text fields.", _textView.Text);
        var text = "This is the first line.\nThis is the second line.\n";

        using (var stream = new MemoryStream ())
        {
            var writer = new StreamWriter (stream);
            writer.Write (text);
            writer.Flush ();
            stream.Position = 0;

            _textView.Load (stream);

            Assert.Equal (
                          $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                          _textView.Text
                         );
            Assert.True (_textView.WordWrap);
        }
    }

    [Fact]
    public void WordWrap_True_Text_Always_Returns_Unwrapped ()
    {
        var text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView { Width = 10 };
        tv.Text = text;

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
        tv.WordWrap = true;

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
    }

    [Fact]
    [TextViewTestsAutoInitShutdown]
    public void WordWrap_WrapModel_Output ()
    {
        //          0123456789
        var text = "This is the first line.\nThis is the second line.\n";
        var tv = new TextView { Width = 10, Height = 10 };
        tv.Text = text;

        Assert.Equal (
                      $"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}",
                      tv.Text
                     );
        tv.WordWrap = true;

        var top = new Toplevel ();
        top.Add (tv);

        top.Layout ();
        tv.Draw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is
the    
first  
line.  
This is
the    
second 
line.  
",
                                                       _output
                                                      );
    }

    private int GetLeftCol (int start)
    {
        string [] lines = _textView.Text.Split (Environment.NewLine);

        if (lines == null || lines.Length == 0)
        {
            return 0;
        }

        if (start == _textView.LeftColumn)
        {
            return start;
        }

        if (_textView.LeftColumn == _textView.CurrentColumn)
        {
            return _textView.CurrentColumn;
        }

        int cCol = _textView.CurrentColumn;
        string line = lines [_textView.CurrentRow];
        int lCount = cCol > line.Length - 1 ? line.Length - 1 : cCol;
        int width = _textView.Frame.Width;
        int tabWidth = _textView.TabWidth;
        var sumLength = 0;
        var col = 0;

        for (int i = lCount; i >= 0; i--)
        {
            char r = line [i];
            sumLength += ((Rune)r).GetColumns ();

            if (r == '\t')
            {
                sumLength += tabWidth + 1;
            }

            if (sumLength > width)
            {
                if (col + width == cCol)
                {
                    col++;
                }

                break;
            }

            if ((cCol < line.Length && col > 0 && start < cCol && col == start) || cCol - col == width - 1)
            {
                break;
            }

            col = i;
        }

        return col;
    }

    // This class enables test functions annotated with the [InitShutdown] attribute
    // to have a function called before the test function is called and after.
    // 
    // This is necessary because a) Application is a singleton and Init/Shutdown must be called
    // as a pair, and b) all unit test functions should be atomic.
    [AttributeUsage (AttributeTargets.Class | AttributeTargets.Method)]
    public class TextViewTestsAutoInitShutdown : AutoInitShutdownAttribute
    {
        public static string Txt = "TAB to jump between text fields.";

        public TextViewTestsAutoInitShutdown () : base (configLocation: ConfigLocations.Default) { }

        public override void After (MethodInfo methodUnderTest)
        {
            _textView = null;
            base.After (methodUnderTest);
        }

        public override void Before (MethodInfo methodUnderTest)
        {
            FakeDriver.FakeBehaviors.UseFakeClipboard = true;
            base.Before (methodUnderTest);

            //                   1         2         3 
            //         01234567890123456789012345678901=32 (Length)
            byte [] buff = Encoding.Unicode.GetBytes (Txt);
            byte [] ms = new MemoryStream (buff).ToArray ();
            _textView = new() { Width = 30, Height = 10, ColorScheme = Colors.ColorSchemes ["Base"] };
            _textView.Text = Encoding.Unicode.GetString (ms);
        }
    }

    [Fact]
    public void HotKey_Command_SetsFocus ()
    {
        var view = new TextView ();

        view.CanFocus = true;
        Assert.False (view.HasFocus);
        view.InvokeCommand (Command.HotKey);
        Assert.True (view.HasFocus);
    }

    [Fact]
    public void HotKey_Command_Does_Not_Accept ()
    {
        var view = new TextView ();
        var accepted = false;
        view.Accepting += OnAccept;
        view.InvokeCommand (Command.HotKey);

        Assert.False (accepted);

        return;

        void OnAccept (object sender, CommandEventArgs e) { accepted = true; }
    }

    [Theory]
    [InlineData (false, 1)]
    [InlineData (true, 1)]
    public void Accepted_Command_Raises_Accepted_Regardles_Of_AllowsReturn (bool allowsReturn, int expectedAcceptEvents)
    {
        var view = new TextView
        {
            AllowsReturn = allowsReturn
        };

        var acceptedEvents = 0;
        view.Accepting += Accept;
        view.InvokeCommand (Command.Accept);
        Assert.Equal (expectedAcceptEvents, acceptedEvents);

        return;

        void Accept (object sender, CommandEventArgs e) { acceptedEvents++; }
    }

    [Theory]
    [InlineData (false, 1)]
    [InlineData (true, 0)]
    public void Enter_Key_Fires_Accepted_BasedOn_AllowsReturn (bool allowsReturn, int expectedAccepts)
    {
        var view = new TextView
        {
            Multiline = allowsReturn
        };

        var accepted = 0;
        view.Accepting += Accept;
        view.NewKeyDownEvent (Key.Enter);
        Assert.Equal (expectedAccepts, accepted);

        return;

        void Accept (object sender, CommandEventArgs e) { accepted++; }
    }

    [Theory]
    [InlineData (false, 1)]
    [InlineData (true, 0)]
    public void Enter_Key_Fires_Accepted_BasedOn_Multiline (bool multiline, int expectedAccepts)
    {
        var view = new TextView
        {
            Multiline = multiline
        };

        var accepted = 0;
        view.Accepting += Accept;
        view.NewKeyDownEvent (Key.Enter);
        Assert.Equal (expectedAccepts, accepted);

        return;

        void Accept (object sender, CommandEventArgs e) { accepted++; }
    }

    [Fact]
    public void Space_Key_Types_Space ()
    {
        var view = new TextView ();

        view.NewKeyDownEvent (Key.Space);

        Assert.Equal (" ", view.Text);
    }

    [Theory]
    [InlineData (false, false, 1, 1)]
    [InlineData (false, true, 1, 0)]
    [InlineData (true, false, 0, 0)]
    [InlineData (true, true, 0, 0)]
    public void Accepted_Event_Handled_Prevents_Default_Button_Accept (bool multiline, bool handleAccept, int expectedAccepts, int expectedButtonAccepts)
    {
        var superView = new Window ();

        var tv = new TextView
        {
            Multiline = multiline
        };

        var button = new Button
        {
            IsDefault = true
        };

        superView.Add (tv, button);

        var buttonAccept = 0;
        button.Accepting += ButtonAccept;

        var textViewAccept = 0;
        tv.Accepting += TextViewAccept;

        tv.SetFocus ();
        Assert.True (tv.HasFocus);

        superView.NewKeyDownEvent (Key.Enter);
        Assert.Equal (expectedAccepts, textViewAccept);
        Assert.Equal (expectedButtonAccepts, buttonAccept);

        button.SetFocus ();
        superView.NewKeyDownEvent (Key.Enter);
        Assert.Equal (expectedAccepts, textViewAccept);
        Assert.Equal (expectedButtonAccepts + 1, buttonAccept);

        return;

        void TextViewAccept (object sender, CommandEventArgs e)
        {
            textViewAccept++;
            e.Cancel = handleAccept;
        }

        void ButtonAccept (object sender, CommandEventArgs e) { buttonAccept++; }
    }

    [Theory]
    [InlineData (true, 0)]
    [InlineData (false, 1)]
    public void Accepted_No_Handler_Enables_Default_Button_Accept (bool multiline, int expectedButtonAccept)
    {
        var superView = new Window ();

        var tv = new TextView
        {
            Multiline = multiline
        };

        var button = new Button
        {
            IsDefault = true
        };

        superView.Add (tv, button);

        var buttonAccept = 0;
        button.Accepting += ButtonAccept;

        tv.SetFocus ();
        Assert.True (tv.HasFocus);

        superView.NewKeyDownEvent (Key.Enter);
        Assert.Equal (expectedButtonAccept, buttonAccept);

        button.SetFocus ();
        superView.NewKeyDownEvent (Key.Enter);
        Assert.Equal (expectedButtonAccept + 1, buttonAccept);

        return;

        void ButtonAccept (object sender, CommandEventArgs e) { buttonAccept++; }
    }

    [Fact]
    public void Autocomplete_Popup_Added_To_SuperView_On_Init ()
    {
        View superView = new ()
        {
            CanFocus = true
        };

        TextView t = new ();

        superView.Add (t);
        Assert.Single (superView.SubViews);

        superView.BeginInit ();
        superView.EndInit ();

        Assert.Equal (2, superView.SubViews.Count);
    }

    [Fact]
    public void Autocomplete__Added_To_SuperView_On_Add ()
    {
        View superView = new ()
        {
            CanFocus = true,
            Id = "superView"
        };

        superView.BeginInit ();
        superView.EndInit ();
        Assert.Empty (superView.SubViews);

        TextView t = new ()
        {
            Id = "t"
        };

        superView.Add (t);

        Assert.Equal (2, superView.SubViews.Count);
    }

    [Fact]
    public void Autocomplete_Visible_False_By_Default ()
    {
        View superView = new ()
        {
            CanFocus = true
        };

        TextView t = new ();

        superView.Add (t);
        superView.BeginInit ();
        superView.EndInit ();

        Assert.Equal (2, superView.SubViews.Count);

        Assert.True (t.Visible);
        Assert.False (t.Autocomplete.Visible);
    }

    [Fact]
    public void Right_CursorAtEnd_WithSelection_ShouldClearSelection ()
    {
        var tv = new TextView
        {
            Text = "Hello"
        };
        tv.SetFocus ();

        tv.NewKeyDownEvent (Key.End.WithShift);
        Assert.Equal (5, tv.CursorPosition.X);

        // When there is selected text and the cursor is at the end of the text field
        Assert.Equal ("Hello", tv.SelectedText);

        // Pressing right should not move focus, instead it should clear selection
        Assert.True (tv.NewKeyDownEvent (Key.CursorRight));
        Assert.Empty (tv.SelectedText);

        // Now that the selection is cleared another right keypress should move focus
        Assert.False (tv.NewKeyDownEvent (Key.CursorRight));
    }

    [Fact]
    public void Left_CursorAtStart_WithSelection_ShouldClearSelection ()
    {
        var tv = new TextView
        {
            Text = "Hello"
        };
        tv.SetFocus ();

        tv.NewKeyDownEvent (Key.CursorRight);
        tv.NewKeyDownEvent (Key.CursorRight);

        Assert.Equal (2, tv.CursorPosition.X);

        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft.WithShift));
        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft.WithShift));

        // When there is selected text and the cursor is at the start of the text field
        Assert.Equal ("He", tv.SelectedText);

        // Pressing left should not move focus, instead it should clear selection
        Assert.True (tv.NewKeyDownEvent (Key.CursorLeft));
        Assert.Empty (tv.SelectedText);

        // When clearing selected text with left the cursor should be at the start of the selection
        Assert.Equal (0, tv.CursorPosition.X);

        // Now that the selection is cleared another left keypress should move focus
        Assert.False (tv.NewKeyDownEvent (Key.CursorLeft));
    }

    [Fact]
    [AutoInitShutdown]
    public void Draw_Esc_Rune ()
    {
        var tv = new TextView { Width = 5, Height = 1, Text = "\u001b" };
        tv.BeginInit ();
        tv.EndInit ();
        tv.Draw ();

        DriverAssert.AssertDriverContentsWithFrameAre ("\u241b", _output);

        tv.Dispose ();
    }

    [Fact]
    public void Equals_True ()
    {
        var c1 = new Cell ();
        var c2 = new Cell ();
        Assert.True (c1.Equals (c2));
        Assert.True (c2.Equals (c1));

        c1.Rune = new ('a');
        c1.Attribute = new ();
        c2.Rune = new ('a');
        c2.Attribute = new ();
        Assert.True (c1.Equals (c2));
        Assert.True (c2.Equals (c1));
    }

    [Fact]
    [AutoInitShutdown]
    public void CellEventArgs_WordWrap_True ()
    {
        var eventCount = 0;

        List<List<Cell>> text =
        [
            Cell.ToCells (
                          "This is the first line.".ToRunes ()
                         ),

            Cell.ToCells (
                          "This is the second line.".ToRunes ()
                         )
        ];
        TextView tv = CreateTextView ();
        tv.DrawNormalColor += TextView_DrawColor;
        tv.DrawReadOnlyColor += TextView_DrawColor;
        tv.DrawSelectionColor += TextView_DrawColor;
        tv.DrawUsedColor += TextView_DrawColor;

        void TextView_DrawColor (object sender, CellEventArgs e)
        {
            Assert.Equal (e.Line [e.Col], text [e.UnwrappedPosition.Row] [e.UnwrappedPosition.Col]);
            eventCount++;
        }

        tv.Text = $"{Cell.ToString (text [0])}\n{Cell.ToString (text [1])}\n";
        Assert.False (tv.WordWrap);
        var top = new Toplevel ();
        top.Add (tv);
        Application.Begin (top);
        Application.LayoutAndDraw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is the first line. 
This is the second line.",
                                                       _output
                                                      );

        tv.Width = 10;
        tv.Height = 25;
        tv.WordWrap = true;
        Application.LayoutAndDraw ();

        DriverAssert.AssertDriverContentsWithFrameAre (
                                                       @"
This is
the    
first  
line.  
This is
the    
second 
line.  ",
                                                       _output
                                                      );

        Assert.Equal (eventCount, (text [0].Count + text [1].Count) * 2);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown (configLocation: ConfigLocations.Default)]
    public void Cell_LoadCells_InheritsPreviousAttribute ()
    {
        List<Cell> cells = [];

        foreach (KeyValuePair<string, ColorScheme> color in Colors.ColorSchemes)
        {
            string csName = color.Key;

            foreach (Rune rune in csName.EnumerateRunes ())
            {
                cells.Add (new () { Rune = rune, Attribute = color.Value.Normal });
            }

            cells.Add (new () { Rune = (Rune)'\n', Attribute = color.Value.Focus });
        }

        TextView tv = CreateTextView ();
        tv.Load (cells);
        var top = new Toplevel ();
        top.Add (tv);
        RunState rs = Application.Begin (top);
        Application.LayoutAndDraw ();

        Assert.True (tv.InheritsPreviousAttribute);

        var expectedText = @"
TopLevel
Base    
Dialog  
Menu    
Error   ";
        DriverAssert.AssertDriverContentsWithFrameAre (expectedText, _output);

        Attribute [] attributes =
        {
            // 0
            Colors.ColorSchemes ["TopLevel"].Normal,

            // 1
            Colors.ColorSchemes ["Base"].Normal,

            // 2
            Colors.ColorSchemes ["Dialog"].Normal,

            // 3
            Colors.ColorSchemes ["Menu"].Normal,

            // 4
            Colors.ColorSchemes ["Error"].Normal,

            // 5
            tv.ColorScheme!.Focus
        };

        var expectedColor = @"
0000000055
1111555555
2222225555
3333555555
4444455555";
        DriverAssert.AssertDriverAttributesAre (expectedColor, _output, Application.Driver, attributes);

        tv.WordWrap = true;
        Application.LayoutAndDraw ();
        DriverAssert.AssertDriverContentsWithFrameAre (expectedText, _output);
        DriverAssert.AssertDriverAttributesAre (expectedColor, _output, Application.Driver, attributes);

        tv.CursorPosition = new (6, 2);
        tv.SelectionStartColumn = 0;
        tv.SelectionStartRow = 0;
        Assert.Equal ($"TopLevel{Environment.NewLine}Base{Environment.NewLine}Dialog", tv.SelectedText);
        tv.Copy ();
        tv.IsSelecting = false;
        tv.CursorPosition = new (2, 4);
        tv.Paste ();
        Application.LayoutAndDraw ();

        expectedText = @"
TopLevel  
Base      
Dialog    
Menu      
ErTopLevel
Base      
Dialogror ";
        DriverAssert.AssertDriverContentsWithFrameAre (expectedText, _output);

        expectedColor = @"
0000000055
1111555555
2222225555
3333555555
4400000000
1111555555
2222224445";
        DriverAssert.AssertDriverAttributesAre (expectedColor, _output, Application.Driver, attributes);

        tv.Undo ();
        tv.CursorPosition = new (0, 3);
        tv.SelectionStartColumn = 0;
        tv.SelectionStartRow = 0;

        Assert.Equal (
                      $"TopLevel{Environment.NewLine}Base{Environment.NewLine}Dialog{Environment.NewLine}",
                      tv.SelectedText
                     );
        tv.Copy ();
        tv.IsSelecting = false;
        tv.CursorPosition = new (2, 4);
        tv.Paste ();
        Application.LayoutAndDraw ();

        expectedText = @"
TopLevel  
Base      
Dialog    
Menu      
ErTopLevel
Base      
Dialog    
ror       ";
        DriverAssert.AssertDriverContentsWithFrameAre (expectedText, _output);

        expectedColor = @"
0000000055
1111555555
2222225555
3333555555
4400000000
1111555555
2222225555
4445555555";
        DriverAssert.AssertDriverAttributesAre (expectedColor, _output, Application.Driver, attributes);

        Application.End (rs);
        top.Dispose ();
    }

    [Fact]
    public void Cell_LoadCells_Without_ColorScheme_Is_Never_Null ()
    {
        List<Cell> cells = new ()
        {
            new () { Rune = new ('T') },
            new () { Rune = new ('e') },
            new () { Rune = new ('s') },
            new () { Rune = new ('t') }
        };
        TextView tv = CreateTextView ();
        var top = new Toplevel ();
        top.Add (tv);
        tv.Load (cells);

        for (var i = 0; i < tv.Lines; i++)
        {
            List<Cell> line = tv.GetLine (i);

            foreach (Cell c in line)
            {
                Assert.NotNull (c.Attribute);
            }
        }
    }

    private TextView CreateTextView () { return new () { Width = 30, Height = 10 }; }
}
