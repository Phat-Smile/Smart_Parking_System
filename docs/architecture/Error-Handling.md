# Error handling

The Gateway and all five .NET HTTP API hosts use the shared `ParkingSystem.AspNetCore` web foundation. The AI HTTP host applies the same safe response and correlation rules. Domain, Application, Persistence, and Infrastructure remain independent of ASP.NET Core.

## HTTP error responses

Errors use `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`, `traceId`, `correlationId`, and `errorCode`. `instance` is the path only; query strings are excluded. The response never includes an exception message, stack trace, SQL, connection string, configured credential, or submitted field value.

Unhandled exceptions return status 500, title `Đã xảy ra lỗi hệ thống.`, a generic retry detail, and error code `internal.unexpected`. Internal logs retain a bounded exception diagnostic after redacting configured secret values and common credential patterns; they do not attach the raw exception object.

FluentValidation failures map to status 400 `ValidationProblemDetails` with error code `request.validation_failed` (decided 2026-10-09, applies to every service):

- `title` is the fixed text `Dữ liệu không hợp lệ.`; `detail` joins the field messages so a client can show it as is.
- `errors` holds **one validator message per field**, the first rule that failed. Validators must set `RuleLevelCascadeMode = CascadeMode.Stop` so the user gets one reason to fix at a time (Identity does; other services adopt it when they write their messages).
- Validator messages are user-facing Vietnamese text written in the validator. They must never contain the submitted value (password, token, OTP). As a safety net, a message containing the submitted value (4 characters or longer) is replaced with `Giá trị không hợp lệ.`.

Business errors returned by handlers (`Result<T>` failures) are mapped by the endpoint to ProblemDetails with `title` and `detail` set to the error message and `errorCode` set to the stable error code. Clients branch on `errorCode`, never on the message text, so messages can be reworded without breaking clients. A message may carry state that helps the user act (remaining attempts, minutes left in a lockout) when that state is already exposed elsewhere by the API.

No new NotFound or Conflict exception types are introduced in this phase. Status-code-only responses, including empty YARP 502/504 failures, receive a generic ProblemDetails body; a downstream response that already has a body is passed through.

The existing `ParkingSystem.BuildingBlocks.Results.Result<T>` and `Error` types remain transport-neutral. HTTP endpoints may map them to HTTP status and ProblemDetails when endpoints are implemented; that mapping is not part of this foundation phase.

## Logging

Each .NET HTTP host has one JSON console provider. Request events contain method, matched route template (or `<unmatched>`), status, duration, trace ID, and correlation scope; raw path and query values are omitted. Successful `/health/live` and `/health/ready` requests log at Debug; 4xx at Information; 5xx at Error. Request and response bodies, authorization/cookie headers, and exception objects are not logged by this middleware. The Outbox dispatcher emits one startup event; polling failures log the exception type only and per-message failures retain the existing sanitized error text.

The AI service uses structured JSON request events with the same fields and status-level policy. Uvicorn access logging is disabled so routine health probes are not duplicated. The IoT simulator's existing startup prints report only broker host/port and whether credentials were configured; they do not print credential values.
