$ErrorActionPreference = 'Stop'

$stdin = [Console]::In.ReadToEnd()
if (-not $stdin) { exit 0 }

try {
    $data = $stdin | ConvertFrom-Json
} catch {
    exit 0
}

$command = $data.tool_input.command
if (-not $command) { exit 0 }

function Get-DestructiveSqlMatch {
    param([string]$Command)

    if ($Command -imatch '\bDROP\s+DATABASE\b') { return 'DROP DATABASE' }
    if ($Command -imatch '\bDROP\s+TABLE\b') { return 'DROP TABLE' }
    if ($Command -imatch '\bTRUNCATE\s+TABLE\b') { return 'TRUNCATE TABLE' }
    if ($Command -imatch '\bALTER\s+DATABASE\b.*\bSET\s+SINGLE_USER\b') { return 'ALTER DATABASE ... SET SINGLE_USER' }
    if ($Command -imatch '\bxp_cmdshell\b') { return 'xp_cmdshell' }
    if ($Command -imatch '\bsp_msforeachtable\b') { return 'sp_msforeachtable' }
    if ($Command -imatch '\bDELETE\s+FROM\b' -and $Command -inotmatch '\bWHERE\b') { return 'DELETE FROM without WHERE' }
    if ($Command -imatch '\bUPDATE\b.*\bSET\b' -and $Command -inotmatch '\bWHERE\b') { return 'UPDATE ... SET without WHERE' }

    return $null
}

$match = Get-DestructiveSqlMatch -Command $command
if ($match) {
    $reason = "block-destructive-sql: command matches destructive SQL pattern '$match'. If this is intentional, run it manually outside Claude Code."
    $result = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $reason
        }
    } | ConvertTo-Json -Compress -Depth 5
    Write-Output $result
}

exit 0
