$ErrorActionPreference = 'Stop'

$targets = [ordered]@{
    'identity-api' = 'identity-api:8081'
    'parking-api' = 'parking-api:8081'
    'booking-api' = 'booking-api:8081'
    'payment-api' = 'payment-api:8081'
    'notification-api' = 'notification-api:8081'
    'ai-service' = 'ai-service:50051'
}
$allowedServices = @(
    'grpc.health.v1.Health',
    'grpc.reflection.v1.ServerReflection',
    'grpc.reflection.v1alpha.ServerReflection'
)

foreach ($service in $targets.Keys) {
    $address = $targets[$service]
    $reflected = & docker run --rm --network parking-network fullstorydev/grpcurl:v1.9.3 `
        -plaintext -max-time 10 $address list
    if ($LASTEXITCODE -ne 0) {
        throw "Reflection listing failed for $service ($address)."
    }

    $actualServices = @($reflected | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $unexpectedServices = @($actualServices | Where-Object { $_ -notin $allowedServices })
    $hasReflection = @($actualServices | Where-Object { $_ -like 'grpc.reflection.*.ServerReflection' }).Count -gt 0
    if ($unexpectedServices.Count -gt 0 -or 'grpc.health.v1.Health' -notin $actualServices -or -not $hasReflection) {
        throw "Unexpected reflected services for $service ($address): $($actualServices -join ', ')"
    }

    $healthResult = & docker run --rm --network parking-network fullstorydev/grpcurl:v1.9.3 `
        -plaintext -max-time 10 -d '{"service":""}' $address grpc.health.v1.Health/Check
    if ($LASTEXITCODE -ne 0 -or ($healthResult -join "`n") -notmatch '"status"\s*:\s*"SERVING"') {
        throw "Standard gRPC health check did not report SERVING for $service ($address)."
    }

    Write-Output "$service ($address): SERVING; reflected services: $($actualServices -join ', ')"
}
