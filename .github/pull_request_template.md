## Summary

Describe what changed and why.

## Type of change

- [ ] Bug fix
- [ ] Feature or enhancement
- [ ] Documentation
- [ ] Dependency or build change
- [ ] Refactoring / maintenance

## Validation

Describe the checks you ran and their results.

```text
# Example:
dotnet restore CryptoBook/CryptoBook.sln --locked-mode
dotnet build CryptoBook/CryptoBook.sln -c Release --no-restore
dotnet test CryptoBook/CryptoBook.sln -c Release --no-restore
```

## Security and data integrity

- [ ] This change does not affect encryption, key handling, recovery, file replacement,
      backups, or release signing.
- [ ] If it does affect one of those areas, the risk and validation are described below.

Security/data-integrity notes:

## UI changes

If the UI changed, attach before/after screenshots or a short recording when useful.

## Checklist

- [ ] The change is focused and does not include unrelated edits.
- [ ] Tests were added or updated when behavior changed.
- [ ] Documentation was updated when user-visible behavior changed.
- [ ] No secrets, private documents, passwords, keys, or local machine state were committed.
