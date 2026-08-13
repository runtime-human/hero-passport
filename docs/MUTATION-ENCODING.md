# Hero Passport — Canonical Mutation Encoding

**Status:** Accepted v3.2.1 implementation clarification  
**Snapshot:** 2026-08-11  
**Encoding:** `mutation-args/1`

This document makes the byte-level encoding from `DATA-MODEL.md` Section 9 exact. It does not change mutation semantics; it removes implementation ambiguity needed for durable late retries.

## 1. Hash

```text
args_hash = SHA-256(canonical mutation bytes)
```

Request IDs are excluded. Only already-validated canonical semantic scope/arguments are encoded.

## 2. Stream format

Canonical bytes start with the five ASCII bytes:

```text
48 50 4d 41 01    # "HPMA" + version byte 0x01
```

Then emit frames in the operation-specific order below.

Every frame is:

```text
tag        1 byte
length     unsigned 32-bit big-endian
value      exactly `length` bytes
```

Tag `0x00` is always the UTF-8 operation key. Operation fields use tags `0x01`, `0x02`, ... in fixed schema order. Unknown/extra fields are never encoded.

## 3. Canonical scalar values

```text
string/enum     UTF-8 bytes of the already canonical validated value
UUIDv7          ASCII/UTF-8 lowercase canonical D form (36 bytes)
bool            one byte: 0x00 false, 0x01 true
bounded integer signed 32-bit big-endian
```

Strings are already SafeTextV1-normalized where the field contract requires SafeText. Do not normalize again inside the encoder.

## 4. Canonical string list

A list is one framed value. Its value bytes are:

```text
count      1 byte
each item:
  length   unsigned 32-bit big-endian
  UTF-8 item bytes
```

`skillsUsed` therefore preserves validated primary/secondary/tertiary order. No sorting occurs in the encoder.

## 5. Operation frames

### `bootstrap`

```text
0x00 operation = "bootstrap"
0x01 locale
0x02 heroName
0x03 presentationStyle
0x04 autoStartQuest
0x05 autoFinishQuest
```

### `create_hero`

```text
0x00 operation = "create_hero"
0x01 name
```

### `start_quest`

```text
0x00 operation = "start_quest"
0x01 ProjectId
0x02 HeroId
0x03 questType
0x04 title
0x05 goal
```

### `finish_quest`

```text
0x00 operation = "finish_quest"
0x01 questId
0x02 result
0x03 summary
0x04 testsMentioned
0x05 scopeViolations
0x06 userCorrections
0x07 buildStatus
0x08 buildEvidence
0x09 testsStatus
0x0A testsEvidence
0x0B skillsUsed list
```

## 6. Compatibility rule

A receipt/report stores `args_encoding_version` or `finalization_args_encoding_version`. A later release replaying an old request MUST use the stored encoder version; it must not reinterpret an old hash with current serialization rules.

Changing tags, field order, scalar representation, list representation or framing requires a new encoding version.

## 7. Required tests

Commit byte-level golden vectors for all four operations. At minimum assert:

```text
same canonical fields -> same bytes/hash
changed field -> changed hash
request ID does not affect hash
start ProjectId/HeroId changes affect hash
skills order affects finish hash
UTF-8/non-ASCII SafeText is stable
```
