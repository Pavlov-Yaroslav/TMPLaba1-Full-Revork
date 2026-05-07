// ComponentsWindow.xaml.cs
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TMPLAB1;

namespace TMPLaba1_NewFront
{
    /// <summary>
    /// Окно управления компонентами: просмотр, добавление, удаление и восстановление записей PRD-файла.
    /// </summary>
    public partial class ComponentsWindow : Window
    {
        // Текущий открытый PRD-файл.
        private PRD currentPrdFile;

        /// <summary>
        /// Коллекция компонентов, отображаемых в таблице.
        /// </summary>
        public ObservableCollection<Component> Components { get; set; } = new ObservableCollection<Component>();

        /// <summary>
        /// Представляет один компонент из PRD-файла.
        /// </summary>
        public class Component
        {
            /// <summary>Имя компонента.</summary>
            public string Name { get; set; }

            /// <summary>Тип компонента: Изделие, Узел или Деталь.</summary>
            public string Type { get; set; }

            /// <summary>Признак того, что компонент помечен на удаление.</summary>
            public bool IsDeleted { get; set; } = false;
        }

        /// <summary>
        /// Формирует полный список компонентов из PRD-файла с определением типа каждого
        /// на основе данных связанного PRS-файла.
        /// </summary>
        /// <param name="currentPrdFile">Текущий PRD-файл.</param>
        /// <returns>Список компонентов с именем, типом и признаком удаления.</returns>
        public List<Component> GetAllComponents(ref PRD currentPrdFile)
        {
            var components = new List<Component>();

            string prsFileName = Encoding.UTF8.GetString(currentPrdFile.Header.NameSpec).TrimEnd('\0');
            string prsFullPath = Path.Combine(
                Path.GetDirectoryName(currentPrdFile.CurrentFileName) ?? string.Empty,
                prsFileName);

            // Словарь: смещение компонента → его тип (Изделие / Узел / Деталь).
            var typeMap = new Dictionary<int, string>();

            if (File.Exists(prsFullPath))
            {
                using (FileStream prsStream = new FileStream(prsFullPath, FileMode.Open, FileAccess.Read))
                using (BinaryReader prsReader = new BinaryReader(prsStream))
                {
                    prsStream.Seek(0, SeekOrigin.Begin);
                    int firstRecord = prsReader.ReadInt32();

                    var productSet = new HashSet<int>();
                    var detailSet = new HashSet<int>();

                    // Обход всех записей PRS-файла для сбора множеств изделий и деталей.
                    int offset = firstRecord;
                    while ((offset != -1) && (offset < prsStream.Length))
                    {
                        prsStream.Seek(offset, SeekOrigin.Begin);
                        byte flagDelete = prsReader.ReadByte();
                        int pProduct = prsReader.ReadInt32();
                        int pDetail = prsReader.ReadInt32();
                        prsReader.ReadUInt16();
                        int pNext = prsReader.ReadInt32();

                        if (flagDelete != 0xFF)
                        {
                            productSet.Add(pProduct);
                            detailSet.Add(pDetail);
                        }

                        offset = pNext;
                    }

                    // Объединение двух множеств для перебора всех уникальных компонентов.
                    var allComponents = new HashSet<int>(productSet);
                    allComponents.UnionWith(detailSet);

                    foreach (int compOffset in allComponents)
                    {
                        bool inProduct = productSet.Contains(compOffset);
                        bool inDetail = detailSet.Contains(compOffset);

                        // Компонент является изделием, если он только в productSet.
                        // Компонент является узлом, если он в обоих множествах.
                        // Иначе — деталь.
                        if (inProduct && !inDetail)
                        {
                            typeMap[compOffset] = "Изделие";
                        }
                        else if (inProduct && inDetail)
                        {
                            typeMap[compOffset] = "Узел";
                        }
                        else
                        { 
                            typeMap[compOffset] = "Деталь";
                        }
                    }
                }
            }

            using (FileStream fs = new FileStream(currentPrdFile.CurrentFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                int offset = currentPrdFile.Header.p_FirstRecord;

                while ((offset != -1) && (offset < fs.Length))
                {
                    fs.Seek(offset, SeekOrigin.Begin);

                    currentPrdFile.Record.FlagDelete = br.ReadByte();
                    currentPrdFile.Record.p_FirstComp = br.ReadInt32();
                    currentPrdFile.Record.p_Next = br.ReadInt32();
                    currentPrdFile.Record.Name = br.ReadBytes(currentPrdFile.Header.RecordLen);
                    string name = Encoding.UTF8.GetString(currentPrdFile.Record.Name).TrimEnd('\0');

                    string type = typeMap.ContainsKey(offset) ? typeMap[offset] : "Деталь";
                    bool isDeleted = (currentPrdFile.Record.FlagDelete == 0xFF);

                    components.Add(new Component
                    {
                        Name = name,
                        Type = type,
                        IsDeleted = isDeleted
                    });

                    offset = currentPrdFile.Record.p_Next;
                }
            }

            return components;
        }

        /// <summary>
        /// Инициализирует окно и загружает компоненты из переданного PRD-файла.
        /// </summary>
        /// <param name="prdFile">Открытый PRD-файл для отображения.</param>
        public ComponentsWindow(PRD prdFile)
        {
            InitializeComponent();
            currentPrdFile = prdFile;
            ComponentsGrid.ItemsSource = Components;
            LoadComponents();
        }

        /// <summary>
        /// Считывает компоненты из файла, сортирует по имени и обновляет коллекцию.
        /// </summary>
        private void LoadComponents()
        {
            if (currentPrdFile == null) return;

            try
            {
                List<Component> components = GetAllComponents(ref currentPrdFile);
                var sortedComponents = components.OrderBy(c => c.Name).ToList();

                Components.Clear();
                foreach (var component in sortedComponents)
                {
                    Components.Add(component);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при загрузке компонентов: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обрабатывает нажатие кнопки «Добавить»: валидирует поля ввода и добавляет новый компонент.
        /// </summary>
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text.Trim();
            string type = (TypeTextBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type))
            {
                MessageBox.Show("Введите оба поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if ((type != "Изделие") && (type != "Узел") && (type != "Деталь"))
            {
                MessageBox.Show(
                    "Тип должен быть: Изделие, Узел или Деталь",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!currentPrdFile.IsOpen)
                { 
                    currentPrdFile.Open();
                }
                   
                string inputArgument = $"({name}, {type})";
                currentPrdFile.Input(inputArgument);

                LoadComponents();

                NameTextBox.Text = "";
                TypeTextBox.SelectedIndex = -1;

                MessageBox.Show(
                    $"Компонент '{name}' типа '{type}' успешно добавлен",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при добавлении компонента: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обрабатывает нажатие кнопки «Удалить»: помечает выбранный компонент на удаление.
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComponentsGrid.SelectedItem == null)
            {
                MessageBox.Show(
                    "Выберите компонент для удаления",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Component selected = (Component)ComponentsGrid.SelectedItem;

            if (selected.IsDeleted)
            {
                MessageBox.Show(
                    "Компонент уже помечен на удаление",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"Удалить компонент '{selected.Name}'?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (!currentPrdFile.IsOpen)
                        currentPrdFile.Open();

                    currentPrdFile.Delete(selected.Name);

                    LoadComponents();

                    MessageBox.Show(
                        $"Компонент '{selected.Name}' помечен на удаление",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Ошибка при удалении компонента: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Обрабатывает нажатие кнопки «Восстановить»: снимает пометку удаления с выбранного компонента.
        /// </summary>
        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComponentsGrid.SelectedItem == null)
            {
                MessageBox.Show(
                    "Выберите компонент для восстановления",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Component selected = (Component)ComponentsGrid.SelectedItem;

            if (!selected.IsDeleted)
            {
                MessageBox.Show(
                    "Компонент не помечен на удаление",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!currentPrdFile.IsOpen)
                { 
                    currentPrdFile.Open();
                }

                currentPrdFile.Restore(selected.Name);

                LoadComponents();

                MessageBox.Show(
                    $"Компонент '{selected.Name}' восстановлен",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при восстановлении компонента: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обрабатывает нажатие кнопки «Восстановить все»: снимает пометку удаления со всех компонентов.
        /// </summary>
        private void RestoreAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!currentPrdFile.IsOpen)
                { 
                    currentPrdFile.Open();
                }

                // Символ «*» означает восстановление всех помеченных записей.
                currentPrdFile.Restore("*");

                LoadComponents();

                MessageBox.Show("Все компоненты восстановлены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при восстановлении компонентов: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обрабатывает нажатие кнопки «Сжать»: окончательно удаляет помеченные записи из файла.
        /// </summary>
        private void TruncateButton_Click(object sender, RoutedEventArgs e)
        {
            int deletedCount = Components.Count(c => c.IsDeleted);

            if (deletedCount == 0)
            {
                MessageBox.Show(
                    "Нет компонентов, помеченных на удаление",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Локальная функция возвращает правильное окончание слова «компонент» для числа count.
            string GetComponentEnding(int count)
            {
                int lastDigit = count % 10;
                int lastTwoDigits = count % 100;

                if ((lastTwoDigits >= 11) && (lastTwoDigits <= 19))
                { 
                    return "ов";
                }                
                   

                return lastDigit switch
                {
                    1 => "",
                    2 or 3 or 4 => "а",
                    _ => "ов"
                };
            }

            MessageBoxResult result = MessageBox.Show(
                $"Окончательно удалить {deletedCount} {"компонент" + GetComponentEnding(deletedCount)}?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (!currentPrdFile.IsOpen)
                    { 
                        currentPrdFile.Open();
                    }
                        
                    currentPrdFile.Truncate();
                    LoadComponents();

                    MessageBox.Show(
                        "Файл успешно сжат. Удаленные записи очищены.",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Ошибка при сжатии файла: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Перезагружает список компонентов из файла (вызывается из других окон при изменении данных).
        /// </summary>
        public void Refresh()
        {
            LoadComponents();
        }
    }
}