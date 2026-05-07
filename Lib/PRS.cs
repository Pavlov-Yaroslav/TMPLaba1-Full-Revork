using System.Text;

namespace TMPLAB1
{
    public class PRS : IFile
    {
        // Смещение до p_Next внутри записи:
        // flag(1) + p_Product(4) + p_Detail(4) + multiOcc(2) = 11
        const int PRS_NEXT_OFFSET = 11;

        // Размер сигнатуры PRD (используется при чтении связанного файла)
        const int PRD_HEADER_SIGNATURE_SIZE = 2;

        public bool IsOpen { get; set; }

        public string CurrentFileName { get; set; }

        public HeaderPRS Header { get; set; } = new HeaderPRS();

        public IFileHeader FileHeader
        {
            get => Header;
            set => Header = (HeaderPRS)value;
        }

        public RecordPRS Record { get; set; } = new RecordPRS();

        IRecord IFile.Record
        {
            get => Record;
            set => Record = (RecordPRS)value;
        }

        public PRS(string fileName)
        {
            CurrentFileName = fileName;

            // Пустой файл: нет записей
            Header.p_FirstRecord = -1;
            Header.p_FreeSpace = 0;
        }

        /// <summary>
        /// Создает пустой PRS-файл (только заголовок)
        /// </summary>
        public void Create()
        {
            using (BinaryWriter bw = new BinaryWriter(File.Create(CurrentFileName)))
            {
                bw.Write(Header.p_FirstRecord);
                bw.Write(Header.p_FreeSpace);
                Console.WriteLine($"Файл {CurrentFileName} создан.");
            }
        }

        /// <summary>
        /// Просто помечает файл как открытый
        /// </summary>
        public void Open()
        {
            if (!File.Exists(CurrentFileName))
            { 
                throw new Exception($"Файла {CurrentFileName} не существует");
            } 

            try
            {
                IsOpen = true;
                Console.WriteLine($"Файл {CurrentFileName} открыт");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при открытии файла: {ex.Message}");
            }
        }

        /// <summary>
        /// Чтение записи PRD (используется для получения имен компонентов)
        /// </summary>
        private (RecordPRD, string) ReadRecord(BinaryReader br, ushort RecordLen)
        {
            RecordPRD read = new RecordPRD(
                            br.ReadByte(),
                            br.ReadInt32(),
                            br.ReadInt32(),
                            br.ReadBytes(RecordLen)
                        );

            string recordName = Encoding.UTF8.GetString(read.Name).TrimEnd('\0');
            return (read, recordName);
        }

        /// <summary>
        /// Поиск компонента в PRD по имени → возвращает offset
        /// </summary>
        private int FindComponent(FileStream stream, BinaryReader reader, int firstRecord, string name, ushort RecordLen)
        {
            int offset = firstRecord;

            while ((offset != -1) && (offset < stream.Length))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                (RecordPRD read, string nameStr) = ReadRecord(reader, RecordLen);

                if (nameStr == name) return offset;

                offset = read.p_Next;
            }

            return -1;
        }

        /// <summary>
        /// Проверяет, создаст ли добавление связи (parent -> child) циклическую зависимость.
        /// </summary>
        private bool WouldCreateCycle(string prdFileName, int parentOffset, int childOffset)
        {
            // Если родитель и потомок — одна и та же запись, цикл
            if (parentOffset == childOffset)
                return true;

            // Проверяем, не является ли parent уже потомком child (транзитивно)
            HashSet<int> visited = new HashSet<int>();
            Queue<int> toProcess = new Queue<int>();
            toProcess.Enqueue(childOffset);

            while (toProcess.Count > 0)
            {
                int current = toProcess.Dequeue();

                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                // Если нашли parent в потомках child — цикл
                if (current == parentOffset)
                    return true;

                // Ищем все связи, где current является родителем
                using (FileStream fsPrs = new FileStream(CurrentFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader readerPrs = new BinaryReader(fsPrs))
                {
                    fsPrs.Seek(0, SeekOrigin.Begin);
                    int firstRecord = readerPrs.ReadInt32();
                    int offset = firstRecord;

                    while (offset != -1)
                    {
                        fsPrs.Seek(offset, SeekOrigin.Begin);
                        byte flag = readerPrs.ReadByte();
                        int product = readerPrs.ReadInt32();
                        int detail = readerPrs.ReadInt32();
                        ushort multi = readerPrs.ReadUInt16();
                        int next = readerPrs.ReadInt32();

                        if (flag != 0xFF && product == current)
                        {
                            toProcess.Enqueue(detail);
                        }

                        offset = next;
                    }
                }
            }

            return false;
        }

        public void Print(string argument)
        {
            string prdFileName = Path.ChangeExtension(CurrentFileName, ".prd");
            PRD filePRD = new PRD(prdFileName);
            filePRD.Open();

            bool foundRelation = false;

            using (FileStream prsStream = new FileStream(CurrentFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader prsReader = new BinaryReader(prsStream))
            using (FileStream prdStream = new FileStream(prdFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader prdReader = new BinaryReader(prdStream))
            {
                // Читаем заголовок PRS
                prsStream.Seek(0, SeekOrigin.Begin);
                Header.p_FirstRecord = prsReader.ReadInt32();
                Header.p_FreeSpace = prsReader.ReadInt32();

                if (Header.p_FirstRecord == -1)
                {
                    Console.WriteLine("Файл пуст.");
                    return;
                }

                // Читаем заголовок PRD
                prdStream.Seek(PRD_HEADER_SIGNATURE_SIZE, SeekOrigin.Begin);
                filePRD.Header.RecordLen = prdReader.ReadUInt16();
                filePRD.Header.p_FirstRecord = prdReader.ReadInt32();

                // Находим offset компонента
                int componentOffset = FindComponent(prdStream, prdReader, filePRD.Header.p_FirstRecord, argument, filePRD.Header.RecordLen);

                if (componentOffset == -1 && argument != "*")
                {
                    throw new Exception($"Компонент '{argument}' не найден в PRD!");
                }

                // Обход связей с защитой от бесконечного цикла
                int currentOffset = Header.p_FirstRecord;
                HashSet<int> visitedOffsets = new HashSet<int>(); // Защита от зацикливания
                int maxIterations = 10000; // Защита от слишком долгого выполнения
                int iteration = 0;

                while (currentOffset != -1 && iteration < maxIterations)
                {
                    iteration++;

                    if (visitedOffsets.Contains(currentOffset))
                    {
                        Console.WriteLine("Обнаружен цикл в ссылках!");
                        break;
                    }
                    visitedOffsets.Add(currentOffset);

                    if (currentOffset < 0 || currentOffset >= prsStream.Length)
                    {
                        Console.WriteLine($"Некорректный offset: {currentOffset}");
                        break;
                    }

                    prsStream.Seek(currentOffset, SeekOrigin.Begin);

                    byte flagDelete = prsReader.ReadByte();
                    int p_Product = prsReader.ReadInt32();
                    int p_Detail = prsReader.ReadInt32();
                    ushort multiOccurrence = prsReader.ReadUInt16();
                    int p_Next = prsReader.ReadInt32();

                    if (flagDelete != 0xFF) // Живая запись
                    {
                        // Проверяем, что offset'ы в пределах файла
                        if (p_Product >= 0 && p_Product < prdStream.Length &&
                            p_Detail >= 0 && p_Detail < prdStream.Length)
                        {
                            try
                            {
                                // Получаем имя родителя
                                prdStream.Seek(p_Product, SeekOrigin.Begin);
                                byte flagProd = prdReader.ReadByte();
                                int pFirstCompProd = prdReader.ReadInt32();
                                int pNextProd = prdReader.ReadInt32();
                                byte[] nameBytesProd = prdReader.ReadBytes(filePRD.Header.RecordLen);
                                string productName = Encoding.UTF8.GetString(nameBytesProd).TrimEnd('\0');

                                // Если нужно вывести только для конкретного компонента
                                if (argument == "*" || argument == productName)
                                {
                                    // Получаем имя потомка
                                    prdStream.Seek(p_Detail, SeekOrigin.Begin);
                                    byte flagDetail = prdReader.ReadByte();
                                    int pFirstCompDetail = prdReader.ReadInt32();
                                    int pNextDetail = prdReader.ReadInt32();
                                    byte[] nameBytesDetail = prdReader.ReadBytes(filePRD.Header.RecordLen);
                                    string detailName = Encoding.UTF8.GetString(nameBytesDetail).TrimEnd('\0');

                                    if (multiOccurrence > 1)
                                    {
                                        Console.WriteLine($"{productName} -> {detailName} (x{multiOccurrence})");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"{productName} -> {detailName}");
                                    }
                                    foundRelation = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка чтения записи: {ex.Message}");
                            }
                        }
                    }

                    currentOffset = p_Next;
                }

                if (iteration >= maxIterations)
                {
                    Console.WriteLine("Превышен лимит итераций при обходе связей.");
                }

                if (!foundRelation && argument != "*")
                {
                    throw new Exception($"Компонент '{argument}' не найден, либо является деталью, либо не имеет связей!");
                }
                else if (!foundRelation && argument == "*")
                {
                    Console.WriteLine("Связей не найдено.");
                }
            }
        }

        /// <summary>
        /// Удаление связи (или уменьшение кратности)
        /// </summary>и
        public string Delete(string argument)
        {
            if (!IsOpen) throw new Exception("Файл не открыт");

            string message;

            string[] parts = argument
             .Replace("(", "")
             .Replace(")", "")
             .Replace(",", "")
             .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string product = parts[0];
            string detail = parts[1];

            string prdFileName = Path.ChangeExtension(CurrentFileName, ".prd");
            PRD filePRD = new PRD(prdFileName);

            using (FileStream prsStream = new FileStream(CurrentFileName, FileMode.Open, FileAccess.ReadWrite))
            using (BinaryReader prsReader = new BinaryReader(prsStream))
            using (BinaryWriter prsWriter = new BinaryWriter(prsStream))
            using (FileStream prdStream = new FileStream(prdFileName, FileMode.Open, FileAccess.ReadWrite))
            using (BinaryReader prdReader = new BinaryReader(prdStream))
            using (BinaryWriter prdWriter = new BinaryWriter(prdStream))
            {

                prsStream.Seek(0, SeekOrigin.Begin);

                Header.p_FirstRecord = prsReader.ReadInt32();

                int currentOffset = Header.p_FirstRecord;

                if (currentOffset == -1)
                {
                    throw new ArgumentNullException("Файл пуст.");
                }

                prdStream.Seek(PRD_HEADER_SIGNATURE_SIZE, SeekOrigin.Begin);
                filePRD.Header.RecordLen = prdReader.ReadUInt16();
                filePRD.Header.p_FirstRecord = prdReader.ReadInt32();

                int productOffset = FindComponent(prdStream, prdReader, filePRD.Header.p_FirstRecord, product, filePRD.Header.RecordLen);
                int detailOffset = FindComponent(prdStream, prdReader, filePRD.Header.p_FirstRecord, detail, filePRD.Header.RecordLen);

                if (productOffset == -1)
                { 
                    throw new Exception("Указнного узла/изделия не существует!");
                }
                if (detailOffset == -1)
                { 
                    throw new Exception("Указнной детали не существует!");
                } 

                while (currentOffset != -1)
                {
                    prsStream.Seek(currentOffset, SeekOrigin.Begin);

                    Record.FlagDelete = prsReader.ReadByte();
                    Record.p_Product = prsReader.ReadInt32();
                    Record.p_Detail = prsReader.ReadInt32();
                    Record.MultiOccurrence = prsReader.ReadUInt16();
                    Record.p_Next = prsReader.ReadInt32();

                    if (!Record.IsDeleted && (Record.p_Product == productOffset) && (Record.p_Detail == detailOffset))
                    {
                        prsStream.Seek(currentOffset, SeekOrigin.Begin);
                        if (Record.MultiOccurrence > 1)
                        {
                            Record.FlagDelete = prsReader.ReadByte();
                            Record.p_Product = prsReader.ReadInt32();
                            Record.p_Detail = prsReader.ReadInt32();
                            prsWriter.Write(--Record.MultiOccurrence);
                            return $"У связи {product} -> {detail} уменьшена кратность";
                        }
                        else
                        {
                            prsWriter.Write((byte)0xFF);
                            return $"Связь {product} -> {detail} помечена на удаление";
                        }
                    }
                    currentOffset = Record.p_Next;
                }

                throw new Exception("Связи не существует!");
            }
        }

        /// <summary>
        /// Восстановление всех записей, помеченных на удаление
        /// </summary>
        private void RestoreAll()
        {
            using (FileStream prsStream = new FileStream(CurrentFileName, FileMode.Open, FileAccess.ReadWrite))
            using (BinaryReader prsReader = new BinaryReader(prsStream))
            using (BinaryWriter prsWriter = new BinaryWriter(prsStream))
            {
                prsStream.Seek(0, SeekOrigin.Begin);
                Header.p_FirstRecord = prsReader.ReadInt32();
                int freeSpace = prsReader.ReadInt32();

                int currentOffset = Header.p_FirstRecord;

                if (currentOffset == -1)
                {
                    Console.WriteLine("Файл пуст.");
                    return;
                }

                int count = 0;
                List<int> offsetsToRestore = new List<int>();

                // Первый проход: собираем ВСЕ удалённые записи
                while (currentOffset != -1)
                {
                    prsStream.Seek(currentOffset, SeekOrigin.Begin);

                    byte flagDelete = prsReader.ReadByte();
                    int p_Product = prsReader.ReadInt32();
                    int p_Detail = prsReader.ReadInt32();
                    ushort multiOccurrence = prsReader.ReadUInt16();
                    int p_Next = prsReader.ReadInt32();

                    if (flagDelete == 0xFF) // Удалена
                    {
                        offsetsToRestore.Add(currentOffset);
                        Console.WriteLine($"Найдена удалённая связь: product={p_Product}, detail={p_Detail}");
                    }

                    currentOffset = p_Next;
                }

                // Второй проход: восстанавливаем
                foreach (int offset in offsetsToRestore)
                {
                    prsStream.Seek(offset, SeekOrigin.Begin);
                    prsWriter.Write((byte)0x00);
                    count++;
                }

                Console.WriteLine($"Восстановлено связей: {count}");
            }

            // Сортировка после восстановления, если есть записи
            if (Header.p_FirstRecord != -1)
            {
                SortLinksAlphabetically();
            }
        }

        /// <summary>
        /// Удаляет окончательно связи
        /// </summary>
        public void Truncate()
        {
            if (!IsOpen)
            { 
                throw new Exception("Файл не открыт");
            }

            string tempFile = Path.GetTempFileName();
            int newFirstRecord = -1;
            int removedCount = 0;

            // Маппинг старых offset -> новые для обновления ссылок
            Dictionary<int, int> offsetMap = new Dictionary<int, int>();

            // Множество компонентов, которые имеют живые связи (как родители)
            HashSet<int> parentsWithLiveLinks = new HashSet<int>();

            string prdFileName = Path.ChangeExtension(CurrentFileName, ".prd");

            try
            {
                // ПЕРВЫЙ ПРОХОД: собираем живые записи и определяем родителей
                using (FileStream source = new FileStream(CurrentFileName, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader br = new BinaryReader(source))
                    {
                        source.Seek(0, SeekOrigin.Begin);
                        int firstRecord = br.ReadInt32();
                        int freeSpace = br.ReadInt32();

                        int currentOffset = firstRecord;

                        while ((currentOffset != -1) && (currentOffset < source.Length))
                        {
                            source.Seek(currentOffset, SeekOrigin.Begin);

                            byte flagDelete = br.ReadByte();
                            int p_Product = br.ReadInt32();
                            int p_Detail = br.ReadInt32();
                            ushort multiOccurrence = br.ReadUInt16();
                            int p_Next = br.ReadInt32();

                            if (flagDelete == 0) // Живая запись
                            {
                                parentsWithLiveLinks.Add(p_Product);
                            }

                            currentOffset = p_Next;
                        }
                    }
                }

                // ВТОРОЙ ПРОХОД: перезаписываем файл без удалённых записей
                using (FileStream source = new FileStream(CurrentFileName, FileMode.Open, FileAccess.Read))
                using (FileStream dest = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                using (BinaryReader br = new BinaryReader(source))
                using (BinaryWriter bw = new BinaryWriter(dest))
                {
                    source.Seek(0, SeekOrigin.Begin);
                    int firstRecord = br.ReadInt32();
                    int freeSpace = br.ReadInt32();

                    bw.Write(-1); // временный p_FirstRecord
                    bw.Write(0);

                    int currentOffset = firstRecord;
                    List<(int oldOffset, long newOffset, int p_Product, int p_Detail, ushort multiOcc)> liveRecords = new();

                    while ((currentOffset != -1) && (currentOffset < source.Length))
                    {
                        source.Seek(currentOffset, SeekOrigin.Begin);

                        byte flagDelete = br.ReadByte();
                        int p_Product = br.ReadInt32();
                        int p_Detail = br.ReadInt32();
                        ushort multiOccurrence = br.ReadUInt16();
                        int p_Next = br.ReadInt32();

                        int nextOffset = p_Next;

                        if (flagDelete != 0xFF) // Живая запись
                        {
                            long recordPos = dest.Position;
                            offsetMap[currentOffset] = (int)recordPos;

                            bw.Write(flagDelete);
                            bw.Write(p_Product);
                            bw.Write(p_Detail);
                            bw.Write(multiOccurrence);
                            bw.Write(0); // временный p_Next

                            liveRecords.Add((currentOffset, recordPos, p_Product, p_Detail, multiOccurrence));

                            if (newFirstRecord == -1) newFirstRecord = (int)recordPos;
                        }
                        else
                        {
                            removedCount++;
                        }

                        currentOffset = nextOffset;
                    }

                    // Обновляем p_Next указатели
                    for (int i = 0; i < liveRecords.Count; i++)
                    {
                        long recordPos = liveRecords[i].newOffset;
                        int nextPointer;
                        if (i < liveRecords.Count - 1)
                        {
                            nextPointer = (int)liveRecords[i + 1].newOffset;
                        }
                        else
                        {
                            nextPointer = -1;
                        }

                        dest.Seek(recordPos + PRS_NEXT_OFFSET, SeekOrigin.Begin);
                        bw.Write(nextPointer);
                    }

                    // Обновляем заголовок
                    dest.Seek(0, SeekOrigin.Begin);
                    bw.Write(newFirstRecord);
                    bw.Write(0);
                }

                Header.p_FirstRecord = newFirstRecord;
                Header.p_FreeSpace = 0;

                File.Delete(CurrentFileName);
                File.Move(tempFile, CurrentFileName);

                Console.WriteLine($"Файл сжат. Удалено записей: {removedCount}");

                // ТРЕТИЙ ПРОХОД: обновляем p_FirstComp в PRD
                UpdatePrdComponentTypes(prdFileName, parentsWithLiveLinks, offsetMap);
            }
            catch (Exception e)
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                Console.Write($"Ошибка: {e.Message}");
            }
        }

        /// <summary>
        /// Обновляет типы компонентов в PRD после сжатия PRS
        /// </summary>
        private void UpdatePrdComponentTypes(string prdFileName, HashSet<int> parentsWithLiveLinks, Dictionary<int, int> offsetMap)
        {
            if (!File.Exists(prdFileName))
                return;

            PRD filePRD = new PRD(prdFileName);
            filePRD.Open();

            using (FileStream prdStream = new FileStream(prdFileName, FileMode.Open, FileAccess.ReadWrite))
            using (BinaryWriter prdWriter = new BinaryWriter(prdStream))
            using (BinaryReader prdReader = new BinaryReader(prdStream))
            {
                // Преобразуем старые offset родителей в новые (если они изменились)
                HashSet<int> updatedParents = new HashSet<int>();

                foreach (int oldOffset in parentsWithLiveLinks)
                {
                    if (offsetMap.ContainsKey(oldOffset))
                    {
                        updatedParents.Add(offsetMap[oldOffset]);
                    }
                    else
                    {
                        updatedParents.Add(oldOffset);
                    }
                }

                int offset = filePRD.Header.p_FirstRecord;

                while (offset != -1 && offset < prdStream.Length)
                {
                    prdStream.Seek(offset, SeekOrigin.Begin);

                    byte flagDelete = prdReader.ReadByte();
                    int p_FirstComp = prdReader.ReadInt32();
                    int p_Next = prdReader.ReadInt32();

                    // Пропускаем удалённые записи
                    if (flagDelete == 0xFF)
                    {
                        offset = p_Next;
                        continue;
                    }

                    // Если компонент не имеет живых связей и при этом p_FirstComp != -1
                    if (!updatedParents.Contains(offset) && p_FirstComp != -1)
                    {
                        // Обновляем p_FirstComp на -1 (делаем компонент деталью)
                        prdStream.Seek(offset + 1, SeekOrigin.Begin); // +1 для пропуска FlagDelete
                        prdWriter.Write(-1);
                        Console.WriteLine($"Компонент по смещению {offset} стал деталью (нет связей)");
                    }

                    offset = p_Next;
                }
            }
        }

        /// <summary>
        /// Сортирует ВСЕ записи (и живые, и удалённые) в алфавитном порядке по имени дочернего компонента
        /// </summary>
        private void SortLinksAlphabetically()
        {
            string prdFileName = Path.ChangeExtension(CurrentFileName, ".prd");

            // Собираем ВСЕ записи
            List<(int oldOffset, int productOffset, int detailOffset, ushort multiOcc, byte flagDelete, string productName, string detailName)> allLinks = new();

            using (FileStream prsStream = new FileStream(CurrentFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader prsReader = new BinaryReader(prsStream))
            using (FileStream prdStream = new FileStream(prdFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader prdReader = new BinaryReader(prdStream))
            {
                prsStream.Seek(0, SeekOrigin.Begin);
                int firstRecord = prsReader.ReadInt32();
                int freeSpace = prsReader.ReadInt32();

                // Читаем заголовок PRD
                prdStream.Seek(2 + 2, SeekOrigin.Begin);
                ushort prdRecordLen = prdReader.ReadUInt16();
                int prdFirstRecord = prdReader.ReadInt32();

                int currentOffset = firstRecord;

                while (currentOffset != -1 && currentOffset < prsStream.Length)
                {
                    prsStream.Seek(currentOffset, SeekOrigin.Begin);

                    byte flagDelete = prsReader.ReadByte();
                    int p_Product = prsReader.ReadInt32();
                    int p_Detail = prsReader.ReadInt32();
                    ushort multiOccurrence = prsReader.ReadUInt16();
                    int p_Next = prsReader.ReadInt32();

                    // Получаем имена для сортировки (даже для удалённых записей)
                    string productName = "???";
                    string detailName = "???";

                    try
                    {
                        if (p_Product >= 0 && p_Product < prdStream.Length)
                        {
                            prdStream.Seek(p_Product, SeekOrigin.Begin);
                            prdReader.ReadByte(); // flagDelete
                            prdReader.ReadInt32(); // p_FirstComp
                            prdReader.ReadInt32(); // p_Next
                            byte[] nameBytes = prdReader.ReadBytes(prdRecordLen);
                            productName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
                        }

                        if (p_Detail >= 0 && p_Detail < prdStream.Length)
                        {
                            prdStream.Seek(p_Detail, SeekOrigin.Begin);
                            prdReader.ReadByte(); // flagDelete
                            prdReader.ReadInt32(); // p_FirstComp
                            prdReader.ReadInt32(); // p_Next
                            byte[] nameBytes = prdReader.ReadBytes(prdRecordLen);
                            detailName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка чтения имён: {ex.Message}");
                    }

                    allLinks.Add((currentOffset, p_Product, p_Detail, multiOccurrence, flagDelete, productName, detailName));
                    currentOffset = p_Next;
                }
            }

            if (allLinks.Count == 0) return;

            // Сортируем все записи по имени родителя, затем по имени потомка
            var sortedLinks = allLinks.OrderBy(x => x.productName)
                                      .ThenBy(x => x.detailName)
                                      .ToList();

            // Перестраиваем файл
            string tempFile = Path.GetTempFileName();
            int newFirstRecord = -1;
            int prevOffset = -1;

            try
            {
                using (FileStream destFs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(destFs))
                {
                    // Заголовок (временный)
                    bw.Write(-1);
                    bw.Write(0);

                    foreach (var link in sortedLinks)
                    {
                        long recordPos = destFs.Position;
                        int newOffset = (int)recordPos;

                        if (newFirstRecord == -1) newFirstRecord = newOffset;

                        if (prevOffset != -1)
                        {
                            // Обновляем p_Next предыдущей записи
                            destFs.Seek(prevOffset + PRS_NEXT_OFFSET, SeekOrigin.Begin);
                            bw.Write(newOffset);
                            destFs.Seek(0, SeekOrigin.End);
                        }

                        // Записываем запись
                        bw.Write(link.flagDelete);
                        bw.Write(link.productOffset);
                        bw.Write(link.detailOffset);
                        bw.Write(link.multiOcc);
                        bw.Write(0); // временный p_Next

                        prevOffset = newOffset;
                    }

                    // Последняя запись указывает на -1
                    if (prevOffset != -1)
                    {
                        destFs.Seek(prevOffset + PRS_NEXT_OFFSET, SeekOrigin.Begin);
                        bw.Write(-1);
                    }

                    // Обновляем заголовок
                    destFs.Seek(0, SeekOrigin.Begin);
                    bw.Write(newFirstRecord);
                    bw.Write(0);
                }

                File.Delete(CurrentFileName);
                File.Move(tempFile, CurrentFileName);

                Header.p_FirstRecord = newFirstRecord;
                Header.p_FreeSpace = 0;

                Console.WriteLine($"Связи отсортированы в алфавитном порядке. Всего записей: {sortedLinks.Count}");
            }
            catch
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                throw;
            }
        }

        private Dictionary<int, ComponentType> BuildComponentTypeMap()
        {
            var productSet = new HashSet<int>();
            var detailSet = new HashSet<int>();

            using (FileStream fs = new FileStream(CurrentFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                fs.Seek(0, SeekOrigin.Begin);
                int firstRecord = br.ReadInt32();
                int freeSpace = br.ReadInt32();

                int offset = firstRecord;
                while ((offset != -1) && (offset < fs.Length))
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    byte flagDelete = br.ReadByte();
                    int p_Product = br.ReadInt32();
                    int p_Detail = br.ReadInt32();
                    br.ReadUInt16();
                    int p_Next = br.ReadInt32();

                    if (flagDelete != 0xFF)
                    {
                        productSet.Add(p_Product);
                        detailSet.Add(p_Detail);
                    }

                    offset = p_Next;
                }
            }

            var typeMap = new Dictionary<int, ComponentType>();
            var allComponents = new HashSet<int>(productSet);
            allComponents.UnionWith(detailSet);

            foreach (int compOffset in allComponents)
            {
                bool inProduct = productSet.Contains(compOffset);
                bool inDetail = detailSet.Contains(compOffset);

                if (inProduct && !inDetail)
                {
                    typeMap[compOffset] = ComponentType.PRODUCT;
                }
                else if (inProduct && inDetail)
                {
                    typeMap[compOffset] = ComponentType.ASSEMBLY;
                }
                else
                { 
                    typeMap[compOffset] = ComponentType.DETAIL;
                }
            }

            return typeMap;
        }

        /// <summary>
        /// Добавление связи (родитель, ребенок)
        /// </summary>
        public string Input(string argument)
        {
            string[] parts = argument
                .Replace("(", "")
                .Replace(")", "")
                .Replace(",", "")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new Exception("Формат: input <Компонент> <Деталь>");
            }

            string mainComponent = parts[0];
            string detailComponent = parts[1];

            string prdFileName = Path.ChangeExtension(CurrentFileName, ".prd");

            int mainRecordOffset;
            int detailRecordOffset;
            ushort prdRecordLen = 0;

            using (FileStream fsPrd = new FileStream(prdFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader readerPrd = new BinaryReader(fsPrd))
            {
                fsPrd.Seek(2, SeekOrigin.Begin);
                prdRecordLen = readerPrd.ReadUInt16();
                int firstRecord = readerPrd.ReadInt32();

                mainRecordOffset = FindComponent(fsPrd, readerPrd, firstRecord, mainComponent, prdRecordLen);
                detailRecordOffset = FindComponent(fsPrd, readerPrd, firstRecord, detailComponent, prdRecordLen);
            }

            if (mainRecordOffset == -1)
            {
                throw new Exception($"Компонент '{mainComponent}' отсутствует в PRD");
            }

            if (detailRecordOffset == -1)
            {
                throw new Exception($"Компонент '{detailComponent}' отсутствует в PRD");
            }

            var typeMap = BuildComponentTypeMap();

            using FileStream fsPrs = new(CurrentFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using BinaryReader readerPrs = new(fsPrs);
            using BinaryWriter writerPrs = new(fsPrs);

            fsPrs.Seek(0, SeekOrigin.Begin);
            Header.p_FirstRecord = readerPrs.ReadInt32();
            Header.p_FreeSpace = readerPrs.ReadInt32();

            if (mainRecordOffset != detailRecordOffset)
            {
                int currentOffset = Header.p_FirstRecord;

                while (currentOffset != -1)
                {
                    fsPrs.Seek(currentOffset, SeekOrigin.Begin);
                    byte flagDelete = readerPrs.ReadByte();
                    int p_Product = readerPrs.ReadInt32();
                    int p_Detail = readerPrs.ReadInt32();
                    ushort multiOccurrence = readerPrs.ReadUInt16();
                    int p_Next = readerPrs.ReadInt32();

                    if (p_Detail == detailRecordOffset)
                    {
                        if (p_Product == mainRecordOffset)
                        {
                            currentOffset = p_Next;
                            continue;
                        }

                        string existingParentName = "";
                        using (FileStream fsPrdTemp = new FileStream(prdFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (BinaryReader readerPrdTemp = new BinaryReader(fsPrdTemp))
                        {
                            fsPrdTemp.Seek(p_Product, SeekOrigin.Begin);
                            readerPrdTemp.ReadByte();
                            readerPrdTemp.ReadInt32();
                            readerPrdTemp.ReadInt32();
                            byte[] nameBytes = readerPrdTemp.ReadBytes(prdRecordLen);
                            existingParentName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
                        }

                        string status = flagDelete == 0xFF ? " (помечена на удаление)" : "";
                        throw new Exception($"Деталь '{detailComponent}' уже используется в составе '{existingParentName}'{status}. Деталь не может входить в состав нескольких узлов.");
                    }

                    currentOffset = p_Next;
                }
            }

            if (WouldCreateCycle(prdFileName, mainRecordOffset, detailRecordOffset))
            {
                throw new Exception($"Невозможно добавить связь: '{mainComponent} → {detailComponent}' создаст циклическую зависимость");
            }

            int searchOffset = Header.p_FirstRecord;

            while ((searchOffset != -1) && ((searchOffset + 15) <= fsPrs.Length))
            {
                fsPrs.Seek(searchOffset, SeekOrigin.Begin);

                byte flagDelete = readerPrs.ReadByte();
                int p_Product = readerPrs.ReadInt32();
                int p_Detail = readerPrs.ReadInt32();
                ushort multiOccurrence = readerPrs.ReadUInt16();
                int p_Next = readerPrs.ReadInt32();

                if (p_Product == mainRecordOffset && p_Detail == detailRecordOffset)
                {
                    if (flagDelete == 0)
                    {
                        fsPrs.Seek(searchOffset + 9, SeekOrigin.Begin);
                        writerPrs.Write(++multiOccurrence);
                        return $"Увеличена кратность: {mainComponent} -> {detailComponent}";
                    }
                    else
                    {
                        throw new Exception($"Связь '{mainComponent} → {detailComponent}' удалена. Используйте 'Restore {mainComponent} {detailComponent}' для восстановления.");
                    }
                }

                searchOffset = p_Next;
            }

            fsPrs.Seek(0, SeekOrigin.End);
            int newRecordOffset = (int)fsPrs.Position;

            Record.FlagDelete = 0;
            Record.p_Product = mainRecordOffset;
            Record.p_Detail = detailRecordOffset;
            Record.MultiOccurrence = 1;
            Record.p_Next = Header.p_FirstRecord;

            writerPrs.Write(Record.FlagDelete);
            writerPrs.Write(Record.p_Product);
            writerPrs.Write(Record.p_Detail);
            writerPrs.Write(Record.MultiOccurrence);
            writerPrs.Write(Record.p_Next);

            Header.p_FirstRecord = newRecordOffset;
            Header.p_FreeSpace += 15;

            fsPrs.Seek(0, SeekOrigin.Begin);
            writerPrs.Write(Header.p_FirstRecord);
            writerPrs.Write(Header.p_FreeSpace);

            using (FileStream fsPrdUpdate = new FileStream(prdFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (BinaryWriter writerPrdUpdate = new BinaryWriter(fsPrdUpdate))
            {
                fsPrdUpdate.Seek(mainRecordOffset + 1, SeekOrigin.Begin);
                writerPrdUpdate.Write(newRecordOffset);
            }

            return $"Добавлена связь: {mainComponent} -> {detailComponent}";
        }

        /// <summary>
        /// Восстанваливает связь (если ввести (имя родителя, имя ребенка)) или все связи (если ввести *)
        /// </summary>
        public void Restore(string name)
        {
            if (!IsOpen)
            {
                throw new Exception("Файл не открыт");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("Укажите связь для восстановления");
            }

            if (name == "*")
            {
                RestoreAll();
                return;
            }

            string[] parts = name
             .Replace("(", "")
             .Replace(")", "")
             .Replace(",", "")
             .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string product = parts[0];
            string detail = parts[1];

            string prdFileName = Path.ChangeExtension(CurrentFileName, ".prd");
            PRD filePRD = new PRD(prdFileName);
            filePRD.Open();

            using (FileStream prsStream = new FileStream(CurrentFileName, FileMode.Open, FileAccess.ReadWrite))
            using (BinaryReader prsReader = new BinaryReader(prsStream))
            using (BinaryWriter prsWriter = new BinaryWriter(prsStream))
            using (FileStream prdStream = new FileStream(prdFileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader prdReader = new BinaryReader(prdStream))
            {
                prsStream.Seek(0, SeekOrigin.Begin);
                Header.p_FirstRecord = prsReader.ReadInt32();
                int freeSpace = prsReader.ReadInt32();

                prdStream.Seek(2, SeekOrigin.Begin);
                filePRD.Header.RecordLen = prdReader.ReadUInt16();
                filePRD.Header.p_FirstRecord = prdReader.ReadInt32();

                int productOffset = FindComponent(prdStream, prdReader, filePRD.Header.p_FirstRecord, product, filePRD.Header.RecordLen);
                int detailOffset = FindComponent(prdStream, prdReader, filePRD.Header.p_FirstRecord, detail, filePRD.Header.RecordLen);

                if (productOffset == -1)
                {
                    throw new Exception($"Компонент '{product}' не существует в PRD!");
                }
                if (detailOffset == -1)
                {
                    throw new Exception($"Компонент '{detail}' не существует в PRD!");
                }

                int currentOffset = Header.p_FirstRecord;
                bool found = false;

                while (currentOffset != -1)
                {
                    prsStream.Seek(currentOffset, SeekOrigin.Begin);

                    byte flagDelete = prsReader.ReadByte();
                    int p_Product = prsReader.ReadInt32();
                    int p_Detail = prsReader.ReadInt32();
                    ushort multiOccurrence = prsReader.ReadUInt16();
                    int p_Next = prsReader.ReadInt32();

                    if (p_Product == productOffset && p_Detail == detailOffset)
                    {
                        found = true;

                        if (flagDelete == 0xFF)
                        {
                            prsStream.Seek(currentOffset, SeekOrigin.Begin);
                            prsWriter.Write((byte)0x00);
                            Console.WriteLine($"Связь {product} -> {detail} восстановлена");
                        }
                        else
                        {
                            throw new Exception($"Связь {product} -> {detail} не удалена (кратность: {multiOccurrence})");
                        }
                        break;
                    }

                    currentOffset = p_Next;
                }

                if (!found)
                {
                    throw new Exception($"Связь {product} -> {detail} не существует!");
                }
            }

            SortLinksAlphabetically();
        }

    }
}