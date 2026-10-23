using System.Drawing;
using System.Windows.Forms;
using GreekSwitch.Windows.Focus;

namespace GreekSwitch.Windows.Tests.Focus;

/// <summary>
/// Exercises the COM UI Automation path against real controls in a window of our own, shown
/// off-screen without activation so it never takes focus from the user.
/// </summary>
public class PasswordFieldDetectorTests
{
    [Fact]
    public void Recognises_a_password_box_and_only_a_password_box()
    {
        nint password = 0, plain = 0;
        using var ready = new ManualResetEventSlim();
        Form? form = null;
        var ui = new Thread(() =>
        {
            form = new BackgroundForm();
            var passwordBox = new TextBox { UseSystemPasswordChar = true };
            var plainBox = new TextBox { Top = 40 };
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

            detector.IsPasswordWindow(password).Should().BeTrue();
            detector.IsPasswordWindow(plain).Should().BeFalse();
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
        new PasswordFieldDetector().Invoking(d => d.IsPasswordFieldFocused()).Should().NotThrow();
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
