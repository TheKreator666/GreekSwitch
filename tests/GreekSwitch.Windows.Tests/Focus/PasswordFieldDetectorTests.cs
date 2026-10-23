using System.Drawing;
using System.Windows.Forms;
using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Output;

namespace GreekSwitch.Windows.Tests.Focus;

/// <summary>
/// Exercises the COM UI Automation path against real controls in a window of our own, shown
/// off-screen without activation so it never takes focus from the user.
/// </summary>
public class PasswordFieldDetectorTests
{
    [Fact]
    public void Recognises_a_password_box_and_reads_text_before_the_caret()
    {
        nint password = 0, plain = 0;
        using var ready = new ManualResetEventSlim();
        Form? form = null;
        TextBox? plainBox = null;
        var ui = new Thread(() =>
        {
            form = new BackgroundForm();
            var passwordBox = new TextBox { UseSystemPasswordChar = true };
            plainBox = new TextBox { Top = 40 };
            form.Controls.Add(passwordBox);
            form.Controls.Add(plainBox);
            form.Shown += (_, _) =>
            {
                password = passwordBox.Handle;
                plain = plainBox.Handle;
                ready.Set();
            };
            Application.Run(form);
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();

        try
        {
            ready.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the test window should open");
            var detector = new PasswordFieldDetector();

            detector.CheckWindow(password).IsPassword.Should().BeTrue();
            detector.CheckWindow(plain).IsPassword.Should().BeFalse();
            detector.CheckWindow(plain).NativeWindow.Should().Be(plain, "a native control reports its own window");

            using var uia = new UiaThread();
            var verifier = new UiaTextVerifier(uia, TimeSpan.FromSeconds(5));
            form!.Invoke(() =>
            {
                plainBox!.Text = "hello world ";
                plainBox.SelectionStart = plainBox.Text.Length;
            });
            verifier.BeforeCaretIn(plain, "world ").Should().Be(TextMatch.Match);
            verifier.BeforeCaretIn(plain, "word ").Should().Be(TextMatch.Mismatch);
            verifier.BeforeCaretIn(plain, "hello world ").Should().Be(TextMatch.Match);
        }
        finally
        {
            form?.Invoke(form.Close);
            ui.Join(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void Querying_the_focused_element_does_not_throw()
    {
        new PasswordFieldDetector().Invoking(d => d.CheckFocused()).Should().NotThrow();
    }

    private sealed class BackgroundForm : Form
    {
        public BackgroundForm()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(200, 100);
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
        }

        protected override bool ShowWithoutActivation => true;
    }
}
