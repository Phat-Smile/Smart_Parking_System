[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$keyDirectory = Join-Path $repositoryRoot '.local\security\jwt'
$privateKeyPath = Join-Path $keyDirectory 'private.pem'
$publicKeyPath = Join-Path $keyDirectory 'public.pem'

New-Item -ItemType Directory -Path $keyDirectory -Force | Out-Null
if ((Test-Path -LiteralPath $privateKeyPath) -or (Test-Path -LiteralPath $publicKeyPath)) {
    throw 'A development JWT key file already exists. The script will not overwrite either key.'
}

if ($PSVersionTable.PSVersion.Major -lt 7) {
    # Windows PowerShell 5.1 runs on .NET Framework, which cannot export PEM keys. Use openssl instead.
    $openssl = Get-Command openssl -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
    if (-not $openssl) {
        $gitOpenssl = Join-Path $env:ProgramFiles 'Git\usr\bin\openssl.exe'
        if (Test-Path -LiteralPath $gitOpenssl) { $openssl = $gitOpenssl }
    }
    if (-not $openssl) {
        throw 'PowerShell 7 or openssl is required. Install PowerShell 7 (winget install Microsoft.PowerShell) or Git for Windows, then run this script again.'
    }

    # openssl writes progress to stderr; Windows PowerShell would turn that into a terminating error.
    $ErrorActionPreference = 'Continue'
    try {
        & $openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out $privateKeyPath 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'openssl could not generate the private key.' }
        & $openssl pkey -in $privateKeyPath -pubout -out $publicKeyPath 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'openssl could not export the public key.' }
    }
    catch {
        Remove-Item -LiteralPath $privateKeyPath, $publicKeyPath -Force -ErrorAction SilentlyContinue
        throw
    }
    finally {
        $ErrorActionPreference = 'Stop'
    }

    Write-Output 'Generated local development JWT key pair. Private key contents were not displayed.'
    return
}

$rsa = [System.Security.Cryptography.RSA]::Create(3072)
$privateCreated = $false
try {
    $privatePem = $rsa.ExportPkcs8PrivateKeyPem()
    $publicPem = $rsa.ExportSubjectPublicKeyInfoPem()

    $privateStream = [System.IO.File]::Open($privateKeyPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    $privateCreated = $true
    try {
        $privateBytes = [System.Text.Encoding]::ASCII.GetBytes($privatePem)
        $privateStream.Write($privateBytes, 0, $privateBytes.Length)
    }
    finally {
        $privateStream.Dispose()
    }

    $publicStream = [System.IO.File]::Open($publicKeyPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $publicBytes = [System.Text.Encoding]::ASCII.GetBytes($publicPem)
        $publicStream.Write($publicBytes, 0, $publicBytes.Length)
    }
    finally {
        $publicStream.Dispose()
    }

    Write-Output 'Generated local development JWT key pair. Private key contents were not displayed.'
}
catch {
    if ($privateCreated -and (Test-Path -LiteralPath $privateKeyPath)) {
        Remove-Item -LiteralPath $privateKeyPath -Force
    }
    throw
}
finally {
    $rsa.Dispose()
}
