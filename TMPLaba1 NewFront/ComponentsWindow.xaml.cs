using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using TMPLAB1;
using static System.Net.Mime.MediaTypeNames;

namespace TMPLaba1_NewFront
{
    public partial class ComponentsWindow : Window
    {
        private PRD currentPrdFile;
        public ObservableCollection<Component> Components { get; set; } = new ObservableCollection<Component>();

        public class Component
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool IsDeleted { get; set; } = false;
        }

        public List<Component> GetAllComponents(ref PRD currentPrdFile)
        {
            var components = new List<Component>();

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

                    string type = currentPrdFile.Record.IsDetail ? "Деталь" : "Узел/Изделие";
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

        public ComponentsWindow(PRD prdFile)
        {
            InitializeComponent();
            currentPrdFile = prdFile;

            ComponentsGrid.ItemsSource = Components;

            LoadComponents();
        }

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
                MessageBox.Show($"Ошибка при загрузке компонентов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                MessageBox.Show("Тип должен быть: Изделие, Узел или Деталь", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                MessageBox.Show($"Компонент '{name}' типа '{type}' успешно добавлен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении компонента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComponentsGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите компонент для удаления", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Component selected = (Component)ComponentsGrid.SelectedItem;

            if (selected.IsDeleted)
            {
                MessageBox.Show("Компонент уже помечен на удаление", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult result = MessageBox.Show($"Удалить компонент '{selected.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (!currentPrdFile.IsOpen)
                    {
                        currentPrdFile.Open();
                    }

                    currentPrdFile.Delete(selected.Name);

                    LoadComponents();

                    MessageBox.Show($"Компонент '{selected.Name}' помечен на удаление", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении компонента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComponentsGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите компонент для восстановления", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Component selected = (Component)ComponentsGrid.SelectedItem;

            if (!selected.IsDeleted)
            {
                MessageBox.Show("Компонент не помечен на удаление", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                MessageBox.Show($"Компонент '{selected.Name}' восстановлен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при восстановлении компонента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!currentPrdFile.IsOpen)
                {
                    currentPrdFile.Open();
                }

                currentPrdFile.Restore("*");

                LoadComponents();

                MessageBox.Show("Все компоненты восстановлены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при восстановлении компонентов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TruncateButton_Click(object sender, RoutedEventArgs e)
        {
            int deletedCount = Components.Count(c => c.IsDeleted);

            if (deletedCount == 0)
            {
                MessageBox.Show("Нет компонентов, помеченных на удаление", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string GetComponentEnding(int count)
            {
                int lastDigit = count % 10;
                int lastTwoDigits = count % 100;

                if (lastTwoDigits >= 11 && lastTwoDigits <= 19)
                    return "ов";

                return lastDigit switch
                {
                    1 => "",
                    2 or 3 or 4 => "а",
                    _ => "ов"
                };
            }

            MessageBoxResult result = MessageBox.Show(
                $"Окончательно удалить {deletedCount} {"компонент" + GetComponentEnding(deletedCount)}?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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

                    MessageBox.Show("Файл успешно сжат. Удаленные записи очищены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сжатии файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
