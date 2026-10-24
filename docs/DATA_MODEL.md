# Data model

Status: **draft v1** (2026-10-19). Sections are referenced by tasks as `DATA_MODEL §N`.
Field lists are the intended shape; exact types/names may be refined in the task that implements them.

## §1. Base types

### SyncEntity (abstract)
| Field | Type | Notes |
|---|---|---|
| Id | Guid | generated on the device (`Guid.CreateVersion7()`) |
| CreatedAt | DateTimeOffset | UTC |
| UpdatedAt | DateTimeOffset | UTC, set on every change |
| IsDeleted | bool | soft delete |

### Comments on everything
Every user-editable entity has a free-text comment: `Notes` (string?, max 2000 chars) on main entities
(Property, Supplier, Contract, Meter, TariffComponent, TariffRate, Bill, Payment, Category) and `Note` on
small child items (PropertyParameter, MeterReading, BillLine, PaymentAllocation). Comments are searchable
and shown wherever the item is listed (decision 2026-10-20).

### Money / quantities
- `decimal` everywhere. `Currency` enum: `EUR`, `LVL` (historical). Fixed conversion
  **1 EUR = 0.702804 LVL** (used only for reports over pre-2014 data).
- Amount fields are paired with a currency on the owning entity (bill, payment, tariff rate), not per line.
- Rounding helper in Core: `MoneyMath.Round(decimal)` → 2 decimals, `AwayFromZero`.

### YearMonth
Value type for billing periods (`2026-10`). Comparable, `AddMonths`, parse/format `yyyy-MM`.

### Time-versioned values
Pattern: a child list of `{ ValidFrom: DateOnly, Value }`. Lookup "value at date D" = the entry with the
greatest `ValidFrom <= D`. Shared helper `Versioned<T>.At(DateOnly)` in Core.

## §2. Properties

### Property
| Field | Type | Notes |
|---|---|---|
| Name | string | "Apartment Riga", "Cottage" |
| Kind | PropertyKind | House, Apartment, Cottage, Garage, Land, Other |
| Address | string? | |
| Notes | string? | |
| SortOrder | int | |
| IsArchived | bool | sold / no longer tracked; hidden by default, history kept |

### PropertyParameter (time-versioned)
| Field | Type | Notes |
|---|---|---|
| PropertyId | Guid | |
| Kind | PropertyParameterKind | TotalArea, HeatedArea, DeclaredResidents, LandArea, CadastralValueBuilding, CadastralValueLand, Custom |
| ValidFrom | DateOnly | |
| Value | decimal | |
| Note | string? | |

Residents/area change over time and drive calculations (waste by residents, maintenance by area).

## §3. Suppliers and contracts

### Category (user-defined payment category)
| Field | Type | Notes |
|---|---|---|
| SystemKey | ServiceKind? | set for built-in categories (seeded one per `ServiceKind`), null for user-created |
| Name | string? | user name; null for a built-in category means "use the localized name of SystemKey" |
| Color | string | `#RRGGBB`, used in charts |
| Icon | string? | icon key (later) |
| SortOrder | int | |
| IsArchived | bool | hidden from pickers, history kept |
| Notes | string? | |

`ServiceKind` stays as the **technical** kind (what the service is, how it behaves: meters, units).
`Category` is what the **user** sees and what reports group by. Examples of user categories: Kindergarten,
Insurance, Security, Parking. A non-utility payment (kindergarten) is a contract with `Service = Other`,
`BillingMode = Fixed` or `Actual` without meters, and its own category; bills are entered manually.
Built-in categories can be renamed (Name set) and recoloured, but not deleted.

### PaymentDetails (owned value on Supplier and Contract)
| Field | Type | Notes |
|---|---|---|
| RecipientName | string? | |
| RecipientRegistrationNumber | string? | |
| Iban | string? | validated (ISO 13616 checksum), stored without spaces, displayed in groups of 4 |
| Bic | string? | |
| BankName | string? | |
| PurposeTemplate | string? | e.g. `Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}` |

Effective details for a contract = contract value per field, else supplier value per field.
Purpose placeholders: `{CustomerNumber}`, `{ContractNumber}`, `{BillNumber}`, `{Period:<format>}`
(default format `MM.yyyy`), `{Amount}` (invariant `0.00`), `{PropertyName}`. Unknown placeholders are kept as
typed. The UI offers "copy IBAN", "copy purpose", "copy all" (decision 2026-10-20).

### Supplier
| Field | Type | Notes |
|---|---|---|
| Name | string | |
| RegistrationNumber | string? | |
| Phone, Email | string? | |
| Notes | string? | |
| PaymentDetails | PaymentDetails? | see above |
| Links | list of **Link** | see below |

### Link (owned by Supplier or Contract)
| Field | Type | Notes |
|---|---|---|
| Kind | LinkKind | Website, CustomerPortal, SubmitReadings, Tariffs, PayOnline, Other |
| Title | string? | defaults to localized Kind name |
| Url | string | must be absolute http/https; opened via `ILauncherService` |

Contract links override/add to supplier links (e.g. a contract-specific portal).

### Contract (a customer account with a supplier for one property)
| Field | Type | Notes |
|---|---|---|
| PropertyId | Guid | |
| SupplierId | Guid | |
| Service | ServiceKind | technical kind, see list below |
| CategoryId | Guid | user-facing category for reports (default: built-in category of `Service`) |
| PaymentDetails | PaymentDetails? | overrides supplier details per field |
| Title | string | "Water", "Electricity (dual-tariff)" |
| CustomerNumber | string? | "abonenta numurs" |
| ContractNumber | string? | |
| PortalLogin | string? | optional reminder of the user name; passwords are never stored (the browser's password manager fills them) |
| ValidFrom / ValidTo | DateOnly / DateOnly? | |
| Currency | Currency | default EUR |
| BillingTiming | BillingTiming | `InArrears` (default) / `InAdvance` (prepaid services) |
| BillingMode | BillingMode | `Actual` (by consumption), `Equalized` (fixed monthly + annual settlement), `Fixed` (subscription), `Assessed` (taxes: annual amount + installments) |
| ReadingDayFrom / ReadingDayTo | int? | window for submitting readings (e.g. 20–26) |
| BillDay | int? | usual day the bill arrives |
| PaymentDueDay | int? | usual due day |
| Links | list of Link | |
| Notes | string? | |
| IsArchived | bool | |

**ServiceKind**: ColdWater, HotWater, Sewerage, WaterHeating, Heating, Electricity, Gas, Waste,
Maintenance (apartment management fee), Internet, Tv, InternetTv, PropertyTaxBuilding, PropertyTaxLand,
Insurance, Other. A contract may produce several charge lines of different kinds (water + sewerage).

### ContractMeter (many-to-many)
| Field | Type | Notes |
|---|---|---|
| ContractId | Guid | |
| MeterId | Guid | |
| ValidFrom / ValidTo | DateOnly / DateOnly? | |

Example: hot water meter → water contract (volume for water + sewerage) and heat contract (water heating).

## §4. Meters

### Meter
| Field | Type | Notes |
|---|---|---|
| PropertyId | Guid | |
| Resource | MeterResource | ColdWater, HotWater, Electricity, Gas, Heat, Other |
| Unit | MeterUnit | M3, KWh, MWh, Gcal |
| SerialNumber | string? | |
| Location | string? | "bathroom", "kitchen", "stairwell box" |
| Digits | int? | integer digits on the dial, for rollover (99999 → 00012) |
| InstalledOn | DateOnly | |
| RemovedOn | DateOnly? | set when replaced |
| ReplacedMeterId | Guid? | previous meter this one replaced |
| VerifiedOn | DateOnly? | last metrological verification |
| VerificationIntervalMonths | int? | e.g. 72 for water meters |
| NextVerificationDue | DateOnly? | explicit override; otherwise VerifiedOn/InstalledOn + interval |
| Notes | string? | |

### MeterRegister
| Field | Type | Notes |
|---|---|---|
| MeterId | Guid | |
| Kind | RegisterKind | Total, Day, Night, Peak, Export |
| Direction | EnergyDirection | Import (consumption), Export (fed into grid) |
| Name | string? | display override |

A simple meter has one `Total/Import` register. Dual-tariff: `Day` + `Night`. Solar: + `Export`.

### MeterReading
| Field | Type | Notes |
|---|---|---|
| RegisterId | Guid | |
| Date | DateOnly | date the reading was taken |
| Value | decimal | dial value as shown |
| Kind | ReadingKind | Regular, Initial (new meter), Final (meter removed), Control (supplier check), Estimated (by supplier) |
| SubmittedOn | DateOnly? | when it was reported to the supplier |
| PhotoAttachmentId | Guid? | photo taken on the phone |
| Note | string? | |

### Consumption rules (Core: `ConsumptionCalculator`)
1. Consumption between two readings of the same register = `later − earlier`.
2. If `later < earlier` and `Digits` is set → rollover: `later + 10^Digits − earlier`; flagged as rollover.
   If `Digits` is not set → validation error (negative consumption).
3. **Meter replacement:** consumption over a period that spans a replacement =
   (old meter: Final − last reading before period end/previous) + (new meter: first reading − Initial).
   Readings of different meters are never subtracted from each other.
4. Anomaly warning when consumption > `k ×` average of last 12 periods (k configurable, default 3)
   or when a reading date is earlier than the previous one.
5. Assigning readings to a billing period: for each contract, the reading for period `P` is the reading
   taken within the contract's reading window of `P` (or the last one on/before the end of `P` if there
   is no window). If there is no reading inside the window: first a **late** reading (after the window end,
   before the next period's window start; earliest such), else the latest reading before the window start;
   both add `ReadingOutsideWindow`. The same reading is never used as both start and end of a period.
   The user can override the chosen reading for a charge.
6. A replacement meter does **not** inherit `Digits` or `SerialNumber`; the replacement UI pre-fills them
   from the old meter for the user to confirm (decision 2026-10-19).

## §5. Tariffs and VAT

### VatRate (catalog, time-versioned)
| Field | Type | Notes |
|---|---|---|
| Code | string | `LV-STANDARD`, `LV-REDUCED-HEAT`, `ZERO`, custom |
| Name | string | |
| Versions | list `{ ValidFrom, Percent }` | e.g. LV-STANDARD: 18%, 21% (2009), 22% (2011), 21% (2012-07) |

Seeded with Latvian history; user-editable. Each tariff component references a VAT code, so a change of
the national rate is entered once. A bill or component can override with an explicit percent.

### TariffComponent (belongs to a contract)
| Field | Type | Notes |
|---|---|---|
| ContractId | Guid | |
| Name | string | "Water supply", "Sewerage", "Distribution", "Energy", "Heating of hot water" |
| ChargeKind | ServiceKind | technical kind of the line |
| CategoryId | Guid? | report category of this line; null = contract's category (e.g. sewerage line of a water contract can have its own category) |
| Notes | string? | |
| Basis | ChargeBasis | see below |
| RegisterKinds | set of RegisterKind? | for `PerUnit`: which registers are counted (e.g. Day only) |
| MeterIds | set of Guid? | for `PerUnit`: which meters (default: all contract meters); a selected meter includes its replacements (meter chain) |
| VatCode | string | |
| Rates | list of **TariffRate** | time-versioned |
| SortOrder | int | |

**ChargeBasis**:
- `PerUnit` — consumption × price (water m³, kWh, gas m³, hot water m³ × heating price).
- `PerArea` — area parameter × price (maintenance fee per m²).
- `PerResident` — declared residents × price (waste).
- `FixedMonthly` — fixed amount per month (internet, TV, meter fee, subscription fee).
- `FromBill` — no own calculation; amount taken from the supplier bill (heating by building heat meter).
- `NetExport` — export register × price, negative line (solar net billing).

### TariffRate (time-versioned)
| Field | Type | Notes |
|---|---|---|
| ValidFrom | DateOnly | |
| Price | decimal | per unit / m² / resident / month |
| PriceIncludesVat | bool | supplier often quotes gross prices |
| Currency | Currency | |
| Tiers | list `{ UpToQuantity, Price }`? | stepped price; threshold accumulates per `TierPeriod` |
| TierPeriod | Month / Year? | e.g. the 2013–2014 "1200 kWh per year" electricity rule |
| Notes | string? | e.g. "price from supplier letter 2025-10" |

### Charge calculation (Core: `ChargeCalculator`)
For contract C and period P, per component:
1. Determine quantity (consumption / area / residents / 1 / none for FromBill).
2. Pick the rate valid on the period's reference date (first day of P; components with readings use the
   end reading date). Price changes inside a period are **not** split in v1 (documented limitation).
3. Net = quantity × price (if price includes VAT: gross = quantity × price, net = gross / (1+vat)).
4. VAT = Round(net × vat%), Gross = net + VAT. Each line rounded to 0.01.
5. Result: `CalculatedCharge` with lines `{ ComponentId, Quantity, UnitPrice, Net, VatPercent, Vat, Gross }`
   and total. Calculated on demand; a snapshot is stored when a bill for P is entered (so later tariff
   edits do not silently change history), and can be recalculated explicitly.

## §6. Bills, payments, balance

### Amount fields — one naming everywhere
Every money amount that has VAT uses the same four fields (code / UI label):
`Net` "Amount excl. VAT" · `VatPercent` "VAT %" · `Vat` "VAT" · `Gross` "Total".
Used by `ChargeLine`, `Bill`, `BillLine`, `BillTemplate`. The user enters **any two** of Net / VatPercent /
Gross and the third and `Vat` are derived (`MoneyMath.FromNet/FromGross`); entering Gross only (no VAT info)
is allowed (`Net`, `VatPercent`, `Vat` stay null). Invariant when all set: `Net + Vat = Gross`.

### Bill (supplier invoice or any expense)
| Field | Type | Notes |
|---|---|---|
| PropertyId | Guid | always set (copied from the contract when there is one) |
| CategoryId | Guid | always set (default: contract's category) |
| ContractId | Guid? | null = **one-off expense** without a contract (repair, purchase…) |
| SupplierId | Guid? | optional for one-off expenses |
| Period | YearMonth | the month the bill **belongs to** = month of service/readings (bill for October readings, issued in November → 2026-10) |
| ServiceFrom / ServiceTo | DateOnly? | for non-monthly bills (quarter, year, prepaid internet) — reports spread the amount over these months |
| Kind | BillKind | Regular, Equalized, Settlement (annual reconciliation), Correction, TaxInstallment, OneOff |
| Title | string? | free description, required for one-off expenses |
| Number | string? | invoice number |
| IssuedOn | DateOnly? | |
| DueOn | DateOnly? | |
| Net / VatPercent / Vat / Gross | decimal? / … / decimal | **Gross required** (variant B); the rest optional, see "Amount fields" |
| Currency | Currency | |
| Lines | list of **BillLine** | optional detail |
| CustomFields | list of **CustomFieldValue** | optional, see below |
| ConsumptionOnBill | decimal? | quantity the supplier billed (to compare with metered, e.g. water losses) |
| CalculatedSnapshot | CalculatedCharge? | app's calculation at entry time |
| TemplateId | Guid? | the BillTemplate it was created from |
| AttachmentIds | list of Guid | PDF / photo of the bill |
| Notes | string? | |

**BillLine**: `Description`, `Quantity?`, `Unit?`, `UnitPrice?`, `Net?`, `VatPercent?`, `Vat?`, `Gross`,
`CategoryId?` (line in another category), `CustomFields`, `Note?`. Sum of line `Gross` should equal bill
`Gross` (warning, not error — suppliers round differently).

**CustomFieldDefinition** (on a Contract or a BillTemplate, so the same fields reappear every month):
`Name`, `Kind` (Text, Number, Money, Date), `AppliesTo` (Bill, BillLine), `SortOrder`.
**CustomFieldValue**: `DefinitionId?`, `Name` (kept even if the definition is removed), `Kind`, `Value`
(string, invariant format). Example: "Meter reading on bill", "Losses m³", "Supplier's reference".
*(Confirmed by the owner, 2026-10-20.)* Number and Money fields can be summed in reports.

Derived (not stored): `Difference = Gross − CalculatedSnapshot.TotalGross`; `OpenAmount = Gross − allocations`;
`IsOverdue = OpenAmount > 0 && DueOn < today`; `PerResident = Gross / DeclaredResidents` (shown for waste).

### Payment
| Field | Type | Notes |
|---|---|---|
| ContractId | Guid? | null = generic payment split across contracts |
| PaidOn | DateOnly | |
| Amount | decimal | |
| Currency | Currency | |
| Method | PaymentMethod | BankTransfer, Card, Cash, DirectDebit, Other |
| CreatedByQuickAction | bool | set by the "Paid" button (see PAYMENT_TYPES) |
| Reference | string? | |
| PropertyId | Guid? | for payments without contract |
| Allocations | list `{ BillId, Amount, Note? }` | |
| Notes | string? | |

- Unallocated part of a payment = **prepayment**, increases the contract balance.
- One payment may cover several bills (also of different contracts/properties).
- **Paid in advance** (internet, TV): the bill for November is issued and paid in October; it belongs to
  `Period = 2026-11`. Before November it is shown as "prepaid". Contract field `BillingTiming` decides the
  default period of new bills.
- Auto-allocation: oldest open bills first; user can edit.
- "Paid" quick action: creates a payment for the bill's open amount, dated today, allocated to that bill;
  `Payment.CreatedByQuickAction` (bool) marks it so "Cancel payment" can delete it as a whole. Undo = soft delete.

### Contract balance (derived)
`Balance(C, date) = Σ payments(C, ≤ date) − Σ bills(C, ≤ date)`. Shown on the contract and dashboard.

### Equalized billing (BillingMode = Equalized)
### EqualizedPlan (time-versioned)
`{ ContractId, ValidFrom, ValidTo?, MonthlyAmount }`. Monthly bills of kind `Equalized` equal the plan
amount. The app keeps calculating the *actual* charge from readings; the dashboard shows the running
difference "actual − paid by plan" since the last settlement, i.e. the expected settlement amount.
A `Settlement` bill (positive = surcharge, negative = refund) closes the year.

## §7. Property taxes
A contract with `Service = PropertyTaxBuilding/PropertyTaxLand`, `BillingMode = Assessed`, supplier =
municipality. The yearly assessment is entered as several `Bill`s of kind `TaxInstallment` with their due
dates (usually quarterly, can be paid in full in the first installment). Cadastral values are optional
`PropertyParameter`s for information.

## §8. Templates

### ContractTemplate — how a contract is **set up**
Reusable set of tariff components (names, basis, VAT code, current rates), meter roles, links, payment
details and custom field definitions for a supplier, e.g. "Water — water + sewerage, 2 cold + 2 hot meters".
Creating a contract from a template copies everything; the user then adjusts. Export/import as JSON.

### BillTemplate — how a **payment** is created (monthly, other periods, or one-off)
| Field | Type | Notes |
|---|---|---|
| Name | string | "Internet + TV", "Home insurance", "Property tax", "Chimney cleaning" |
| PropertyId | Guid | |
| CategoryId | Guid | |
| ContractId | Guid? | optional |
| SupplierId | Guid? | optional |
| Title | string? | default bill title |
| Net / VatPercent / Gross | decimal? | default amount (any two, see "Amount fields") |
| Lines | list of BillLine | default lines (amounts may be empty — user fills them) |
| CustomFields | list of CustomFieldDefinition | |
| Schedule | Schedule | see below |
| Timing | BillingTiming | `InArrears` (bill for a past period) / `InAdvance` (prepaid) |
| DueDayOffset | int? | due date = issue date + N days |
| RemindDaysBefore | int? | reminder before due date |
| IsActive | bool | |
| Notes | string? | |

**Schedule**: `Kind` = `Once` (manual use only, e.g. "buy water meter"), `Monthly`, `EveryNMonths`
(`N`, e.g. 3 = quarterly, 6 = half-year), `Yearly`; `StartMonth` (YearMonth), `EndMonth?`, `DayOfMonth?`
(issue day). A scheduled template creates a **draft** bill for each due period (the user confirms or
edits the amount); `Once` templates appear in a "New payment from template" picker.

Examples: internet + TV = Monthly, InAdvance, fixed Gross; insurance = Yearly in March; property tax =
EveryNMonths 3 (or Yearly with 4 installments — see §7); chimney cleaning = Yearly; meter replacement = Once.

## §9. Reminders
### ReminderRule
| Field | Type | Notes |
|---|---|---|
| Kind | ReminderKind | SubmitReadings, PayBill, MeterVerification, TaxInstallment |
| ContractId / MeterId | Guid? | |
| DayOfMonth / LeadDays | int? | |
| IsEnabled | bool | |

Derived reminders: readings window (from contract), bill due dates (from bills), verification due
(from meters, e.g. 60 days ahead). Scheduled locally via `INotificationService`; also shown on the
dashboard as "To do".

## §10. Attachments and settings
- `Attachment { FileName, ContentType, SizeBytes, Sha256, OwnerKind, OwnerId }`.
- `AppSetting { Key, Value }` — language, theme, backup options, sync state (`DeviceId`, last applied
  change sequence per device).

## §11. Reports (derived, phase 6)
- Monthly costs by service, stacked/grouped bar chart, one or several properties.
- Consumption by resource (m³, kWh) over time; year-over-year comparison.
- Unit cost (€/m³, €/kWh) over time.
- Calculated vs billed difference over time (e.g. water losses).
- Balances and overdue bills.
- Export of any report table to Excel.

## §12. Mapping of the owner's Excel (for the import phase)
The workbook is an **example, not the source of truth** — it contains workarounds. When in doubt, ask the
owner (decision 2026-10-20). Combined apartment + cottage electricity history is imported to the apartment
with a note; from now on they are two properties with two contracts.
One sheet per supplier, one row per month. Observed patterns to support:
- Water: cold + hot readings, price × m³ (later water + sewerage), VAT changing over years, bill total
  in a separate column, difference column, meter replacement noted in comments.
- Heat: building heating amount from bill + hot water heating = hot-water m³ × price + 12% VAT,
  using the **same hot water meter** as the water sheet.
- Waste, maintenance, internet, TV: fixed monthly amounts (net + VAT or total only).
- Electricity: single register, price per kWh, a period with a yearly 1200 kWh tier, later totals only.
- Propane gas: readings + price, equalized monthly amount.
- Amounts before 2014-01 are in LVL.
- Free-text notes in side columns (tariff changes, overpayments, meter numbers) → imported as notes.
