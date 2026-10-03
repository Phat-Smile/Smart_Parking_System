# Error handling

The Gateway and all five .NET HTTP API hosts use the shared `ParkingSystem.AspNetCore` web foundation. The AI HTTP host applies the same safe response and correlation rules. Domain, Application, Persistence, and Infrastructure remain independent of ASP.NET Core.

## HTTP error responses

Errors use `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`, `traceId`, `correlationId`, and `errorCode`. `instance` is the path only; query strings are excluded. The response never includes an exception message, stack trace, SQL, connection string, configured credential, or submitted field value.

Unhandled exceptions return status 500, title `An unexpected error occurred.`, a generic retry detail, and error code `internal.unexpected`. Internal logs retain a bounded exception diagnostic after redacting configured secret values and common credential patterns; they do not attach the raw exception object.

Existing FluentValidation failures map to status 400 `ValidationProblemDetails`; the `errors` map contains field names and generic messages, not validator messages or attempted values. The stable error code is `request.validation_failed`. No new NotFound or Conflict exception types are introduced in this phase. Status-code-only responses, including empty YARP 502/504 failures, receive a generic ProblemDetails body; a downstream response that already has a body is passed through.

The existing `ParkingSystem.BuildingBlocks.Results.Result<T>` and `Error` types remain transport-neutral. HTTP endpoints may map them to HTTP status and ProblemDetails when endpoints are implemented; that mapping is not part of this foundation phase.

## Logging

Each .NET HTTP host has one JSON console provider. Request events contain method, path without query, status, duration, trace ID, and correlation scope. Successful `/health/live` and `/health/ready` requests log at Debug; 4xx at Information; 5xx at Error. Request and response bodies, authorization/cookie headers, and exception objects are not logged by this middleware. The Outbox dispatcher emits one startup event; polling failures log the exception type only and per-message failures retain the existing sanitized error text.

The AI service uses structured JSON request events with the same fields and status-level policy. Uvicorn access logging is disabled so routine health probes are not duplicated. The IoT simulator's existing startup prints report only broker host/port and whether credentials were configured; they do not print credential values.
