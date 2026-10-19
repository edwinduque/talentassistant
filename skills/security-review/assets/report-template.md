# Security review report — {target} ({mode}) — {date}

Reviewed by AI-assisted process (skill `security-review`). **Not a professional security audit; OWASP/ASVS references do not guarantee coverage.**

## 1. Scope and method

- Target: {spec path | diff range | repo area}.
- Trust boundaries examined: {browser↔Web, Web↔API, API↔MySQL, API↔HCM Postgres, API↔Groq, model↔tools}.
- Mode depth: {spec|diff|audit}; checklist sections applied: {list}.
- Verification commands actually executed (name + observed result):

| Command | Observed |
| --- | --- |
| {e.g. `dotnet test ... --filter ...`} | {counts/result} |

## 2. Summary

| ID | Severity | Confidence | State | Short claim |
| --- | --- | --- | --- | --- |
| SEC-001 | High | High | Confirmed vulnerability | {one line} |

States: `Confirmed vulnerability` · `Potential risk (needs validation)` · `Verified control (evidence)` · `Not applicable (justified)` · `Not verified`.

## 3. Findings

### SEC-{nnn}: {title}

- Severity: {level} — justification: {why this level}.
- OWASP 2025: {A0X (name)}; ASVS: {family}.
- Location: {file:symbol} or {file:line}.
- Evidence observed: {code excerpt summary or command output — never secret values}.
- Exploitation scenario and impact: {concrete steps and consequence}.
- Recommendation: {actionable fix or control}.
- Suggested test: {test that proves the fix}.
- Confidence and limitations: {High/Medium/Low — what was and was not inspected}.

## 4. Unverified areas

{Explicit list of checklist items or surfaces not inspected and why. The report cannot be marked approved while this list contains relevant areas.}

## 5. Proposed SDD integration

- Candidate acceptance criteria: {AC-xxx text proposals linked to findings}.
- Candidate tasks: {T-x proposals, spec amendments needed}.
- Decisions requiring the user: {policy items, e.g. egress, multi-tenant scope}.
