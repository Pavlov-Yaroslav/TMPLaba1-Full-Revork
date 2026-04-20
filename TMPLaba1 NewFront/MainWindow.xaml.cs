using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TMPLAB1;
using System.IO;

namespace TMPLaba1_NewFront
{
    public partial class MainWindow : Window
    {
        private PRD? currentPrdFile = null;
        private PRS? currentPrsFile = null;
        private ComponentsWindow? CompWindow = null;
        private SpecificationWindow? SpecWindow = null;

        public MainWindow()
        {
            InitializeComponent();
            this.StateChanged += MainWindow_StateChanged;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PRD files (*.prd)|*.prd|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    currentPrdFile = new PRD(openFileDialog.FileName);
                    currentPrdFile.Open();

                    string directory = Path.GetDirectoryName(openFileDialog.FileName) ?? string.Empty;
                    string prsFileName = Path.ChangeExtension(openFileDialog.FileName, ".prs");

                    currentPrsFile = new PRS(prsFileName);

                    if (File.Exists(prsFileName))
                    {
                        currentPrsFile.Open();
                    }
                    else
                    {
                        currentPrsFile.Create();
                        currentPrsFile.Open();
                    }

                    MessageBox.Show($"Файлы успешно загружены",
                                  "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке файлов: {ex.Message}",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenComponents_Click(object sender, RoutedEventArgs e)
        {
            if ((currentPrdFile == null) || !currentPrdFile.IsOpen)
            {
                MessageBox.Show("Сначала откройте файл через меню 'Открыть'", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CompWindow != null && CompWindow.IsLoaded)
            {
                CompWindow.Close();
                CompWindow = null;
            }

            CompWindow = new ComponentsWindow(currentPrdFile);
            CompWindow.Show();
        }

        private void OpenSpecification_Click(object sender, RoutedEventArgs e)
        {
            if ((currentPrdFile == null) || !currentPrdFile.IsOpen || (currentPrsFile == null) || !currentPrsFile.IsOpen)
            {
                MessageBox.Show("Сначала откройте файл через меню 'Открыть'", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SpecWindow != null && SpecWindow.IsLoaded)
            {
                SpecWindow.Close();
                SpecWindow = null;
            }

            SpecWindow = new SpecificationWindow(currentPrdFile, currentPrsFile);
            SpecWindow.Show();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                // Сворачиваем все дочерние окна
                if (CompWindow != null && CompWindow.IsLoaded)
                {
                    CompWindow.WindowState = WindowState.Minimized;
                }

                if (SpecWindow != null && SpecWindow.IsLoaded)
                {
                    SpecWindow.WindowState = WindowState.Minimized;
                }
            }
            else if (this.WindowState == WindowState.Normal)
            {
                if (CompWindow != null && CompWindow.IsLoaded && CompWindow.WindowState == WindowState.Minimized)
                {
                    CompWindow.WindowState = WindowState.Normal;
                }

                if (SpecWindow != null && SpecWindow.IsLoaded && SpecWindow.WindowState == WindowState.Minimized)
                {
                    SpecWindow.WindowState = WindowState.Normal;
                }
            }
            else if (this.WindowState == WindowState.Maximized)
            {
                if (CompWindow != null && CompWindow.IsLoaded && CompWindow.WindowState == WindowState.Minimized)
                {
                    CompWindow.WindowState = WindowState.Normal;
                }

                if (SpecWindow != null && SpecWindow.IsLoaded && SpecWindow.WindowState == WindowState.Minimized)
                {
                    SpecWindow.WindowState = WindowState.Normal;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (CompWindow != null && CompWindow.IsLoaded)
            {
                CompWindow.Close();
            }

            if (SpecWindow != null && SpecWindow.IsLoaded)
            {
                SpecWindow.Close();
            }

            base.OnClosed(e);
        }
    }
}