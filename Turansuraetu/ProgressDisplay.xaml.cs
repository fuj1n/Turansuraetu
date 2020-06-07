using System.ComponentModel;
using System.Windows;

namespace Turansuraetu
{
    /// <summary>
    /// Interaction logic for ProgressDisplay.xaml
    /// </summary>
    public partial class ProgressDisplay : Window
    {
        private bool isDone = false;

        public ProgressDisplay()
        {
            InitializeComponent();
        }

        public void Update(string text, double progress)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Update(text, progress));
                return;
            }

            ProgressLabel.Content = text;
            Bar.Value = progress * 100.0;
        }

        private void OnClosingWindow(object sender, CancelEventArgs e)
        {
            e.Cancel = !isDone;
        }

        public void Done()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(Done);
                return;
            }

            isDone = true;
            Close();
        }
    }
}
