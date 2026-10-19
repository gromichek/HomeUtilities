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

### Supplier
| Field | Type | Notes |
|---|---|---|
| Name | string | |
| RegistrationNumber | string? | |
| Phone, Email | string? | |
| Notes | string? | |
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
| Service | ServiceKind | see list below |
| Title | string | "Water", "Electricity (dual-tariff)" |
| CustomerNumber | string? | "abonenta numurs" |
| ContractNumber | string? | |
| PortalLogin | string? | optional reminder of the user name; passwords are never stored (the browser's password manager fills them) |
| ValidFrom / ValidTo | DateOnly / DateOnly? | |
| Currency | Currency | default EUR |
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
   is no window). The user can override the chosen reading for a charge.

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
| ChargeKind | ServiceKind | which service this line counts toward in reports |
| Basis | ChargeBasis | see below |
| RegisterKinds | set of RegisterKind? | for `PerUnit`: which registers are counted (e.g. Day only) |
| MeterIds | set of Guid? | for `PerUnit`: which meters (default: all contract meters) |
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

### Bill (supplier invoice)
| Field | Type | Notes |
|---|---|---|
| ContractId | Guid | |
| Period | YearMonth | |
| Kind | BillKind | Regular, Equalized, Settlement (annual reconciliation), Correction, TaxInstallment |
| Number | string? | invoice number |
| IssuedOn | DateOnly? | |
| DueOn | DateOnly? | |
| Total | decimal | **required** — variant B |
| Currency | Currency | |
| Lines | list of **BillLine**? | optional detail: Description, Quantity?, Amount, VatPercent? |
| ConsumptionOnBill | decimal? | quantity the supplier billed (to compare with metered, e.g. water losses) |
| CalculatedSnapshot | CalculatedCharge? | app's calculation at entry time |
| AttachmentIds | list of Guid | PDF / photo of the bill |
| Note | string? | |

Derived (not stored): `Difference = Total − CalculatedSnapshot.Total`; `OpenAmount = Total − allocations`;
`IsOverdue = OpenAmount > 0 && DueOn < today`.

### Payment
| Field | Type | Notes |
|---|---|---|
| ContractId | Guid? | null = generic payment split across contracts |
| PaidOn | DateOnly | |
| Amount | decimal | |
| Currency | Currency | |
| Method | PaymentMethod | BankTransfer, Card, Cash, DirectDebit, Other |
| Reference | string? | |
| Allocations | list `{ BillId, Amount }` | |
| Note | string? | |

- Unallocated part of a payment = **prepayment**, increases the contract balance.
- One payment may cover several bills (also of different contracts/properties — e.g. one electricity
  payment covering apartment and cottage).
- Auto-allocation: oldest open bills first; user can edit.

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
### ContractTemplate
A reusable set of tariff components (names, basis, VAT code, current rates) and links for a supplier,
e.g. "Water utility — water + sewerage". Creating a contract from a template copies the components.
Templates can be exported/imported as JSON to share between properties.

### RecurringBillRule
For `Fixed` contracts: generates a draft bill each month with the current fixed amount
(internet, TV) — the user only confirms.

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
