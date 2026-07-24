$ErrorActionPreference = 'Stop'

$stdin = [Console]::In.ReadToEnd()
if (-not $stdin) { exit 0 }

try {
    $data = $stdin | ConvertFrom-Json
} catch {
    exit 0
}

$toolName = $data.tool_name
$filePath = $data.tool_input.file_path
if (-not $filePath) { $filePath = $data.tool_response.filePath }
if (-not $filePath) { exit 0 }

$logPath = Join-Path $PSScriptRoot 'edit-log.txt'
$timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
"$timestamp`t$toolName`t$filePath" | Out-File -FilePath $logPath -Append -Encoding utf8

exit 0
