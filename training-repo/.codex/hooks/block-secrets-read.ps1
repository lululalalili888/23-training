# PreToolUse hook (Bash matcher): denies any command that references a known
# secrets location, mirroring .claude/settings.json's Read(**/*.pfx) /
# Read(**/UserSecrets/**) deny rules. Codex has no per-tool file-path deny
# list, so this blocks by inspecting the command text itself.

$stdin = [Console]::In.ReadToEnd()
if (-not $stdin) { exit 0 }

try {
    $data = $stdin | ConvertFrom-Json
} catch {
    exit 0
}

$command = $data.tool_input.command
if (-not $command) { exit 0 }

function Get-SecretPathMatch {
    param([string]$Command)

    if ($Command -imatch 'UserSecrets') { return 'UserSecrets' }
    if ($Command -imatch '\.pfx\b') { return '*.pfx' }
    if ($Command -imatch 'appsettings\.Production\.json') { return 'appsettings.Production.json' }

    return $null
}

$match = Get-SecretPathMatch -Command $command
if ($match) {
    $reason = "block-secrets-read: command references a protected secrets path ('$match'). If this is intentional, run it manually outside Codex."
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
