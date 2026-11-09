// =====================================================================
//  Сравнение сортировок: слиянием (O(N log N)) и вставками (O(N^2))
// =====================================================================
//
// Как запускать (из папки ~/SortBenchmark):
//   dotnet run -c Release                    — по заданию: 10^8 строк (долго, ~600 МБ на диске, много ГБ памяти!)
//   dotnet run -c Release -- 1000000         — 10^6 строк (быстро, удобно для проверки)
//   dotnet run -c Release -- 1000000 20000   — 10^6 строк, вставками сортируем первые 20000
//
// "-c Release" важно: без него программа собирается без оптимизаций и всё медленнее.

using System.Diagnostics;   // здесь живёт Stopwatch — секундомер
using System.Text;

// ---------- Параметры ----------

// Сколько строк записать в файл (по заданию 10^8).
long lineCount = 100_000_000;
// Сколько первых строк сортировать квадратичной сортировкой.
// 10^8 строк вставками не отсортировать никогда: это ~2.5 * 10^15 сравнений,
// при ~10^9 операций в секунду — около 30 дней. Поэтому берём только начало файла.
int quadraticCount = 50_000;

if (args.Length >= 1) lineCount = long.Parse(args[0]);
if (args.Length >= 2) quadraticCount = int.Parse(args[1]);

if (lineCount < 1 || quadraticCount < 1)
{
    Console.WriteLine("Количество строк должно быть не меньше 1.");
    return;
}
if (lineCount > int.MaxValue)
{
    Console.WriteLine("Слишком много строк: массив в C# не может быть длиннее ~2.1 млрд элементов.");
    return;
}
if (quadraticCount > lineCount) quadraticCount = (int)lineCount;

string fileName = "strings.txt";

Console.WriteLine($"Строк в файле: {lineCount:N0}");
Console.WriteLine($"Для сортировки вставками берём первые {quadraticCount:N0} строк");
Console.WriteLine();

// Предупреждение для больших объёмов.
if (lineCount >= 10_000_000)
{
    double fileMb = lineCount * 6.0 / 1024 / 1024;   // в среднем 5 букв + символ перевода строки
    double ramGb = lineCount * 60.0 / 1024 / 1024 / 1024; // грубо: строка-объект в .NET ~40-50 байт + ссылки в массивах
    Console.WriteLine("ВНИМАНИЕ! Это большой объём:");
    Console.WriteLine($"  файл на диске займёт примерно {fileMb:N0} МБ,");
    Console.WriteLine($"  для сортировки всех строк в памяти нужно примерно {ramGb:N1} ГБ оперативной памяти,");
    Console.WriteLine("  генерация и сортировка займут несколько минут.");
    Console.Write("Продолжить? (y/n): ");
    string? answer = Console.ReadLine();
    if (answer == null || answer.Trim().ToLower() != "y")
    {
        Console.WriteLine("Отменено. Запустите с меньшим числом, например: dotnet run -c Release -- 1000000");
        return;
    }
    Console.WriteLine();
}

Stopwatch sw = new Stopwatch();

// ===================== 3. Создаём файл со строками =====================
Console.WriteLine($"[1] Создаём файл {fileName} ...");
sw.Restart();
GenerateFile(fileName, (int)lineCount);
sw.Stop();
Console.WriteLine($"    Готово за {sw.Elapsed.TotalSeconds:F2} с, размер файла {new FileInfo(fileName).Length / 1024.0 / 1024.0:F1} МБ");

// ===================== Читаем файл в массив =====================
Console.WriteLine($"[2] Читаем строки из файла в память ...");
sw.Restart();
string[] allLines = ReadFile(fileName, (int)lineCount);
sw.Stop();
Console.WriteLine($"    Прочитано {allLines.Length:N0} строк за {sw.Elapsed.TotalSeconds:F2} с");

// Отдельная копия начала файла для квадратичной сортировки
// (сортировки меняют массив, поэтому каждой сортировке — своя копия).
string[] forInsertion = new string[quadraticCount];
Array.Copy(allLines, forInsertion, quadraticCount);
string[] forMergeSmall = (string[])forInsertion.Clone();

// ===================== 4. Сортируем и замеряем время =====================
Console.WriteLine();
Console.WriteLine($"[3] Сортируем первые {quadraticCount:N0} строк обеими сортировками:");

sw.Restart();
Sorts.InsertionSort(forInsertion);
sw.Stop();
Console.WriteLine($"    Вставками:  {sw.Elapsed.TotalSeconds,10:F4} с");

sw.Restart();
Sorts.MergeSort(forMergeSmall);
sw.Stop();
Console.WriteLine($"    Слиянием:   {sw.Elapsed.TotalSeconds,10:F4} с");

// Проверка: обе отсортировали правильно и результат совпадает со встроенной сортировкой.
string[] reference = new string[quadraticCount];
Array.Copy(allLines, reference, quadraticCount);
Array.Sort(reference, StringComparer.Ordinal);
bool okSmall = Sorts.IsSorted(forInsertion) && Sorts.SameArrays(forInsertion, reference)
            && Sorts.SameArrays(forMergeSmall, reference);
Console.WriteLine($"    Проверка правильности: {(okSmall ? "OK" : "ОШИБКА!")}");

Console.WriteLine();
Console.WriteLine($"[4] Сортируем слиянием ВСЕ {allLines.Length:N0} строк ...");
sw.Restart();
Sorts.MergeSort(allLines);
sw.Stop();
Console.WriteLine($"    Слиянием:   {sw.Elapsed.TotalSeconds,10:F4} с");
Console.WriteLine($"    Проверка, что массив отсортирован: {(Sorts.IsSorted(allLines) ? "OK" : "ОШИБКА!")}");

// Оценим, сколько бы сортировались вставками все строки:
// время растёт как N^2, значит t(N) ≈ t(n) * (N/n)^2.
double insertionSeconds = MeasureInsertionSeconds(quadraticCount);
double ratio = (double)allLines.Length / quadraticCount;
double guessSeconds = insertionSeconds * ratio * ratio;
Console.WriteLine($"    А вставками все строки сортировались бы примерно {FormatTime(guessSeconds)} (оценка по формуле N^2)");

allLines = null!;   // отпускаем большой массив, чтобы сборщик мусора освободил память
GC.Collect();

// ===================== 5*. Сколько простых операций в секунду =====================
Console.WriteLine();
Console.WriteLine("[5*] Сколько простых операций в секунду делает компьютер?");
MeasureOperationsPerSecond();

// ===================== 6*. С какого N сортировки ведут себя "как в теории" =====================
Console.WriteLine();
Console.WriteLine("[6*] Сравниваем сортировки на разных N (время одной сортировки, в микросекундах):");
FindCrossover();

Console.WriteLine();
Console.WriteLine("Готово!");


// =====================================================================
//                         Вспомогательные функции
// =====================================================================

// Создаёт файл из count строк. Каждая строка — от 0 до 10 случайных строчных латинских букв.
static void GenerateFile(string fileName, int count)
{
    Random random = new Random();
    char[] letters = new char[10];   // сюда собираем буквы одной строки

    // StreamWriter с большим буфером пишет на диск большими кусками — так быстрее.
    using StreamWriter writer = new StreamWriter(fileName, false, Encoding.ASCII, 1 << 20);
    for (int i = 0; i < count; i++)
    {
        int length = random.Next(0, 11);           // длина от 0 до 10 включительно
        for (int k = 0; k < length; k++)
            letters[k] = (char)('a' + random.Next(26));   // случайная буква a..z
        writer.Write(letters, 0, length);
        writer.Write('\n');                        // конец строки
    }
}

// Читает count строк из файла в массив.
static string[] ReadFile(string fileName, int count)
{
    string[] result = new string[count];
    using StreamReader reader = new StreamReader(fileName, Encoding.ASCII, false, 1 << 20);
    for (int i = 0; i < count; i++)
        result[i] = reader.ReadLine() ?? "";       // пустая строка в файле → ""
    return result;
}

// Делает массив из n случайных строк (как в файле, но сразу в памяти).
static string[] RandomStrings(int n, Random random)
{
    string[] a = new string[n];
    char[] letters = new char[10];
    for (int i = 0; i < n; i++)
    {
        int length = random.Next(0, 11);
        for (int k = 0; k < length; k++)
            letters[k] = (char)('a' + random.Next(26));
        a[i] = new string(letters, 0, length);
    }
    return a;
}

// Ещё раз сортирует вставками n случайных строк и возвращает время в секундах
// (нужно для оценки времени на всех строках).
static double MeasureInsertionSeconds(int n)
{
    string[] a = RandomStrings(n, new Random(1));
    Stopwatch sw = Stopwatch.StartNew();
    Sorts.InsertionSort(a);
    return sw.Elapsed.TotalSeconds;
}

// Красиво печатает время: секунды, минуты, часы, дни или годы.
static string FormatTime(double seconds)
{
    if (seconds < 60) return $"{seconds:F1} с";
    if (seconds < 3600) return $"{seconds / 60:F1} мин";
    if (seconds < 86400) return $"{seconds / 3600:F1} ч";
    if (seconds < 365 * 86400) return $"{seconds / 86400:F1} дней";
    return $"{seconds / (365 * 86400):F1} лет";
}

// 5*. Крутим простой цикл много раз и делим количество операций на время.
static void MeasureOperationsPerSecond()
{
    long iterations = 1_000_000_000;   // миллиард повторений
    long sum = 0;

    Stopwatch sw = Stopwatch.StartNew();
    for (long i = 0; i < iterations; i++)
    {
        // Одно сложение и одно "исключающее ИЛИ". Результат зависит от прошлого шага,
        // поэтому компилятор не может выкинуть цикл или посчитать его заранее.
        sum = (sum + i) ^ 5;
    }
    sw.Stop();

    double seconds = sw.Elapsed.TotalSeconds;
    double perSecond = iterations / seconds;
    // На каждом шаге цикла: сложение, XOR, увеличение i, сравнение i < iterations — примерно 4 операции.
    Console.WriteLine($"    {iterations:N0} шагов цикла за {seconds:F3} с (контрольная сумма {sum})");
    Console.WriteLine($"    ≈ {perSecond:E2} шагов цикла в секунду");
    Console.WriteLine($"    ≈ {perSecond * 4:E2} простых операций в секунду (в каждом шаге ~4 операции)");
    Console.WriteLine("    Вывод: компьютер делает порядка 10^9 простых операций в секунду.");
}

// 6*. Для каждого N много раз сортируем случайные массивы обеими сортировками
// и считаем среднее время. Маленькие сортировки длятся микросекунды, поэтому
// одного замера мало — повторяем, пока не наберётся заметное общее время.
static void FindCrossover()
{
    int[] sizes = { 2, 5, 10, 20, 30, 50, 75, 100, 150, 200, 500, 1000, 2000, 5000, 10000 };
    Random random = new Random(42);

    // "Разогрев": первый запуск метода в .NET медленнее (метод компилируется),
    // поэтому сначала прогоняем обе сортировки вхолостую.
    for (int w = 0; w < 200; w++)
    {
        Sorts.InsertionSort(RandomStrings(100, random));
        Sorts.MergeSort(RandomStrings(100, random));
    }

    double[] insertionTimes = new double[sizes.Length];
    double[] mergeTimes = new double[sizes.Length];

    Console.WriteLine($"    {"N",7} {"вставками",12} {"слиянием",12}   кто быстрее");
    for (int s = 0; s < sizes.Length; s++)
    {
        int n = sizes[s];
        double insertionTotal = 0, mergeTotal = 0;
        int repeats = 0;
        Stopwatch total = Stopwatch.StartNew();
        Stopwatch sw = new Stopwatch();

        // Повторяем минимум 5 раз и пока не пройдёт хотя бы 0.3 секунды.
        while (repeats < 5 || total.Elapsed.TotalSeconds < 0.3)
        {
            string[] data = RandomStrings(n, random);    // одинаковые данные для обеих сортировок
            string[] copy1 = (string[])data.Clone();
            string[] copy2 = (string[])data.Clone();

            sw.Restart();
            Sorts.InsertionSort(copy1);
            sw.Stop();
            insertionTotal += sw.Elapsed.TotalMicroseconds;

            sw.Restart();
            Sorts.MergeSort(copy2);
            sw.Stop();
            mergeTotal += sw.Elapsed.TotalMicroseconds;

            repeats++;
        }

        insertionTimes[s] = insertionTotal / repeats;
        mergeTimes[s] = mergeTotal / repeats;
        string winner = mergeTimes[s] < insertionTimes[s] ? "слиянием" : "вставками";
        Console.WriteLine($"    {n,7} {insertionTimes[s],12:F3} {mergeTimes[s],12:F3}   {winner}");
    }

    // Ищем самое маленькое N, начиная с которого слияние быстрее при ВСЕХ больших N.
    int crossoverIndex = -1;
    for (int s = sizes.Length - 1; s >= 0; s--)
    {
        if (mergeTimes[s] < insertionTimes[s])
            crossoverIndex = s;
        else
            break;
    }

    Console.WriteLine();
    if (crossoverIndex == -1)
        Console.WriteLine("    Слияние ни разу стабильно не обогнало вставки (странно — попробуйте запустить ещё раз).");
    else
        Console.WriteLine($"    Начиная примерно с N = {sizes[crossoverIndex]}, сортировка слиянием стабильно быстрее вставок.");
    Console.WriteLine("    При маленьких N вставки выигрывают: у них проще код и нет рекурсии и лишнего копирования.");
    Console.WriteLine("    При больших N побеждает N*log(N): при росте N в 10 раз вставки замедляются ~в 100 раз,");
    Console.WriteLine("    а слияние — всего ~в 12-13 раз.");
}
