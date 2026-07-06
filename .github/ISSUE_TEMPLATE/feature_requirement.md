---
name: Feature requirement
description: Define a new BillSoft feature or product requirement
title: "[Feature]: "
labels: ["Type: Requirement"]
body:
  - type: textarea
    id: goal
    attributes:
      label: Goal
      description: What business problem does this solve?
    validations:
      required: true
  - type: dropdown
    id: domain
    attributes:
      label: Domain
      options:
        - Billing
        - Kitchen
        - Inventory
        - Vendor
        - OCR
        - Cash Control
        - Audit
        - Owner Dashboard
        - Reports
        - Admin
    validations:
      required: true
  - type: textarea
    id: users
    attributes:
      label: User roles affected
      description: Owner, Admin, Cashier, Waiter, KitchenUser, InventoryUser, AccountsUser, SuperAdmin
    validations:
      required: true
  - type: textarea
    id: expected
    attributes:
      label: Expected behavior
    validations:
      required: true
  - type: textarea
    id: business-rules
    attributes:
      label: Business rules
      description: Include fraud-control, status, money, stock, and confirmation rules.
    validations:
      required: true
  - type: textarea
    id: audit
    attributes:
      label: Audit requirement
      description: What must be logged?
    validations:
      required: false
  - type: textarea
    id: database
    attributes:
      label: Database impact
      description: Tables/columns/migrations affected.
    validations:
      required: false
  - type: textarea
    id: api
    attributes:
      label: API impact
    validations:
      required: false
  - type: textarea
    id: ui
    attributes:
      label: UI impact
    validations:
      required: false
  - type: textarea
    id: acceptance
    attributes:
      label: Acceptance criteria
      description: Use clear testable bullet points.
    validations:
      required: true
