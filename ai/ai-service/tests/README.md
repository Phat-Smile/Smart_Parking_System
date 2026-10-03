# AI Service tests

Test scaffolding is reserved here. Add tests when AI behavior is introduced; do not fabricate model predictions in this phase.
# AI service tests

With Python 3.12 and the project dependencies installed, run from `ai/ai-service`:

```sh
python -m unittest discover -s tests -v
```

The gRPC tests cover standard Health/Check, development reflection contents, and the absence of reflection when disabled.
