---
name: Bug report
description: Report incorrect product behavior
title: "[Bug]: "
labels: ["Type: Bug"]
body:
  - type: textarea
    id: summary
    attributes:
      label: Summary
    validations:
      required: true
  - type: textarea
    id: current
    attributes:
      label: Current behavior
    validations:
      required: true
  - type: textarea
    id: expected
    attributes:
      label: Expected behavior
    validations:
      required: true
  - type: dropdown
    id: severity
    attributes:
      label: Severity
      options:
        - P0 - Critical flow blocked
        - P1 - Major issue
        - P2 - Minor issue
        - P3 - Low risk
    validations:
      required: true
  - type: textarea
    id: reproduce
    attributes:
      label: Steps to reproduce
    validations:
      required: true
  - type: textarea
    id: impact
    attributes:
      label: Business impact
      description: Explain whether this affects sales, stock, vendor bills, reports, or audit records.
    validations:
      required: true
  - type: textarea
    id: evidence
    attributes:
      label: Evidence
      description: Add screenshots, logs, references, or notes.
    validations:
      required: false
