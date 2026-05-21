$inputFile  = "\\192.168.7.225\korzina\Дементьев\MagicEntry_Logs\debug_users.txt"
$outputFile = "$env:TEMP\ad_users_report.txt"

$now = Get-Date -Format "yyyy.MM.dd HH:mm"

# Читаем логины
$usernames = Get-Content $inputFile |
    ForEach-Object { $_.Trim().Trim(',') } |
    Where-Object { $_ -ne "" }

$users = foreach ($username in $usernames) {
    $searcher = New-Object DirectoryServices.DirectorySearcher
    $searcher.Filter = "(&(objectClass=user)(sAMAccountName=$username))"

    $result = $searcher.FindOne()
    if ($result) {
        $u = $result.GetDirectoryEntry()

        [PSCustomObject]@{
            Username    = $u.sAMAccountName.Value
            DisplayName = $u.displayName.Value
            Company     = $u.company.Value
            Department  = $u.department.Value
        }
    }
}

$userCount = $users.Count

# Заголовок отчёта
$resultText = @(
    "Вот список пользователей, которые еще не перезагрузили компьютер."
    "Количество пользователей: $userCount"
    "Время формирования отчета: $now"
    "-" * 80
    ""
)

# Группировка Company -> Department
$resultText += foreach ($companyGroup in $users | Group-Object Company) {
    "=== Компания: $($companyGroup.Name) ==="
    foreach ($deptGroup in $companyGroup.Group | Group-Object Department) {
        "  -- Отдел: $($deptGroup.Name)"
        foreach ($u in $deptGroup.Group) {
            "     $($u.Username) | $($u.DisplayName)"
        }
        ""
    }
    ""
}

# Вывод
$resultText | Out-File $outputFile -Encoding UTF8
notepad $outputFile
Clear-Host
