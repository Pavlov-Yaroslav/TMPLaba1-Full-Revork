using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TMPLAB1;

namespace TMPLaba1_NewFront
{
    public partial class SpecificationWindow : Window
    {
        private PRD? currentPrdFile = null;
        private PRS? currentPrsFile = null;
        private bool showDeleted = false;

        public SpecificationWindow(PRD prd, PRS prs)
        {
            InitializeComponent();
            currentPrdFile = prd;
            currentPrsFile = prs;

            if (!currentPrdFile.IsOpen)
                currentPrdFile.Open();

            if (!currentPrsFile.IsOpen)
                currentPrsFile.Open();

            SetupContextMenu();
            LoadTree();
        }

        private void SetupContextMenu()
        {
            var contextMenu = (ContextMenu)this.Resources["TreeContextMenu"];

            var addBtn = (MenuItem)contextMenu.Items[0];
            var deleteBtn = (MenuItem)contextMenu.Items[1];
            var restoreBtn = (MenuItem)contextMenu.Items[2];

            addBtn.Click += OnAddRelation;
            deleteBtn.Click += OnDeleteRelation;
            restoreBtn.Click += OnRestoreRelation;

            contextMenu.Opened += (s, e) =>
            {
                var menu = s as ContextMenu;
                var targetItem = menu?.PlacementTarget as TreeViewItem;

                if (targetItem?.Tag is Tuple<string, bool, bool> tagData)
                {
                    bool isDeletedComponent = tagData.Item2;
                    bool isDeletedRelation = tagData.Item3;

                    // Кнопка "Восстановить" видна только для удалённых СВЯЗЕЙ
                    restoreBtn.Visibility = isDeletedRelation ? Visibility.Visible : Visibility.Collapsed;
                    // Кнопка "Удалить" видна только для живых связей
                    deleteBtn.Visibility = !isDeletedRelation ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    restoreBtn.Visibility = Visibility.Collapsed;
                    deleteBtn.Visibility = Visibility.Visible;
                }
            };
        }

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
                MessageBox.Show(result, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDeleteRelation(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var treeItem = contextMenu?.PlacementTarget as TreeViewItem;

            if (treeItem == null) return;

            var parentItem = treeItem.Parent as TreeViewItem;
            if (parentItem == null)
            {
                MessageBox.Show("Нельзя удалить корневой элемент", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string parentName = (parentItem.Tag as Tuple<string, bool, bool>)?.Item1;
            string childDisplayName = treeItem.Header?.ToString();
            string childName = childDisplayName?.Split('(')[0].Trim();

            if (MessageBox.Show($"Удалить связь '{parentName} → {childName}'?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
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

        private void OnRestoreRelation(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var treeItem = contextMenu?.PlacementTarget as TreeViewItem;

            if (treeItem == null) return;

            var parentItem = treeItem.Parent as TreeViewItem;
            if (parentItem == null)
            {
                MessageBox.Show("Нельзя восстановить корневой элемент", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string parentName = (parentItem.Tag as Tuple<string, bool, bool>)?.Item1;
            string childDisplayName = treeItem.Header?.ToString();
            string childName = childDisplayName?.Split('(')[0].Trim();

            try
            {
                currentPrsFile.Restore($"({parentName}, {childName})");
                LoadTree();
                MessageBox.Show($"Связь '{parentName} → {childName}' восстановлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============= КНОПКИ ОКНА =============

        // Кнопка "Удалить окончательно (Сжать)"
        private void BtnTruncate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем все связи
                var allRelations = GetAllRelations();
                int deletedRelationsCount = allRelations.Count(r => r.IsDeleted);

                if (deletedRelationsCount == 0)
                {
                    MessageBox.Show("Нет связей, помеченных на удаление", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string GetEnding(int count)
                {
                    int lastDigit = count % 10;
                    int lastTwoDigits = count % 100;

                    if (lastTwoDigits >= 11 && lastTwoDigits <= 19)
                        return "ей";

                    return lastDigit switch
                    {
                        1 => "ь",
                        2 or 3 or 4 => "и",
                        _ => "ей"
                    };
                }

                MessageBoxResult result = MessageBox.Show(
                    $"Окончательно удалить {deletedRelationsCount} {"связ" + GetEnding(deletedRelationsCount)}?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    currentPrsFile.Truncate();
                    LoadTree();
                    MessageBox.Show("Файл спецификации успешно сжат. Удалённые связи очищены.",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сжатии: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Кнопка "Показать удаленные"
        private void BtnShowDeleted_Click(object sender, RoutedEventArgs e)
        {
            showDeleted = !showDeleted;

            Button btn = sender as Button;
            btn.Content = showDeleted ? "Скрыть удаленные" : "Показать удаленные";

            LoadTree();
        }

        // Кнопка "Восстановить все"
        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allRelations = GetAllRelations();
                int deletedRelationsCount = allRelations.Count(r => r.IsDeleted);

                if (deletedRelationsCount == 0)
                {
                    MessageBox.Show("Нет удалённых связей для восстановления", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBoxResult result = MessageBox.Show(
                    $"Восстановить все ({deletedRelationsCount}) удалённые связи?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    currentPrsFile.Restore("*");
                    LoadTree();
                    MessageBox.Show($"Восстановлено связей: {deletedRelationsCount}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при восстановлении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                MessageBox.Show(result, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============= ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ =============

        private void LoadTree(string forceRootName = null)
        {
            try
            {
                SpecTreeView.Items.Clear();

                var components = GetAllComponents();
                var relations = GetAllRelations();
                var relationsMap = BuildRelationsMap(relations);

                var rootComponents = FindRootComponents(components, relationsMap);

                // Если явно задан корень — добавляем его, если его ещё нет в списке
                if (!string.IsNullOrEmpty(forceRootName))
                {
                    var forceRoot = components.FirstOrDefault(c => c.Name == forceRootName);
                    if (forceRoot != null && rootComponents.All(r => r.Name != forceRootName))
                    {
                        rootComponents.Add(forceRoot);
                    }
                }

                if (rootComponents.Count == 0 && relations.Count == 0)
                {
                    SpecTreeView.Items.Add(new TreeViewItem { Header = "Нет связей в спецификации" });
                    return;
                }

                if (rootComponents.Count == 0)
                {
                    SpecTreeView.Items.Add(new TreeViewItem { Header = "Нет корневых компонентов (изделий)" });
                    return;
                }

                // ОДИН цикл, отсортированный
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

        private List<ComponentInfo> GetAllComponents()
        {
            var components = new List<ComponentInfo>();

            using (FileStream fs = new FileStream(currentPrdFile.CurrentFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                int offset = currentPrdFile.Header.p_FirstRecord;

                while (offset != -1 && offset < fs.Length)
                {
                    fs.Seek(offset, SeekOrigin.Begin);

                    byte flagDelete = br.ReadByte();
                    int p_FirstComp = br.ReadInt32();
                    int p_Next = br.ReadInt32();
                    byte[] nameBytes = br.ReadBytes(currentPrdFile.Header.RecordLen);
                    string name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

                    components.Add(new ComponentInfo
                    {
                        Name = name,
                        Offset = offset,
                        IsDeleted = flagDelete == 0xFF,
                        IsDetail = p_FirstComp == -1,
                        P_FirstComp = p_FirstComp
                    });

                    offset = p_Next;
                }
            }

            return components;
        }

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

                prdStream.Seek(2, SeekOrigin.Begin);
                ushort prdRecordLen = prdReader.ReadUInt16();

                int currentOffset = firstRecord;

                while (currentOffset != -1 && currentOffset < prsStream.Length)
                {
                    prsStream.Seek(currentOffset, SeekOrigin.Begin);

                    byte flagDelete = prsReader.ReadByte();
                    int p_Product = prsReader.ReadInt32();
                    int p_Detail = prsReader.ReadInt32();
                    ushort multiOccurrence = prsReader.ReadUInt16();
                    int p_Next = prsReader.ReadInt32();

                    string productName = GetComponentNameByOffset(prdStream, prdReader, p_Product, prdRecordLen);
                    string detailName = GetComponentNameByOffset(prdStream, prdReader, p_Detail, prdRecordLen);

                    if (!string.IsNullOrEmpty(productName) && !string.IsNullOrEmpty(detailName))
                    {
                        relations.Add(new RelationInfo
                        {
                            ParentName = productName,
                            ParentOffset = p_Product,
                            ChildName = detailName,
                            ChildOffset = p_Detail,
                            Count = multiOccurrence,
                            IsDeleted = flagDelete == 0xFF,
                            Offset = currentOffset
                        });
                    }

                    currentOffset = p_Next;
                }
            }

            return relations;
        }

        private string GetComponentNameByOffset(FileStream prdStream, BinaryReader prdReader, int offset, ushort recordLen)
        {
            if (offset == -1) return null;

            try
            {
                prdStream.Seek(offset, SeekOrigin.Begin);
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

        private Dictionary<string, List<RelationInfo>> BuildRelationsMap(List<RelationInfo> relations)
        {
            var map = new Dictionary<string, List<RelationInfo>>();

            foreach (var relation in relations)
            {
                // Показываем удалённые связи только если showDeleted = true
                if (!relation.IsDeleted || showDeleted)
                {
                    if (!map.ContainsKey(relation.ParentName))
                        map[relation.ParentName] = new List<RelationInfo>();

                    map[relation.ParentName].Add(relation);
                }
            }

            foreach (var key in map.Keys.ToList())
            {
                map[key] = map[key].OrderBy(r => r.ChildName).ToList();
            }

            return map;
        }

        private List<ComponentInfo> FindRootComponents(List<ComponentInfo> components,
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

            var roots = components.Where(c => !allChildren.Contains(c.Name) && !c.IsDetail && (!c.IsDeleted || showDeleted)).ToList();

            if (roots.Count == 0)
            {
                roots = components.Where(c => relationsMap.ContainsKey(c.Name) && (!c.IsDeleted || showDeleted)).ToList();
            }

            return roots;
        }

        private TreeViewItem CreateTreeItem(string name, bool isDeletedComponent, bool isDeletedRelation)
        {
            var item = new TreeViewItem
            {
                Header = name,
                Tag = new Tuple<string, bool, bool>(name, isDeletedComponent, isDeletedRelation)
            };

            if (isDeletedComponent || isDeletedRelation)
            {
                item.Foreground = System.Windows.Media.Brushes.Gray;
                item.FontStyle = FontStyles.Italic;
            }

            item.ContextMenu = (ContextMenu)this.Resources["TreeContextMenu"];

            return item;
        }

        private void BuildTree(TreeViewItem parentItem, string parentName,
            Dictionary<string, List<RelationInfo>> relationsMap,
            List<ComponentInfo> components,
            HashSet<string> visited)
        {
            if (visited.Contains(parentName))
                return;

            visited.Add(parentName);

            if (!relationsMap.ContainsKey(parentName))
                return;

            foreach (var relation in relationsMap[parentName])
            {
                string displayName = relation.Count > 1 ?
                    $"{relation.ChildName} (x{relation.Count})" :
                    relation.ChildName;

                var childComponent = components.FirstOrDefault(c => c.Name == relation.ChildName);
                bool isChildDeleted = childComponent?.IsDeleted ?? false;

                TreeViewItem childItem = CreateTreeItem(displayName, isChildDeleted, relation.IsDeleted);
                parentItem.Items.Add(childItem);

                if (childComponent != null && !childComponent.IsDetail && (!childComponent.IsDeleted || showDeleted))
                {
                    BuildTree(childItem, relation.ChildName, relationsMap, components, new HashSet<string>(visited));
                }
            }
        }

        private class ComponentInfo
        {
            public string Name { get; set; }
            public int Offset { get; set; }
            public bool IsDeleted { get; set; }
            public bool IsDetail { get; set; }
            public int P_FirstComp { get; set; }
        }

        private class RelationInfo
        {
            public string ParentName { get; set; }
            public int ParentOffset { get; set; }
            public string ChildName { get; set; }
            public int ChildOffset { get; set; }
            public ushort Count { get; set; }
            public bool IsDeleted { get; set; }
            public int Offset { get; set; }
        }
    }
}