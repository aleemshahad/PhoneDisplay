param(
    [Parameter(Mandatory = $true)]
    [string]$Prefix
)

$ErrorActionPreference = 'Continue'
$branch = 'ci-logs'

if (-not (Test-Path 'build-logs')) {
    Write-Host 'no build-logs directory, nothing to publish'
    exit 0
}

$files = @(Get-ChildItem 'build-logs' -File)

if ($files.Count -eq 0) {
    Write-Host 'build-logs is empty, nothing to publish'
    exit 0
}

git config user.name 'github-actions[bot]'
git config user.email '41898282+github-actions[bot]@users.noreply.github.com'

$stamp = "$Prefix-run$env:GITHUB_RUN_NUMBER"
$same = (git status --porcelain -- build-logs | Measure-Object).Count -eq 0

if ($same) {
    $last = git log -1 --format='%s' -- build-logs
    if ($last -eq "ci logs: $stamp") {
        Write-Host 'logs already committed'
    }
    else {
        git commit --allow-empty -m "ci logs: $stamp" | Out-Null
    }
}
else {
    git add -f build-logs
    git commit -m "ci logs: $stamp" | Out-Null
}

if ($LASTEXITCODE -ne 0) {
    Write-Host 'log commit failed (non fatal)'
    exit 0
}

git push origin "HEAD:refs/heads/$branch" --force | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host 'log push failed (non fatal)'
    exit 0
}

Write-Host "logs published to branch $branch"
exit 0