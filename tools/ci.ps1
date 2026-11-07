# Usado só pelo GitHub Actions. O .exe é de janela (WinExe): o PowerShell não espera
# por ele nem pega o código de saída sozinho, então se abre o processo à mão.
function Invoke-App([string]$exe, [string[]]$arguments, [int]$timeoutSec) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new((Resolve-Path $exe).Path)
    foreach ($a in $arguments) { $psi.ArgumentList.Add($a) }
    $psi.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psi)
    if (-not $p.WaitForExit($timeoutSec * 1000)) {
        $p.Kill($true)
        throw "$exe passou de $timeoutSec s"
    }
    return $p.ExitCode
}
