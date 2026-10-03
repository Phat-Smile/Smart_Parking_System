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
