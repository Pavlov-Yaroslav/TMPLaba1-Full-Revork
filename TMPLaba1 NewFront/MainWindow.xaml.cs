// MainWindow.xaml.cs
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
    /// <summary>
    /// Главное окно приложения. Управляет открытием файлов и координирует
    /// дочерние окна компонентов и спецификации.
    /// </summary>
    public partial class MainWindow : Window
    {
        // Текущий открытый PRD-файл (null, если файл не выбран).
        private PRD? currentPrdFile = null;

        // Текущий открытый PRS-файл (null, если файл не выбран).
        private PRS? currentPrsFile = null;

        // Дочернее окно списка компонентов.
        private ComponentsWindow? сompWindow = null;

        // Дочернее окно спецификации.
        private SpecificationWindow? specWindow = null;

        /// <summary>
        /// Инициализирует главное окно и подписывается на событие изменения состояния.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.StateChanged += MainWindow_StateChanged;
        }

        /// <summary>
        /// Обрабатывает команду «Открыть»: загружает PRD-файл и соответствующий PRS-файл.
        /// Если PRS-файл отсутствует — создаёт его автоматически.
        /// </summary>
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

                    string prsFileName = Path.ChangeExtension(openFileDialog.FileName, ".prs");

                    currentPrsFile = new PRS(prsFileName);

                    if (File.Exists(prsFileName))
                    {
                        currentPrsFile.Open();
                    }
                    else
                    {
                        // Создаём PRS-файл, если он ещё не существует.
                        currentPrsFile.Create();
                        currentPrsFile.Open();
                    }

                    MessageBox.Show(
                        "Файлы успешно загружены",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Ошибка при загрузке файлов: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Обрабатывает команду «Компоненты»: открывает или переоткрывает окно списка компонентов.
        /// </summary>
        private void OpenComponents_Click(object sender, RoutedEventArgs e)
        {
            if ((currentPrdFile == null) || !currentPrdFile.IsOpen)
            {
                MessageBox.Show(
                    "Сначала откройте файл через меню 'Открыть'",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if ((сompWindow != null) && сompWindow.IsLoaded)
            {
                сompWindow.Close();
                сompWindow = null;
            }

            сompWindow = new ComponentsWindow(currentPrdFile);
            сompWindow.Show();
        }

        /// <summary>
        /// Обрабатывает команду «Спецификация»: открывает или переоткрывает окно спецификации.
        /// </summary>
        private void OpenSpecification_Click(object sender, RoutedEventArgs e)
        {
            if ((currentPrdFile == null) || !currentPrdFile.IsOpen
                || (currentPrsFile == null) || !currentPrsFile.IsOpen)
            {
                MessageBox.Show(
                    "Сначала откройте файл через меню 'Открыть'",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if ((specWindow != null) && specWindow.IsLoaded)
            {
                specWindow.Close();
                specWindow = null;
            }

            specWindow = new SpecificationWindow(currentPrdFile, currentPrsFile);
            specWindow.Show();
        }

        /// <summary>
        /// Синхронизирует состояние дочерних окон при сворачивании и восстановлении главного окна.
        /// </summary>
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                // Сворачиваем все дочерние окна
                if (сompWindow != null && сompWindow.IsLoaded)
                {
                    сompWindow.WindowState = WindowState.Minimized;
                }

                if (specWindow != null && specWindow.IsLoaded)
                {
                    specWindow.WindowState = WindowState.Minimized;
                }
            }
            else if (this.WindowState == WindowState.Normal)
            {
                if (сompWindow != null && сompWindow.IsLoaded && сompWindow.WindowState == WindowState.Minimized)
                {
                    сompWindow.WindowState = WindowState.Normal;
                }

                if (specWindow != null && specWindow.IsLoaded && specWindow.WindowState == WindowState.Minimized)
                {
                    specWindow.WindowState = WindowState.Normal;
                }
            }
            else if (this.WindowState == WindowState.Maximized)
            {
                if (сompWindow != null && сompWindow.IsLoaded && сompWindow.WindowState == WindowState.Minimized)
                {
                    сompWindow.WindowState = WindowState.Normal;
                }

                if (specWindow != null && specWindow.IsLoaded && specWindow.WindowState == WindowState.Minimized)
                {
                    specWindow.WindowState = WindowState.Normal;
                }
            }
        }

        /// <summary>
        /// Закрывает все дочерние окна при закрытии главного окна приложения.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            if (сompWindow != null && сompWindow.IsLoaded)
            {
                сompWindow.Close();
            }

            if (specWindow != null && specWindow.IsLoaded)
            {
                specWindow.Close();
            }

            base.OnClosed(e);
        }
    }
}