# Test Decision: TDD Red Phase for Typestate Refactor

**Author:** Edward Thomas (Tester)  
**Date:** 2025-01-15  
**Issue:** #22  
**Branch:** feature/typestate-refactor  
**Commit:** d0e2ea9

## Decision

Write tests **first** (TDD red phase) that reference the new typestate types before they exist. Tests intentionally fail to compile until Types.fs is updated.

## Rationale

1. **Design verification** — Writing tests against Talyllyn's ADR forces us to validate the proposed API ergonomics before implementation
2. **Specification lock-in** — Tests document exact expected behavior: error messages, validation rules, normalization (trailing slash trimming, whitespace handling)
3. **No rework** — When types are implemented to pass these tests, we know the refactor is complete
4. **Regression safety** — 53 total tests ensure no existing behavior breaks

## Test Coverage Strategy

### New validation tests (13 tests):
- **BaseUrl validation:** empty strings, protocol checks, trailing slash normalization, http/https acceptance
- **CommKey validation:** empty/whitespace rejection, trimming behavior
- **Config factory validation:** integration tests for `createConfig` and `createConfigWithUrl` error paths

### Updated integration tests (14 tests):
- **discoverCommKey** — assert `CommKey` type in Ok branch instead of raw string
- **createConfig/createConfigWithUrl** — unwrap Result, use `.value` accessors
- **sendRequest** — construct configs via smart constructors instead of record literals
- **ApiClient tests** — single testConfig helper fix propagates to all 6 tests

### Unaffected tests (39 tests):
- **TreeNavigationTests.fs** — no ApiConfig usage, zero changes
- **Most TypesTests.fs** — JSON deserialization tests unaffected

## Key Test Patterns Used

```fsharp
// Pattern 1: Test validation with Result unwrapping
match BaseUrl.create "" with
| Error (ConfigError msg) -> Assert.Contains("empty", msg)
| _ -> Assert.Fail("Expected ConfigError")

// Pattern 2: Test normalization
match BaseUrl.create "http://localhost:31270/" with
| Ok url -> Assert.Equal("http://localhost:31270", BaseUrl.value url)
| _ -> ...

// Pattern 3: Integration test with smart constructors
match CommKey.create "key" with
| Ok key ->
    let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
    // ... test using config
| Error e -> Assert.Fail($"Failed to create test config: {e}")
```

## Risk Mitigation

**Risk:** Tests don't compile until types exist  
**Mitigation:** Clearly documented in commit message; expected for TDD red phase

**Risk:** Test might not match final implementation  
**Mitigation:** Tests written directly from Talyllyn's ADR with exact function signatures

**Risk:** Excessive test changes during green phase  
**Mitigation:** Tests target behavior, not implementation; minimal changes expected

## Outcome

- **10 new tests** in TypesTests.fs covering BaseUrl/CommKey validation
- **3 new tests** in HttpTests.fs for config factory error paths
- **14 updated tests** across HttpTests.fs (7) and ApiClientTests.fs (1 fix → 6 tests)
- **0 tests removed** — pure additive + refactor
- **Branch pushed** to `feature/typestate-refactor`

Next: Talyllyn implements types per ADR. Tests should compile and pass with zero test changes required.
