# Payment types — agreed behaviour

Status: agreed with the owner on 2026-10-20.
This file explains **what the user does** for each kind of payment; field-level details are in `DATA_MODEL.md`.

## General rules
- A bill belongs to the month of service / readings, not the month it is issued
  (readings 25–28 October → bill arrives in November → `Period = 2026-10`).
- Prepaid services (internet, TV) belong to the month they are for (paid in October for November →
  `Period = 2026-11`, contract `BillingTiming = InAdvance`).
- Amounts everywhere use the same fields: Amount excl. VAT / VAT % / VAT / Total. The user enters any two,
  the rest is derived; Total alone is allowed.
- Every record has a free-text comment.
- A contract may have **any number of meters** of the same resource (e.g. 2 cold + 2 hot water meters);
  consumption is summed over all linked meters.
- The owner's Excel is an example only, not the source of truth.

## Catalogue

| Type | Calculation | Set up once | Enter each period | App computes |
|---|---|---|---|---|
| Water + sewerage | (all cold + hot m³) × (water + sewerage price) + VAT | meters (1..n cold, 1..n hot), 2 components, prices, VAT, reading window | readings; bill total | consumption, expected charge, difference (losses) |
| Hot water heating + heating | hot m³ × heating price + VAT; heating = from bill | same hot meters linked to the heat contract | bill total | heating part = bill − water heating |
| Electricity | kWh × (distribution + energy) + VAT; day/night; tiers | meter + registers, components, prices | readings; bill | consumption per register, expected charge |
| Electricity, equalized (current) | fixed monthly amount, annual settlement | equalized plan (amount from date) | readings; bill (= plan) | actual charge in background, expected settlement |
| Solar panels | consumption − export (credit line) | Export register | readings of both registers | export credit |
| Gas (propane), equalized | m³ × price; fixed monthly amount | meter, price, plan | readings; bill | as electricity |
| Waste | **from bill** (house total ÷ residents, amount varies) | residents with dates (for info) | bill total | cost per resident |
| Apartment maintenance | area × price, **VAT included** in price | area with dates, price incl. VAT | bill total | expected charge, difference |
| Internet / TV | fixed monthly, **paid in advance** | bill template: Monthly, InAdvance, amount | confirm draft | draft bill every month, "prepaid" status |
| Property tax (building / land) | yearly amount in installments | contract with municipality | once a year: amount + due dates | reminders, remaining amount |
| Other recurring (insurance, chimney cleaning…) | none (bill only) | bill template: Yearly / every N months | confirm draft, adjust amount | drafts, reminders |
| One-off expense (repair, meter purchase…) | none | optionally a "Once" template | title, category, property, amounts | reports, balances |

## Templates
1. **Contract template** — how a contract is set up (components, VAT, meter roles, links, payment details,
   custom fields).
2. **Bill template** — how a payment is created: monthly, every N months, yearly, or once; in advance or in
   arrears; default amounts and lines; reminder.
3. **Payment purpose template** — text for the internet bank, e.g. `Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}`.

## Custom fields (confirmed)
Named fields of type Text / Number / Money / Date, defined on a contract or bill template (for the whole
bill or for each line), so they reappear every month. Examples: "Losses m³", "Reading on supplier's bill",
"Supplier reference". Removing a field definition keeps old values. Number/Money fields can be summed in reports.
