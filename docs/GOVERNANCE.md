# Hero Passport — Repository Governance

Snapshot: 2026-09-12

This document records the effective GitHub-side merge governance for the default branch. It is evidence of repository policy, not a substitute for GitHub enforcement.

## Active ruleset

Repository ruleset ID: `23050922`

Name: `build-test`

Target: branch

Enforcement: active

Ref condition: `~DEFAULT_BRANCH`

Bypass actors: none

Effective default branch: `main`

GitHub reports `main` as protected.

## Enforced rules

The active ruleset enforces all of the following on the default branch:

- changes must go through a pull request;
- zero approving reviews are required, avoiding a single-maintainer deadlock;
- required status check: `build-test`;
- required check source: GitHub Actions integration ID `15368`;
- branch deletion is blocked;
- non-fast-forward updates are blocked, which prevents force-push rewriting;
- there are no bypass actors.

`release-platform` is intentionally not an unconditional required status check. Its pull-request workflow uses path filtering, so legitimate changes outside those paths may not create that check. The always-present PR merge gate is `.github/workflows/ci.yml` job `build-test`.

The required-status policy is not configured as strict/up-to-date-with-base. A passing `build-test` for the PR head is required, while the ruleset does not additionally require the branch to be rebased onto the latest `main` before merge.

## Acceptance evidence

Issue #34 owns the governance rollout. Its closure requires executable GitHub evidence that a PR cannot merge while `build-test` is pending or failing and can merge after `build-test` succeeds.

The immutable `v0.1.0` release remains outside this governance change and is not rewritten by enabling repository rules.
