# T-017: Tariff editor per contract + VAT catalog screen
Phase: 4 · Depends on: T-016a · Size: L

## Goal
Let the owner enter how each contract is priced — tariff components with time-versioned prices, VAT, optional
stepped prices — so the app can calculate the expected charge (T-018). Also a screen to check/edit the VAT catalog
(Latvian defaults were entered from memory and must be verifiable by the owner).

## Context
- `docs/UI_GUIDELINES.md` (all), `docs/DATA_MODEL.md` §5 (VatRate, TariffComponent, TariffRate, tiers, ChargeBasis,
  charge calculation), `docs/PAYMENT_TYPES.md` (catalogue: what each payment type looks like).
- Existing Core: `TariffComponent`, `TariffRate`, `TariffTier`, `TariffComponentValidator`, `TariffPricing`
  (`Slice`, `ResolveVatPercent`), `VatRate`, `VatRateValidator`, `LatvianVatDefaults`, `MoneyMath`,
  `ChargeBasis`, `TierPeriod`; stores `ITariffStore` (`ListByContractAsync`), `IVatRateStore`.
- Contract editor (T-014), meters + contract links (T-015).

## Scope — do

### 1. Tariff card in the contract editor
- Card **Tariff** (after Billing): rows = components ordered by `SortOrder`: name, basis in words, current price
  ("1,41 €/m³ excl. VAT · VAT 21 %", "52,30 m² × 0,6711 €/m² incl. VAT", "22,95 €/month incl. VAT",
  "from bill"), up/down icons to reorder, tap → component editor. `IconAdd` in the header (disabled for an unsaved
  contract). Empty state: "No tariff yet — add the components of the bill (e.g. water supply, sewerage)."
- Quick-start presets (only when the card is empty), one tap creates pre-filled components (prices empty → the user
  must enter them; VAT LV-STANDARD unless stated):
  - **Water + sewerage**: "Water supply" PerUnit, "Sewerage" PerUnit (both all registers of all linked meters).
  - **Water heating + heating**: "Water heating" PerUnit (hot meters only, VAT LV-REDUCED), "Heating" FromBill.
  - **Electricity**: "Energy" PerUnit + "Distribution" PerUnit.
  - **Area-based fee**: PerArea (TotalArea), price incl. VAT.
  - **Per resident**: PerResident (DeclaredResidents).
  - **Fixed monthly**: FixedMonthly, price incl. VAT.
  The presets are code (`TariffPresets` in Core), not data.

### 2. Component editor page (route `TariffComponentEdit`, params `contractId`, `id` optional)
Cards:
1. **General**: Name*, Charge kind (ServiceKind picker, default = contract service), Category override (optional,
   picker with colour dots, "same as contract" by default), Basis* (picker; one-line explanation under it for the
   selected basis), Notes.
2. **What is counted** (visible by basis):
   - PerUnit / NetExport: meters of this contract (switch per linked meter, all on = "all meters" → stored as empty
     `MeterIds`), registers (switches Day/Night/Peak/Total/Export as present on those meters; all import on = empty
     `RegisterKinds`); NetExport shows Export only.
   - PerArea / PerResident: parameter picker (Total area / Heated area / Residents / custom name) + current value of
     the property ("now: 52,3 m²").
   - FixedMonthly / FromBill: nothing.
3. **VAT**: VAT rate picker from the catalog showing the current percent ("Standard 21 %") or "Custom %" with a
   number field (`VatPercentOverride`).
4. **Prices**: rows "from 01.09.2026 — 1,41 €/m³ excl. VAT" (+ "stepped" badge), newest first; tap → rate dialog;
   trash with confirm; `IconAdd`. Rate dialog: Valid from*, Price* (`DecimalInput`, up to 6 decimals), "Price
   includes VAT" switch, Currency (read-only = contract currency), **Stepped price** switch → tier rows (Up to
   quantity, Price; last row "and above") + Tier period (Month/Year), Notes.
5. **Example** (read-only, live): uses the newest rate, `TariffPricing.Slice` + `MoneyMath`: PerUnit "10 m³ →
   14,10 + VAT 2,96 = 17,06 €"; PerArea with the current parameter; FixedMonthly the monthly amount; tiers show each
   slice. FromBill: "Amount is taken from the supplier's bill."
Validation via `TariffComponentValidator` with field paths mapped to rows (`Rates[1].Price`, `Rates[0].Tiers[2]…`).
Save keeps the page open (guidelines §4). Delete component: confirm.

### 3. VAT catalog screen (Settings → "VAT rates")
List of VAT rates: name, code, current percent; editor: Name, Code (read-only for the three seeded ones), versions
list (from date + percent; add/edit/delete dialog), Notes. Banner at the top: "Default Latvian rates — please check
them against your bills." Validation via `VatRateValidator`. Deleting a rate that is used by a component →
blocked with a message.

### 4. Units in texts
Unit word from the basis: PerUnit/NetExport → unit of the first selected meter (m³, kWh, MWh, Gcal), PerArea → m²,
PerResident → "resident"/"жилец", FixedMonthly → "month"/"мес." (resx).

### 5. User guide
Update `docs/user-guide/en.md` and `ru.md`: new section "Tariffs" (what a tariff component is, the bases with one
example each, VAT catalog vs custom %, prices with "valid from", stepped prices, presets) and "VAT rates" under
Settings; extend the contract section with the Tariff card; remove tariffs/VAT from "Coming soon".

## Scope — do NOT
- No charge screen (T-018). No contract templates (T-025).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner can enter for his water contract: Water supply 1,41 + Sewerage 0,86 excl. VAT 21 % from 01.01.2026; for
   heating: water heating 4,56 VAT 12 % (hot meters only) + heating from bill; maintenance 52,3 m² × 0,6711 incl. VAT;
   internet 22,95 incl. VAT — and sees correct examples.
3. VAT catalog visible and editable; nothing crashes on bad input.

## Tests required
- Core: `TariffPresetsTests` (each preset passes the validator once prices are filled).
- ViewModels: `TariffComponentEditViewModelTests` — basis switches visible sections; "all meters on" stores empty
  `MeterIds`; NetExport only Export; parameter shows current value; VAT override vs catalog; rate dialog with tiers
  (last tier must be unlimited → error otherwise); example: 10 m³ × 1,41 excl. 21 % → 14,10 / 2,96 / 17,06; tiers
  {≤100 @ 0,1065, rest @ 0,0818} for 150 kWh → 10,65 + 4,09 = 14,74 excl. VAT; PerArea 52,3 × 0,6711 incl. VAT 0 % →
  35,10; CanLeaveAsync; save keeps page open.
- `ContractEditViewModelTests` — tariff card rows/summary, reorder updates `SortOrder`, presets only when empty,
  add disabled for unsaved contract.
- `VatRatesViewModelTests` — seeded codes read-only, version add/edit, delete blocked when used.
- `InputHardeningTests` extended to component editor, rate dialog, VAT editor.

## Manual test steps for the owner
1. Settings → **VAT rates**: check 21 % / 12 % (reduced) / 0 % and the history against your bills; correct if needed.
2. Water contract → **Tariff** → preset "Water + sewerage" → enter prices 1,41 and 0,86 from 01.01.2026, VAT
   Standard → example "10 m³ → 14,10 + VAT 2,96 = 17,06 €" for water supply.
3. Heating contract → preset "Water heating + heating" → in "Water heating" switch on only the hot meters, price
   4,56, VAT reduced (12 %).
4. Add a second price for water supply from 01.11.2026 → list shows both, newest first.
5. Reorder components with the arrows; Back with changes → "Save changes?". Nonsense input → messages only.
