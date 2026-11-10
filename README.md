# moisson-project
Windows desktop application for accurate donation valuation and tax receipt generation for Moisson Montréal (Montréal, QC).
## Running tests

```bash
dotnet test                                   # all tests (Docker must be running)
dotnet test --filter "Category!=Integration"  # unit tests only, no Docker needed
```

Integration tests use Testcontainers to start a disposable PostgreSQL 17 container, so Docker Desktop (or Docker Engine) must be running.


