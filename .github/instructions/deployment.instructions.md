---
description: Immutable image identity and safe isolated validation on the StartingWeb server
applyTo: 'scripts/{ci,deploy,ops}/**/*.py'
---

Docker's classic image store reports a configuration digest as `image inspect .Id`; the server's containerd store reports an OCI manifest or index digest. A `docker save`/`load` transfer can therefore preserve every runtime byte while changing the displayed image ID. Validate the archive identity and tag, then compare the loaded configuration and rootfs diff IDs; store the server's verified immutable ID in deployment state and retain the CI identity in the original release manifest. Never resolve this mismatch by accepting a tag without checking its contents.

Real SQL restore tests must create uniquely named containers and volumes on an internal Docker network. Use a read-only SQL principal for application checks, disable migrations and pending-job recovery, and remove only resources whose per-run ownership labels match. Record schema metadata and aggregate counts, never customer rows.

When replaying EF SQL through ODBC `sqlcmd`, strip a UTF-8 BOM from stdin and enable `QUOTED_IDENTIFIER` with `-I`; set the ANSI/index session options explicitly. SqlClient enables options that bare `sqlcmd` may leave off: a script with filtered indexes can otherwise fail with SQL error 1934 even though the same script succeeds through EF. Keep the downloaded SQL artifact unchanged and record its original hash.

The audited GitHub Environment API exposes `can_admins_bypass`. Verify that it is explicitly false, and still require the user's separate activation authorization. Required reviewers and `prevent_self_review` must be configured together; never count the owner's identity or missing reviewer rules as an independent approval.
