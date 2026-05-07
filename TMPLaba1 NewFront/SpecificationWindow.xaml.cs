// SpecificationWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TMPLAB1;

namespace TMPLaba1_NewFront
{
    /// <summary>
    /// Окно спецификации: отображает иерархическое дерево связей между компонентами
    /// и позволяет добавлять, удалять и восстанавливать связи в PRS-файле.
    /// </summary>
    public partial class SpecificationWindow : Window
    {
        // Текущий PRD-файл с данными о компонентах.
        private PRD? currentPrdFile = null;

        // Текущий PRS-файл с данными о связях между компонентами.
        private PRS? currentPrsFile = null;

        // Флаг отображения удалённых связей в дереве.
        private bool showDeleted = false;

        /// <summary>
        /// Инициализирует окно спецификации, открывает файлы и строит дерево.
        /// </summary>
        /// <param name="prd">Открытый PRD-файл.</param>
        /// <param name="prs">Открытый PRS-файл.</param>
        public SpecificationWindow(PRD prd, PRS prs)
        {
            InitializeComponent();
            currentPrdFile = prd;
            currentPrsFile = prs;

            if (!currentPrdFile.IsOpen)
            { 
                currentPrdFile.Open();
            }

            if (!currentPrsFile.IsOpen)
            { 
                currentPrsFile.Open();
            }

            SetupContextMenu();
            LoadTree();
        }

        /// <summary>
        /// Связывает обработчики событий с пунктами контекстного меню дерева.
        /// </summary>
        private void SetupContextMenu()
        {
            var contextMenu = (ContextMenu)this.Resources["TreeContextMenu"];

            var addBtn = (MenuItem)contextMenu.Items[0];
            var deleteBtn = (MenuItem)contextMenu.Items[1];
            var restoreBtn = (MenuItem)contextMenu.Items[2];

            addBtn.Click += OnAddRelation;
            deleteBtn.Click += OnDeleteRelation;
            restoreBtn.Click += OnRestoreRelation;

            // Управляем видимостью пунктов «Удалить» и «Восстановить» в зависимости от состояния узла.
            contextMenu.Opened += (s, e) =>
            {
                var menu = s as ContextMenu;
                var targetItem = menu?.PlacementTarget as TreeViewItem;

                if (targetItem?.Tag is Tuple<string, bool, bool> tagData)
                {
                    bool isDeletedRelation = tagData.Item3;

                    restoreBtn.Visibility = isDeletedRelation ? Visibility.Visible : Visibility.Collapsed;
                    deleteBtn.Visibility = !isDeletedRelation ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    restoreBtn.Visibility = Visibility.Collapsed;
                    deleteBtn.Visibility = Visibility.Visible;
                }
            };
        }

        /// <summary>
        /// Обрабатывает пункт контекстного меню «Добавить связь»:
        /// запрашивает имя детали и создаёт новую связь в PRS-файле.
        /// </summary>
        private void OnAddRelation(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var treeItem = contextMenu?.PlacementTarget as TreeViewItem;

            if (treeItem == null) return;

            string parentName = (treeItem.Tag as Tuple<string, bool, bool>)?.Item1;
            if (string.IsNullOrEmpty(parentName)) return;

            string detailName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите название детали:",
                "Добавить связь",
                "");

            if (string.IsNullOrEmpty(detailName)) return;

            try
            {
                string result = currentPrsFile.Input($"({parentName}, {detailName})");
                LoadTree();
                UpdateComponentsWindow();
                MessageBox.Show(result, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обрабатывает пункт контекстного меню «Удалить связь»:
        /// помечает выбранную связь на удаление в PRS-файле.
        /// </summary>
        private void OnDeleteRelation(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var treeItem = contextMenu?.PlacementTarget as TreeViewItem;

            if (treeItem == null) return;

            var parentItem = treeItem.Parent as TreeViewItem;
            if (parentItem == null)
            {
                MessageBox.Show(
                    "Нельзя удалить корневой элемент",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string parentName = (parentItem.Tag as Tuple<string, bool, bool>)?.Item1;
            string childDisplayName = treeItem.Header?.ToString();

            // Убираем суффикс кратности вида «(xN)» из отображаемого имени.
            string childName = childDisplayName?.Split('(')[0].Trim();

            if (MessageBox.Show(
                    $"Удалить связь '{parentName} → {childName}'?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    string result = currentPrsFile.Delete($"({parentName}, {childName})");
                    LoadTree();
                    MessageBox.Show(result, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Обрабатывает пункт контекстного меню «Восстановить связь»:
        /// снимает пометку удаления с выбранной связи.
        /// </summary>
        private void OnRestoreRelation(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var treeItem = contextMenu?.PlacementTarget as TreeViewItem;

            if (treeItem == null) return;

            var parentItem = treeItem.Parent as TreeViewItem;
            if (parentItem == null)
            {
                MessageBox.Show(
                    "Нельзя восстановить корневой элемент",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string parentName = (parentItem.Tag as Tuple<string, bool, bool>)?.Item1;
            string childDisplayName = treeItem.Header?.ToString();

            // Убираем суффикс кратности вида «(xN)» из отображаемого имени.
            string childName = childDisplayName?.Split('(')[0].Trim();

            try
            {
                currentPrsFile.Restore($"({parentName}, {childName})");
                LoadTree();
                UpdateComponentsWindow();
                MessageBox.Show(
                    $"Связь '{parentName} → {childName}' восстановлена",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обрабатывает нажатие кнопки «Сжать»: окончательно удаляет помеченные связи из PRS-файла.
        /// </summary>
        private void BtnTruncate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allRelations = GetAllRelations();
                int deletedRelationsCount = allRelations.Count(r => r.IsDeleted);

                if (deletedRelationsCount == 0)
                {
                    MessageBox.Show(
                        "Нет связей, помеченных на удаление",
                        "Информация",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Локальная функция возвращает правильное окончание слова «связь» для числа count.
                string GetEnding(int count)
                {
                    int lastDigit = count % 10;
                    int lastTwoDigits = count % 100;

                    if ((lastTwoDigits >= 11) && (lastTwoDigits <= 19))
                    { 
                        return "ей";
                    }
                        
                    return lastDigit switch
                    {
                        1 => "ь",
                        2 or 3 or 4 => "и",
                        _ => "ей"
                    };
                }

                MessageBoxResult result = MessageBox.Show(
                    $"Окончательно удалить {deletedRelationsCount} {"связ" + GetEnding(deletedRelationsCount)}?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    currentPrsFile.Truncate();
                    LoadTree();
                    UpdateComponentsWindow();
                    MessageBox.Show(
                        "Файл спецификации успешно сжат. Удалённые связи очищены.",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при сжатии: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Переключает отображение удалённых записей и перестраивает дерево.
        /// </summary>
        private void BtnShowDeleted_Click(object sender, RoutedEventArgs e)
        {
            showDeleted = !showDeleted;

            Button btn = sender as Button;
            btn.Content = showDeleted ? "Скрыть удаленные" : "Показать удаленные";

            LoadTree();
        }

        /// <summary>
        /// Обрабатывает нажатие кнопки «Восстановить все»: снимает пометку удаления со всех связей.
        /// </summary>
        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allRelations = GetAllRelations();
                int deletedRelationsCount = allRelations.Count(r => r.IsDeleted);

                if (deletedRelationsCount == 0)
                {
                    MessageBox.Show(
                        "Нет удалённых связей для восстановления",
                        "Информация",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                MessageBoxResult result = MessageBox.Show(
                    $"Восстановить все ({deletedRelationsCount}) удалённые связи?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Символ «*» означает восстановление всех помеченных записей.
                    currentPrsFile.Restore("*");
                    LoadTree();
                    UpdateComponentsWindow();
                    MessageBox.Show(
                        $"Восстановлено связей: {deletedRelationsCount}",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при восстановлении: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обрабатывает нажатие кнопки «Добавить изделие»:
        /// запрашивает имена родителя и потомка, затем добавляет связь.
        /// </summary>
        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            string parentName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите имя изделия (родитель):",
                "Добавить изделие",
                "");

            if (string.IsNullOrEmpty(parentName)) return;

            string childName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите имя комплектующего (ребёнок):",
                "Добавить изделие",
                "");

            if (string.IsNullOrEmpty(childName)) return;

            try
            {
                string result = currentPrsFile.Input($"({parentName}, {childName})");
                LoadTree();
                UpdateComponentsWindow();
                MessageBox.Show(result, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Перестраивает дерево спецификации на основе актуальных данных из файлов.
        /// </summary>
        /// <param name="forceRootName">Имя компонента, который принудительно добавляется в корень дерева.</param>
        private void LoadTree(string forceRootName = null)
        {
            try
            {
                SpecTreeView.Items.Clear();

                var components = GetAllComponents();
                var relations = GetAllRelations();
                var relationsMap = BuildRelationsMap(relations);

                var rootComponents = FindRootComponents(components, relationsMap);

                if (!string.IsNullOrEmpty(forceRootName))
                {
                    var forceRoot = components.FirstOrDefault(c => c.Name == forceRootName);
                    if ((forceRoot != null) && rootComponents.All(r => r.Name != forceRootName))
                        rootComponents.Add(forceRoot);
                }

                if ((rootComponents.Count == 0) && (relations.Count == 0))
                {
                    SpecTreeView.Items.Add(new TreeViewItem { Header = "Нет связей в спецификации" });
                    return;
                }

                if (rootComponents.Count == 0)
                {
                    SpecTreeView.Items.Add(new TreeViewItem { Header = "Нет корневых компонентов (изделий)" });
                    return;
                }

                foreach (var root in rootComponents.OrderBy(r => r.Name))
                {
                    if (!root.IsDeleted || showDeleted)
                    {
                        TreeViewItem rootItem = CreateTreeItem(root.Name, root.IsDeleted, false);
                        BuildTree(rootItem, root.Name, relationsMap, components, new HashSet<string>());
                        SpecTreeView.Items.Add(rootItem);
                    }
                }
            }
            catch (Exception ex)
            {
                SpecTreeView.Items.Add(new TreeViewItem { Header = $"Ошибка: {ex.Message}" });
                MessageBox.Show($"Ошибка при загрузке дерева: {ex.Message}");
            }
        }

        /// <summary>
        /// Читает все компоненты из PRD-файла и определяет, является ли каждый из них деталью,
        /// на основе данных PRS-файла.
        /// </summary>
        /// <returns>Список объектов <see cref="ComponentInfo"/>.</returns>
        private List<ComponentInfo> GetAllComponents()
        {
            var components = new List<ComponentInfo>();

            string prsFileName = Path.ChangeExtension(currentPrdFile.CurrentFileName, ".prs");

            // Словарь: смещение компонента → true, если компонент является деталью (только в детальном множестве).
            var typeMap = new Dictionary<int, bool>();

            if (File.Exists(prsFileName))
            {
                using (FileStream prsStream = new FileStream(prsFileName, FileMode.Open, FileAccess.Read))
                using (BinaryReader prsReader = new BinaryReader(prsStream))
                {
                    prsStream.Seek(0, SeekOrigin.Begin);
                    int firstRecord = prsReader.ReadInt32();

                    var productSet = new HashSet<int>();
                    var detailSet = new HashSet<int>();

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

                    var allComponents = new HashSet<int>(productSet);
                    allComponents.UnionWith(detailSet);

                    // Компонент — деталь, если он присутствует только в множестве детальных смещений.
                    foreach (int compOffset in allComponents)
                    {
                        bool inProduct = productSet.Contains(compOffset);
                        bool inDetail = detailSet.Contains(compOffset);

                        typeMap[compOffset] = (!inProduct && inDetail);
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

                    byte flagDelete = br.ReadByte();
                    int pFirstComp = br.ReadInt32();
                    int pNext = br.ReadInt32();
                    byte[] nameBytes = br.ReadBytes(currentPrdFile.Header.RecordLen);
                    string name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

                    // Если тип неизвестен по PRS — считаем деталью при отсутствии дочерних компонентов.
                    bool isDetail = typeMap.ContainsKey(offset) ? typeMap[offset] : (pFirstComp == -1);

                    components.Add(new ComponentInfo
                    {
                        Name = name,
                        Offset = offset,
                        IsDeleted = (flagDelete == 0xFF),
                        IsDetail = isDetail,
                        P_FirstComp = pFirstComp
                    });

                    offset = pNext;
                }
            }

            return components;
        }

        /// <summary>
        /// Читает все связи из PRS-файла, разрешая имена компонентов через PRD-файл.
        /// </summary>
        /// <returns>Список объектов <see cref="RelationInfo"/>.</returns>
        private List<RelationInfo> GetAllRelations()
        {
            var relations = new List<RelationInfo>();

            string prdFileName = currentPrdFile.CurrentFileName;

            using (FileStream prsStream = new FileStream(currentPrsFile.CurrentFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader prsReader = new BinaryReader(prsStream))
            using (FileStream prdStream = new FileStream(prdFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader prdReader = new BinaryReader(prdStream))
            {
                prsStream.Seek(0, SeekOrigin.Begin);
                int firstRecord = prsReader.ReadInt32();
                int freeSpace = prsReader.ReadInt32();

                // Читаем длину записи PRD из заголовка для корректного чтения имён.
                prdStream.Seek(2, SeekOrigin.Begin);
                ushort prdRecordLen = prdReader.ReadUInt16();

                int currentOffset = firstRecord;

                while ((currentOffset != -1) && (currentOffset < prsStream.Length))
                {
                    prsStream.Seek(currentOffset, SeekOrigin.Begin);

                    byte flagDelete = prsReader.ReadByte();
                    int pProduct = prsReader.ReadInt32();
                    int pDetail = prsReader.ReadInt32();
                    ushort multiOccurrence = prsReader.ReadUInt16();
                    int pNext = prsReader.ReadInt32();

                    string productName = GetComponentNameByOffset(prdStream, prdReader, pProduct, prdRecordLen);
                    string detailName = GetComponentNameByOffset(prdStream, prdReader, pDetail, prdRecordLen);

                    if (!string.IsNullOrEmpty(productName) && !string.IsNullOrEmpty(detailName))
                    {
                        relations.Add(new RelationInfo
                        {
                            ParentName = productName,
                            ParentOffset = pProduct,
                            ChildName = detailName,
                            ChildOffset = pDetail,
                            Count = multiOccurrence,
                            IsDeleted = (flagDelete == 0xFF),
                            Offset = currentOffset
                        });
                    }

                    currentOffset = pNext;
                }
            }

            return relations;
        }

        /// <summary>
        /// Возвращает имя компонента, читая его из PRD-файла по указанному смещению.
        /// </summary>
        /// <param name="prdStream">Поток PRD-файла.</param>
        /// <param name="prdReader">Читатель PRD-файла.</param>
        /// <param name="offset">Смещение записи компонента.</param>
        /// <param name="recordLen">Длина поля имени в байтах.</param>
        /// <returns>Имя компонента или null при ошибке чтения.</returns>
        private string GetComponentNameByOffset(
            FileStream prdStream,
            BinaryReader prdReader,
            int offset,
            ushort recordLen)
        {
            if (offset == -1) return null;

            try
            {
                prdStream.Seek(offset, SeekOrigin.Begin);

                // Пропускаем служебные поля: FlagDelete (1), p_FirstComp (4), p_Next (4).
                prdReader.ReadByte();
                prdReader.ReadInt32();
                prdReader.ReadInt32();

                byte[] nameBytes = prdReader.ReadBytes(recordLen);
                return Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Строит словарь «имя родителя → список связей» для быстрого обхода дерева.
        /// Учитывает флаг <see cref="showDeleted"/> при фильтрации.
        /// </summary>
        /// <param name="relations">Полный список связей из PRS-файла.</param>
        /// <returns>Словарь связей, сгруппированных по имени родителя.</returns>
        private Dictionary<string, List<RelationInfo>> BuildRelationsMap(List<RelationInfo> relations)
        {
            var map = new Dictionary<string, List<RelationInfo>>();

            foreach (var relation in relations)
            {
                if (!relation.IsDeleted || showDeleted)
                {
                    if (!map.ContainsKey(relation.ParentName))
                        map[relation.ParentName] = new List<RelationInfo>();

                    map[relation.ParentName].Add(relation);
                }
            }

            // Сортируем дочерние элементы каждого узла по имени для стабильного отображения.
            foreach (var key in map.Keys.ToList())
            {
                map[key] = map[key].OrderBy(r => r.ChildName).ToList();
            }

            return map;
        }

        /// <summary>
        /// Определяет корневые компоненты: те, которые не являются дочерними ни в одной связи
        /// и не помечены как детали.
        /// </summary>
        /// <param name="components">Список всех компонентов.</param>
        /// <param name="relationsMap">Карта связей по имени родителя.</param>
        /// <returns>Список корневых компонентов.</returns>
        private List<ComponentInfo> FindRootComponents(
            List<ComponentInfo> components,
            Dictionary<string, List<RelationInfo>> relationsMap)
        {
            var allChildren = new HashSet<string>();

            foreach (var relations in relationsMap.Values)
            {
                foreach (var relation in relations)
                {
                    allChildren.Add(relation.ChildName);
                }
            }

            var roots = components
                .Where(c => !allChildren.Contains(c.Name) && !c.IsDetail && (!c.IsDeleted || showDeleted))
                .ToList();

            // Запасной вариант: если корни не найдены, показываем все узлы, имеющие дочерние элементы.
            if (roots.Count == 0)
            {
                roots = components
                    .Where(c => relationsMap.ContainsKey(c.Name) && (!c.IsDeleted || showDeleted))
                    .ToList();
            }

            return roots;
        }

        /// <summary>
        /// Создаёт элемент дерева с заданным именем и визуальным стилем,
        /// отражающим состояние удаления компонента или связи.
        /// </summary>
        /// <param name="name">Отображаемое имя узла.</param>
        /// <param name="isDeletedComponent">Признак удалённого компонента.</param>
        /// <param name="isDeletedRelation">Признак удалённой связи.</param>
        /// <returns>Готовый элемент <see cref="TreeViewItem"/>.</returns>
        private TreeViewItem CreateTreeItem(string name, bool isDeletedComponent, bool isDeletedRelation)
        {
            var item = new TreeViewItem
            {
                Header = name,
                Tag = new Tuple<string, bool, bool>(name, isDeletedComponent, isDeletedRelation)
            };

            // Удалённые узлы отображаются серым курсивом для визуального различения.
            if (isDeletedComponent || isDeletedRelation)
            {
                item.Foreground = System.Windows.Media.Brushes.Gray;
                item.FontStyle = FontStyles.Italic;
            }

            item.ContextMenu = (ContextMenu)this.Resources["TreeContextMenu"];

            return item;
        }

        /// <summary>
        /// Рекурсивно строит поддерево дочерних элементов для указанного родительского узла.
        /// Использует множество <paramref name="visited"/> для защиты от циклических зависимостей.
        /// </summary>
        /// <param name="parentItem">Родительский элемент дерева WPF.</param>
        /// <param name="parentName">Имя родительского компонента.</param>
        /// <param name="relationsMap">Карта связей по имени родителя.</param>
        /// <param name="components">Список всех компонентов.</param>
        /// <param name="visited">Множество уже посещённых имён для исключения циклов.</param>
        private void BuildTree(
            TreeViewItem parentItem,
            string parentName,
            Dictionary<string, List<RelationInfo>> relationsMap,
            List<ComponentInfo> components,
            HashSet<string> visited)
        {
            // Прерываем рекурсию при обнаружении уже посещённого узла.
            if (visited.Contains(parentName)) return;

            visited.Add(parentName);

            if (!relationsMap.ContainsKey(parentName)) return;

            foreach (var relation in relationsMap[parentName])
            {
                // Добавляем суффикс кратности, если компонент входит более одного раза.
                string displayName = (relation.Count > 1)
                    ? $"{relation.ChildName} (x{relation.Count})"
                    : relation.ChildName;

                var childComponent = components.FirstOrDefault(c => c.Name == relation.ChildName);
                bool isChildDeleted = childComponent?.IsDeleted ?? false;

                TreeViewItem childItem = CreateTreeItem(displayName, isChildDeleted, relation.IsDeleted);
                parentItem.Items.Add(childItem);

                // Рекурсивно раскрываем только не-детали и не удалённые (если флаг не установлен).
                if ((childComponent != null) && !childComponent.IsDetail && (!childComponent.IsDeleted || showDeleted))
                {
                    BuildTree(childItem, relation.ChildName, relationsMap, components, new HashSet<string>(visited));
                }
            }
        }

        /// <summary>
        /// Обновляет окно компонентов (если оно открыто) после изменения данных в спецификации.
        /// </summary>
        private void UpdateComponentsWindow()
        {
            // Находим открытое окно компонентов и обновляем его.
            foreach (Window window in Application.Current.Windows)
            {
                if (window is ComponentsWindow compWindow && compWindow.IsLoaded)
                {
                    compWindow.Refresh();
                    break;
                }
            }
        }

        /// <summary>
        /// Хранит информацию об одном компоненте PRD-файла.
        /// </summary>
        private class ComponentInfo
        {
            /// <summary>Имя компонента.</summary>
            public string Name { get; set; }

            /// <summary>Байтовое смещение записи в PRD-файле.</summary>
            public int Offset { get; set; }

            /// <summary>Признак удалённой записи.</summary>
            public bool IsDeleted { get; set; }

            /// <summary>True, если компонент является конечной деталью (не имеет дочерних).</summary>
            public bool IsDetail { get; set; }

            /// <summary>Смещение первого дочернего компонента или -1, если дочерних нет.</summary>
            public int P_FirstComp { get; set; }
        }

        /// <summary>
        /// Хранит информацию об одной связи «изделие – деталь» из PRS-файла.
        /// </summary>
        private class RelationInfo
        {
            /// <summary>Имя родительского компонента.</summary>
            public string ParentName { get; set; }

            /// <summary>Байтовое смещение родителя в PRD-файле.</summary>
            public int ParentOffset { get; set; }

            /// <summary>Имя дочернего компонента.</summary>
            public string ChildName { get; set; }

            /// <summary>Байтовое смещение дочернего компонента в PRD-файле.</summary>
            public int ChildOffset { get; set; }

            /// <summary>Количество вхождений дочернего компонента в изделие.</summary>
            public ushort Count { get; set; }

            /// <summary>Признак удалённой связи.</summary>
            public bool IsDeleted { get; set; }

            /// <summary>Байтовое смещение записи в PRS-файле.</summary>
            public int Offset { get; set; }
        }
    }
}