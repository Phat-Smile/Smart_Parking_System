# AI service tests

Use Python 3.12 with dependencies installed from `../requirements.lock`. From `ai/ai-service`:

```sh
python -m unittest discover -s tests -v
```

The current suite covers HTTP ProblemDetails/correlation, concurrent request isolation, standard gRPC Health/Check, Development reflection and its absence when disabled, and telemetry registration/filtering. The Phase 07 baseline is 10 tests plus 3 subtests; future focused tests may increase that count. There are no business AI tests.
