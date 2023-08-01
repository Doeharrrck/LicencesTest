using System.Windows;
using System.Windows.Controls;

namespace LicenseHandling
{
    /// <summary>
    /// Interaction logic for MessageDialogWindow.xaml.
    /// </summary>
    public partial class MessageDialogWindow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageDialogWindow"/> class.
        /// </summary>
        protected MessageDialogWindow()
        {
            this.InitializeComponent();
        }

        #region IDisposable

        /// <summary>
        /// The instance of this doesn't need to be disposed.
        /// But calling dispose will close the open <see cref="MessageDialogWindow"/>.
        /// </summary>
        public void Dispose()
        {
            this.Close();
        }

        #endregion IDisposable

        #region public properties

        /// <summary>
        /// Gets the message box result.
        /// </summary>
        public DialogResult MessageResult { get; private set; }

        #endregion public properties

        #region public methods

        /// <summary>
        /// Shows a modal instance of a message dialog window.
        /// </summary>
        /// <param name="message">The message, which will be displayed in the middle.</param>
        /// <param name="caption">The caption, that will be displayed on top.</param>
        /// <param name="buttons">Defines which buttons are shown.</param>
        /// <param name="progressLabel">The label of the progress bar. Bar is hidden if null or empty.</param>
        /// <param name="progressBarMax">The maximum of the progress bar.</param>
        /// <param name="progressBarValue">The value of the progress bar.</param>
        /// <returns>The result, depending on which button is clicked.</returns>
        public static DialogResult ShowDialog(
            string message,
            string caption,
            MessageBoxButtons buttons,
            string progressBarLabel,
            int progressBarMax,
            int progressBarValue)
        {
            var window = new MessageDialogWindow
            {
                Title = caption,
            };

            window.MessageText.Text = message;

            if (string.IsNullOrEmpty(progressBarLabel))
            {
                window.ProgressText.Visibility = Visibility.Collapsed;
                window.ProgressBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                window.ProgressText.Text = progressBarLabel;
                window.ProgressBar.Maximum = progressBarMax;
                window.ProgressBar.Value = progressBarValue;
            }

            AddButtons(buttons, window);

            window.ShowDialog();

            return window.MessageResult;
        }

        #endregion public methods

        #region private event handlers

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            this.MessageResult = (DialogResult)button.Tag;
            this.Close();
        }

        #endregion private event handlers

        #region private methods

        private static void AddButtons(MessageBoxButtons buttons, MessageDialogWindow window)
        {
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    AddOkButton(window);
                    window.ButtonPanel.HorizontalAlignment = HorizontalAlignment.Center;
                    break;
                case MessageBoxButtons.OKCancel:
                    AddCancelButton(window);
                    AddOkButton(window);
                    break;
                case MessageBoxButtons.YesNoCancel:
                    AddCancelButton(window);
                    AddNoButton(window);
                    AddYesButton(window);
                    break;
                case MessageBoxButtons.YesNo:
                    AddNoButton(window);
                    AddYesButton(window);
                    break;
                case MessageBoxButtons.Cancel:
                    AddCancelButton(window);
                    break;
                case MessageBoxButtons.AbortRetryIgnore:
                    AddAbortButton(window);
                    AddRetryButton(window);
                    AddIgnoreButton(window);
                    break;
                default:
                    break;
            }
        }

        private static void AddOkButton(MessageDialogWindow window)
        {
            Button okButton = CreateButton(window, "OK", LicenseHandling.DialogResult.OK);
            SetAffirmativeStyle(window, okButton);
            window.ButtonPanel.Children.Add(okButton);
        }

        private static void AddCancelButton(MessageDialogWindow window)
        {
            Button cancelButton = CreateButton(window, "Cancel", LicenseHandling.DialogResult.Cancel);
            window.ButtonPanel.Children.Add(cancelButton);
        }

        private static void AddYesButton(MessageDialogWindow window)
        {
            Button yesButton = CreateButton(window, "Yes", LicenseHandling.DialogResult.Yes);
            SetAffirmativeStyle(window, yesButton);
            window.ButtonPanel.Children.Add(yesButton);
        }

        private static void AddNoButton(MessageDialogWindow window)
        {
            Button noButton = CreateButton(window, "No", LicenseHandling.DialogResult.No);
            window.ButtonPanel.Children.Add(noButton);
        }

        private static void AddAbortButton(MessageDialogWindow window)
        {
            Button abortButton = CreateButton(window, "Abort", LicenseHandling.DialogResult.Abort);
            window.ButtonPanel.Children.Add(abortButton);
        }

        private static void AddRetryButton(MessageDialogWindow window)
        {
            Button retryButton = CreateButton(window, "Retry", LicenseHandling.DialogResult.Retry);
            window.ButtonPanel.Children.Add(retryButton);
        }

        private static void AddIgnoreButton(MessageDialogWindow window)
        {
            Button ignoreButton = CreateButton(window, "Ignore", LicenseHandling.DialogResult.Ignore);
            window.ButtonPanel.Children.Add(ignoreButton);
        }

        private static Button CreateButton(MessageDialogWindow window, string caption, DialogResult result)
        {
            var button = new Button
            {
                Content = caption,
                Tag = result,
                Width = 80,
                Margin = new Thickness(20, 10, 20, 10),
            };

            button.Click += window.Button_Click;
            return button;
        }

        private static void SetAffirmativeStyle(MessageDialogWindow window, Button button)
        {
            button.IsDefault = true;
        }

        #endregion private methods
    }
}
