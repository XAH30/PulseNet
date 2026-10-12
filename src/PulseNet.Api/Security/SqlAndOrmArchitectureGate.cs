using System.Text.RegularExpressions;

namespace PulseNet.Api.Security;

public sealed record GateViolation(
    string RuleId,
    string FilePath,
    int LineNumber,
    string Description);

public sealed record GateScanResult(
    bool IsPassed,
    int ScannedFilesCount,
    IReadOnlyList<GateViolation> Violations);

public sealed record GateRuleOptions(
    bool CheckOrmUsage = true,
    bool CheckSqlInterpolation = true,
    bool CheckSqlConcatenation = true,
    bool CheckRawCommandCreation = true);

/// <summary>
/// Fail-closed статический гейт безопасности и архитектуры:
/// 1) Запрещает любые ORM-библиотеки (EF Core, Dapper, NHibernate, LinqToDB и др.).
/// 2) Запрещает интерполяцию ($"..."), string.Format, string.Concat и конкатенацию (+) в SQL-выражениях.
/// 3) При отсутствии файлов или ошибке чтения завершается fail-closed (IsPassed = false).
/// </summary>
public static partial class SqlAndOrmArchitectureGate
{
    private static readonly string[] ForbiddenOrmTokens =
    [
        "Microsoft.EntityFrameworkCore",
        "EntityFrameworkCore",
        "DbContext",
        "DbSet<",
        "Dapper",
        "SqlMapper",
        "QueryAsync<",
        "QueryFirstOrDefaultAsync<",
        "QuerySingleOrDefaultAsync<",
        "NHibernate",
        "ISessionFactory",
        "LinqToDB",
        "ServiceStack.OrmLite",
        "SqlKata",
        "PetaPoco",
        "RepoDb"
    ];

    // Обнаружение интерполированных строк ($"..." / $@"..." / @$"" / $"""..."""), содержащих SQL-ключевые слова и подстановку { ... }
    [GeneratedRegex(
        @"(\$\@|\@\$|\$)\s*(?:""""""[\s\S]*?\b(?:SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|WHERE|FROM|INTO)\b[\s\S]*?\{[^}]+\}[\s\S]*?""""""|""[^""\r\n]*?\b(?:SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|WHERE|FROM|INTO)\b[^""\r\n]*?\{[^}]+\}[^""\r\n]*?"")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InterpolatedSqlRegex();

    // Обнаружение string.Format / string.Concat / String.Join с SQL-ключевыми словами
    [GeneratedRegex(
        @"\bstring\s*\.\s*(?:Format|Concat|Join)\s*\([\s\S]{0,240}?\b(?:SELECT|INSERT|UPDATE|DELETE|WHERE|FROM|INTO)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FormattedSqlRegex();

    // Обнаружение конкатенации строк (+) рядом с SQL-ключевыми словами
    [GeneratedRegex(
        @"(?:""[^""\r\n]*?\b(?:SELECT|INSERT|UPDATE|DELETE|WHERE\s+\w+\s*=|FROM\s+\w+|VALUES\s*\()[^""\r\n]*?""\s*\+|\+\s*""[^""\r\n]*?\b(?:WHERE|AND|OR|VALUES|LIMIT|ORDER\s+BY)\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConcatenatedSqlRegex();

    // Обнаружение передачи не-константного выражения в CreateCommand / NpgsqlCommand / CommandText
    [GeneratedRegex(
        @"(?:CreateCommand|new\s+NpgsqlCommand)\s*\(\s*(?:\$\s*""|string\.|[A-Za-z_]\w*\s*\+|""[^""]*""\s*\+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonConstantCommandRegex();

    public static GateScanResult ScanDirectory(string rootDirectory, GateRuleOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return new GateScanResult(
                IsPassed: false,
                ScannedFilesCount: 0,
                Violations:
                [
                    new GateViolation(
                        RuleId: "FAIL_CLOSED_DIR_NOT_FOUND",
                        FilePath: rootDirectory ?? "<null>",
                        LineNumber: 0,
                        Description: "Директория для сканирования не найдена (fail-closed).")
                ]);
        }

        var files = Directory
            .EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f =>
                (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                 f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) &&
                !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith("SqlAndOrmArchitectureGate.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            return new GateScanResult(
                IsPassed: false,
                ScannedFilesCount: 0,
                Violations:
                [
                    new GateViolation(
                        RuleId: "FAIL_CLOSED_EMPTY_SCAN",
                        FilePath: rootDirectory,
                        LineNumber: 0,
                        Description: "Не найдено ни одного исходного файла для проверки (fail-closed).")
                ]);
        }

        var sources = new List<(string FilePath, string Content)>(files.Count);
        foreach (var file in files)
        {
            sources.Add((file, File.ReadAllText(file)));
        }

        return ScanSources(sources, options);
    }

    public static GateScanResult ScanSources(
        IReadOnlyList<(string FilePath, string Content)> sources,
        GateRuleOptions? options = null)
    {
        options ??= new GateRuleOptions();

        if (sources is null || sources.Count == 0)
        {
            return new GateScanResult(
                IsPassed: false,
                ScannedFilesCount: 0,
                Violations:
                [
                    new GateViolation(
                        RuleId: "FAIL_CLOSED_EMPTY_SOURCES",
                        FilePath: "<memory>",
                        LineNumber: 0,
                        Description: "Список исходников для проверки пуст (fail-closed).")
                ]);
        }

        var violations = new List<GateViolation>();
        var scannedCount = 0;

        foreach (var (filePath, content) in sources)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                violations.Add(new GateViolation(
                    RuleId: "FAIL_CLOSED_EMPTY_FILE",
                    FilePath: filePath,
                    LineNumber: 1,
                    Description: "Пустой исходный файл не допускается (fail-closed)."));
                continue;
            }

            scannedCount++;

            if (options.CheckOrmUsage)
            {
                CheckForbiddenOrm(filePath, content, violations);
            }

            if (options.CheckSqlInterpolation)
            {
                CheckRegexRule(filePath, content, InterpolatedSqlRegex(), "SQLI_INTERPOLATION", "Обнаружена интерполяция строк в SQL-запросе.", violations);
                CheckRegexRule(filePath, content, FormattedSqlRegex(), "SQLI_STRING_FORMAT", "Обнаружено форматирование/склейка SQL через string.Format/Concat/Join.", violations);
            }

            if (options.CheckSqlConcatenation)
            {
                CheckRegexRule(filePath, content, ConcatenatedSqlRegex(), "SQLI_CONCATENATION", "Обнаружена конкатенация строк (+) в SQL-запросе.", violations);
            }

            if (options.CheckRawCommandCreation)
            {
                CheckRegexRule(filePath, content, NonConstantCommandRegex(), "SQLI_DYNAMIC_COMMAND", "Обнаружена передача динамического выражения в команду БД.", violations);
            }
        }

        return new GateScanResult(
            IsPassed: scannedCount > 0 && violations.Count == 0,
            ScannedFilesCount: scannedCount,
            Violations: violations);
    }

    private static void CheckForbiddenOrm(
        string filePath,
        string content,
        List<GateViolation> violations)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var token in ForbiddenOrmTokens)
            {
                if (line.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(new GateViolation(
                        RuleId: "FORBIDDEN_ORM",
                        FilePath: filePath,
                        LineNumber: i + 1,
                        Description: $"Обнаружено запрещённое использование ORM-токена '{token}'."));
                }
            }
        }
    }

    private static void CheckRegexRule(
        string filePath,
        string content,
        Regex regex,
        string ruleId,
        string description,
        List<GateViolation> violations)
    {
        foreach (Match match in regex.Matches(content))
        {
            var line = GetLineNumber(content, match.Index);
            violations.Add(new GateViolation(
                RuleId: ruleId,
                FilePath: filePath,
                LineNumber: line,
                Description: description));
        }
    }

    private static int GetLineNumber(string content, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
