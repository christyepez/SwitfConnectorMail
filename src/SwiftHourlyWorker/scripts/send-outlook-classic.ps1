param([Parameter(Mandatory=$true)][string]$PayloadPath)
$ErrorActionPreference = 'Stop'
try {
    $payload = Get-Content -LiteralPath $PayloadPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $outlook = New-Object -ComObject Outlook.Application
    $namespace = $outlook.GetNamespace('MAPI')
    $account = $null
    foreach ($entry in $namespace.Accounts) {
        if ($entry.SmtpAddress -ieq $payload.sender) { $account = $entry; break }
    }
    if ($null -eq $account) {
        throw "La cuenta '$($payload.sender)' no está configurada como cuenta de envío en Outlook clásico. No se enviará desde otra cuenta."
    }
    $mail = $outlook.CreateItem(0)
    $mail.SendUsingAccount = $account
    $mail.To = [string]::Join(';', [string[]]$payload.recipients)
    if ($payload.ccRecipients) {
        $mail.CC = [string]::Join(';', [string[]]$payload.ccRecipients)
    }
    $mail.Subject = [string]$payload.subject
    $mail.HTMLBody = [string]$payload.html
    $mail.Send()
    Write-Output 'Mensaje entregado a la bandeja de salida de Outlook Classic; no garantiza entrega final.'
    exit 0
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
