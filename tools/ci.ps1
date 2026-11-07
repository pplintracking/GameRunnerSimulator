# Usado só pelo GitHub Actions. O .exe é de janela (WinExe): o PowerShell não espera
# por ele nem pega o código de saída sozinho, então se abre o processo à mão.
function Invoke-App([string]$exe, [string[]]$arguments, [int]$timeoutSec) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new((Resolve-Path $exe).Path)
    foreach ($a in $arguments) { $psi.ArgumentList.Add($a) }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardError = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $err = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($timeoutSec * 1000)) {
        Show-Stuck $p | Out-Host
        $p.Kill($true)
        $p.WaitForExit(5000) | Out-Null
        if ($err.Wait(3000) -and $err.Result) { Write-Host "--- stderr:"; Write-Host $err.Result }
        throw "$exe passou de $timeoutSec s"
    }
    if ($err.Wait(3000) -and $err.Result) { Write-Host "--- stderr:"; Write-Host $err.Result }
    return $p.ExitCode
}

# Quando trava: que janela está aberta (diálogo de erro?) e o que o Windows registrou.
function Show-Stuck($p) {
    try {
        $p.Refresh()
        "--- processo preso: pid $($p.Id), janela '$($p.MainWindowTitle)', CPU $($p.TotalProcessorTime), threads $($p.Threads.Count), memória $([int]($p.WorkingSet64/1MB)) MB, módulos $($p.Modules.Count)"
        Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $cond = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
        foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)) {
            "janela: $($w.Current.Name)"
            foreach ($c in $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
                if ($c.Current.Name) { "  - $($c.Current.ControlType.ProgrammaticName): $($c.Current.Name)" }
            }
        }
        Get-Process | Where-Object { $_.MainWindowTitle } | ForEach-Object { "outra janela: $($_.ProcessName) '$($_.MainWindowTitle)'" }
    } catch { "sem UIAutomation: $_" }
    Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-10) } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -in '.NET Runtime', 'Application Error', 'Windows Error Reporting' } |
        Select-Object -First 4 | ForEach-Object { "--- evento $($_.ProviderName) $($_.Id)"; $_.Message }
}
