# P1.14 Invoice gate: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The driving invoice of L-17: a 10-page German invoice built only through the public API, with synthetic data, and one gate test that holds every check. The viewer checklist gains the file, its description and a clipboard check of page 1.

**Architecture:**
- **The data** is synthetic and lives in code: the seller, the buyer, the invoice number and date, the line items, the bank details and the registration. The item count is chosen so the document runs to exactly 10 pages.
- **The document** is built by one static method from the data, through the public API only: `Document`, page builders, `Column`, `Row`, `Text`, borders and padding. It is deterministic, with a fixed creation date.
- **The gate test** writes the file with `GeneratedFiles`, beside a `.txt` of the strings page 1 must copy out, and runs every check of L-17 on it.

**Tech Stack:** .NET 10, C#, xUnit v3; qpdf and poppler (`pdfinfo`, `pdftotext`, `pdffonts`, `pdftoppm`).

**Spec:** [P1 Layout and fluent API](../specs/2026-11-23-p1-layout-design.md), decision L-17. Work item [#39](https://github.com/Maviray/pdf/issues/39). The branch `feature/39-invoice-gate` is stacked on `feature/38-default-fonts-package` (PR #48).

## Global Constraints

- The invoice uses only the public API (`using Maviray.Pdf;`), with fonts from `RegisterNotoSans()` on a registry of its own.
- The files live in `tests/Maviray.Pdf.Tests/Invoice/`, one type per file.
- Amounts are formatted without culture data: a `NumberFormatInfo` with `,` as the decimal and `.` as the group separator, then ` €`.
- Each task ends with one commit, subject `#39 <summary>`. The records are the last commit.

## Rulings made while planning

1. **The column headings are in the header,** as L-17 says, so on page 1 they stand above the addresses. P1 has no first-page-only header.
2. **Each item's description is unique and fits on one line** of its column: it carries a three-digit lot number, so no description is a substring of another, and the in-order check can search the page text.
3. **The page-1 clipboard check looks for strings, not lines.** Viewers paste the cells of a row in different orders and with different separators, so the checklist's snippet checks that each line of the `.txt` occurs in the copied text, exactly.
4. **"No ink outside the margins"** renders each page at 72 dpi and finds no pixel with a channel under 128 left of `floor(left)`, right of `ceil(right)`, above `floor(top)` or below `ceil(bottom)`.

## Review Focus

1. **Exactly 10 pages,** and the last page holds the totals, not only a page of footer.
2. **Every page's header and footer,** including the column headings and `Seite n von 10` on every page.
3. **Each description once, in order,** across the page breaks, with none lost or repeated at a break.
4. **Turkish and Cyrillic strings** in the buyer and in item descriptions copy out exactly: `İ`, `ı`, `ş`, `ğ`, `ё`, `й`.
5. **Deterministic bytes** across two writes.

---

### Task 1: The invoice and its data

**Files:**
- Create in `tests/Maviray.Pdf.Tests/Invoice/`: `InvoiceParty.cs`, `InvoiceItem.cs`, `InvoiceData.cs`, `InvoiceSample.cs`, `InvoiceAmounts.cs`, `InvoiceDocument.cs`

**Interfaces:**
- `InvoiceParty(string Name, string Street, string City, string? Contact)`; `InvoiceItem(int Position, string Description, decimal Quantity, string Unit, decimal UnitPrice, int VatRate)` with `Net`; `InvoiceData(string Number, string Date, InvoiceParty Seller, InvoiceParty Buyer, IReadOnlyList<InvoiceItem> Items, string Bank, string Registration)` with the net sum, the VAT per rate and the gross total.
- `InvoiceSample.Create()` returns the invoice; `InvoiceSample.TurkishAndCyrillic` lists the strings that must copy out.
- `InvoiceAmounts.Format(decimal)` returns, for example, `1.234,56 €`.
- `InvoiceDocument.Create(InvoiceData invoice, FontRegistry fonts)` returns the `Document`. (Executed as `InvoiceData`, not `Invoice`: a type named like its namespace makes references ambiguous.)

- [ ] **Test** (`InvoiceDocumentTests.cs`): `AmountsAreGerman` (`1234.5m` → `1.234,50 €`, `0m` → `0,00 €`); `TheTotalsAddUp` (net, 19 % and 7 % VAT, gross); `TheDescriptionsAreUniqueAndNoneHoldsAnother`.
- [ ] Red, implement, the suite. Commit `#39 add the invoice and its data`.

### Task 2: The gate

**Files:**
- Test: `tests/Maviray.Pdf.Tests/Invoice/InvoiceGateTests.cs`

- [ ] **Test:** `InvoiceGatePassesEveryCheck`:
  - `pdfinfo` reports 10 pages;
  - each page's text holds the seller's name, the invoice number, every column heading and `Seite <n> von 10`;
  - across the pages, each description occurs exactly once, and in order; the totals are on page 10 only;
  - every string of `TurkishAndCyrillic` occurs in the text;
  - qpdf accepts the file, and `pdffonts` lists every font as embedded, subset and Unicode-mapped;
  - no page has ink outside its margins (ruling 4);
  - two writes give identical bytes.
  - The file is saved with `GeneratedFiles`, with `InvoiceGatePassesEveryCheck.txt` beside it: the strings page 1 must copy out.
- [ ] Red: with one item fewer or more than the count that makes 10 pages, the page check fails. Then green and the suite. Commit `#39 add the invoice gate`.

### Task 3: The viewer checklist

**Files:**
- Modify: `docs/qa/checklists/viewer-check.md`

- [ ] The invoice file joins the list of files, the file paths and the report template; a section describes what it should look like; the clipboard check for it uses a snippet that finds each line of the `.txt` in the copied text, ordinally. Status date today.
- [ ] Commit `#39 add the invoice to the viewer checklist`.

### Task 4: Records

- [ ] The log, the index row, `#39 add the P1.14 plan and execution log`, the PR stacked on #48, and the CI run.
