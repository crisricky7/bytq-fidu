# Genera .env con secretos aleatorios para el entorno local. No sobrescribe uno existente.
$raiz = Split-Path -Parent $PSScriptRoot
$archivo = Join-Path $raiz ".env"

if (Test-Path $archivo) {
    Write-Output ".env ya existe; no se modifica."
    exit 0
}

function Get-Aleatorio([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    ($buffer | ForEach-Object { $_.ToString("x2") }) -join ""
}

@(
    "POSTGRES_USER=audit"
    "POSTGRES_PASSWORD=$(Get-Aleatorio 16)"
    "RABBITMQ_USER=auditoria"
    "RABBITMQ_PASSWORD=$(Get-Aleatorio 16)"
    "JWT_CLAVE_FIRMA=$(Get-Aleatorio 32)"
) | Set-Content -Path $archivo -Encoding ascii

Write-Output ".env generado."
