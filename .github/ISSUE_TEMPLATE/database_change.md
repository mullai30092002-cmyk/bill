---
name: Database change
description: Propose a schema, migration, seed, or data model change
title: "[Database]: "
labels: ["Type: Database"]
body:
  - type: textarea
    id: reason
    attributes:
      label: Reason for change
    validations:
      required: true
  - type: textarea
    id: tables
    attributes:
      label: Tables affected
    validations:
      required: true
  - type: textarea
    id: migration
    attributes:
      label: Migration requirement
      description: New table, column, index, constraint, seed data, or data correction.
    validations:
      required: true
  - type: textarea
    id: data-integrity
    attributes:
      label: Data integrity rules
      description: Constraints, uniqueness, foreign keys, status rules, or ledger rules.
    validations:
      required: true
  - type: textarea
    id: audit
    attributes:
      label: Audit impact
    validations:
      required: false
  - type: textarea
    id: rollback
    attributes:
      label: Rollback notes
    validations:
      required: false
