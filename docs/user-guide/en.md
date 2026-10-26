# Home utilities — user guide

*Русская версия: [ru.md](ru.md).* This guide describes what the app can do **now**. Features that are still being
built are listed at the end.

<!-- Maintained by coding sessions: every task that changes what the user sees updates this file AND ru.md
     in the same PR. Use the exact UI wording from AppResources.resx. Examples must be invented. -->

## 1. How things fit together

The app is built around a few kinds of records. Most confusion comes from not knowing which record holds which
information, so start here.

![How suppliers, properties, contracts, meters, readings, charges, bills and payments relate](img/relations.en.svg)

| Record | What it is | Example |
|---|---|---|
| **Property** | A place you pay for: apartment, house, cottage, garage, land. Holds **parameters** that change over time (total area, declared residents…). | "Flat in town", 52.3 m², 2 residents |
| **Supplier** | A company you pay. Holds website links (portal, pay online, submit readings) and default **payment details** (IBAN, purpose template). One supplier can serve many properties. | "City Water Ltd" |
| **Contract** | One service from one supplier **at one property**: service kind, category, customer/contract number, billing mode and timing, reading window. Can override the supplier's payment details and links. | "Water — Flat in town" with City Water Ltd |
| **Meter** | A physical meter **in a property**, with one or more **registers** (Total, or Day/Night, Export for solar). | Cold water meter in the bathroom |
| **Meter ↔ contract link** | Says which contracts use a meter. One meter can be used by several contracts. | The hot water meter is linked to the **water** contract (water volume) **and** the **heating** contract (water heating). |
| **Reading** | A value of a meter register on a date. Consumption is calculated from readings. | 01.10.2026: 1234.567 m³ |
| **Category** | Your own grouping with a colour for reports (e.g. "Water", "Taxes"). Every contract has one. | "Water", blue |
| **Tariff component** | One priced line of a contract's bill, with prices that change over time and a VAT rate. Several components make the **tariff** of a contract. | "Water supply" 1.41 €/m³ excl. VAT, "Sewerage" 0.86 €/m³ excl. VAT |
| **Charge** | What a contract should cost for a month: the consumption from the readings × the prices of the tariff, with VAT. Calculated on demand, not stored. | Water, October: 5.500 m³ × 1.41 € = 7.76 € + VAT 21 % = 9.39 € |
| **VAT rate** | A named VAT percentage that changes over time; tariff components point to it, so a change of the national rate is entered once. | "Standard rate": 21 % |
| **Bill** | What the supplier actually charged for one month (or any one-off expense without a contract). It is compared with the charge the app calculated. | Water, October: 46.02 €; the app expected 45.10 € |
| **Payment** | Money you paid. It is allocated to a bill, so the bill is *Paid*; what is not allocated stays on the contract as prepayment. | 03.11.2026: 46.02 € by bank transfer |

**Order of setting things up:** properties → suppliers → contracts (in the property) → meters (in the property,
linked to contracts) → readings → bills. Enter the **tariff** (prices) of a contract when you want the app to calculate what
a bill should be and to compare your bills with it. You can add a supplier directly from the contract editor ("＋ New supplier…").

## 2. Screens and common behaviour

**Menu:** Overview, Properties, Suppliers, Readings, Charges, Bills, Payments, Reports, Help, Settings. On a phone the menu opens
with ☰; on a wide window it is always visible.

**Help:** this guide is built into the app and works offline, in the language of the app. Open it from the menu
item **Help**. Screens such as Properties, Suppliers, Readings, Charges, Bills, Payments, Categories, VAT rates and the contract, meter and tariff component
editors have a **?** button in the toolbar that opens the guide at the matching section; **Back** returns to the screen.

**Layout:** on a phone a list opens the editor as a separate page. On a tablet or a wide window the list is on the
left and the editor on the right. Settings shows the current layout (phone / tablet / desktop).

**Saving and leaving**
- ✓ / **Save** saves and **keeps you in the editor**. Only **Back** leaves.
- Leaving with unsaved changes asks **Save changes?** — *Save*, *Don't save* or *Cancel* (stay where you are).
  This also applies when you pick another item in the menu, use the Android back gesture, or close the Windows window.
  If *Save* finds a mistake in the form, you stay and see the red message.
- A menu item always opens the **main screen of its section** (the list), never a half-finished editor you left earlier.
  On a wide window the list opens with the previously selected record and its editor on the right.
- **Delete** hides the record; its history is kept. Many records can instead be **archived** — hidden from lists and
  pickers but still used in history. Use "Show archived" to see them again.
- Fields marked * are required. A wrong value shows a red message under the field; the app does not save until
  it is fixed.
- Numbers accept both `,` and `.` as the decimal separator.

**Short messages.** After *Save* or *Copy* a small bar with **Saved** or **Copied** appears at the bottom of the window for a few
seconds. Some messages have a button, for example **Bill marked as paid · Undo** (it stays a bit longer). The bar does not
block anything — just carry on.

**Child rows** (parameters, links, registers, custom fields…): the card header has ＋ to add; tap a row to edit it
in a dialog; the trash icon deletes it (with confirmation).

## 3. Properties

Menu **Properties** → **New property**.
- **General:** name, kind (apartment, house, cottage, garage, land, other), address, notes, archived.
- **Parameters:** values that tariffs can use — total area, heated area, land area, declared residents, cadastral
  values, or a custom parameter. Each value has a **Valid from** date; when the area or the number of residents
  changes, add a new value instead of overwriting the old one, so older periods are still calculated correctly.
  The row shows "Now: … since …"; the history button shows earlier values.
- **Contracts** and **Meters** cards: the property's contracts and meters (save the property first).

## 4. Suppliers

Menu **Suppliers** → **New supplier**.
- **General:** name, registration number, phone, e-mail, notes. Search by name in the list.
- **Links:** websites with a kind — *Website*, *Customer portal*, *Pay online*, *Submit readings*, *Tariffs*, *Other*.
  The open icon opens the link in your browser. **Passwords are not stored** — let your browser remember them.
- **Payment details:** recipient, registration number, bank, BIC, IBAN and a **payment purpose template**.
  Placeholders in the template are replaced with real data: `{CustomerNumber}`, `{ContractNumber}`, `{BillNumber}`,
  `{Period:MM.yyyy}`, `{Amount}`, `{PropertyName}`. Example: `Customer {CustomerNumber}, {Period:MM.yyyy}` →
  "Customer 100200, 10.2026". Copy buttons put the IBAN, recipient or purpose on the clipboard for your bank app.

## 5. Contracts

Open a property → card **Contracts** → **Add contract**.
- **General:** title, supplier, service kind (water, heating, electricity, waste, internet, tax…), category, valid
  from / to, archived.
- **Account:** customer number, contract number, portal login (no password).
- **Billing:**
  - *Billing mode* — **By consumption** (meters), **Fixed fee**, **Equalized payments** (the same amount every month,
    settled once a year), **Tax (annual amount)**.
  - *Billing timing* — **In arrears** (the bill comes after the month of service: water, electricity) or
    **In advance** (paid before the month: internet, TV).
  - Usual bill day, payment due day, currency.
- **Reading submission window:** the days of the month when the supplier accepts readings, e.g. 25–28. A window
  like 28–3 continues into the next month. The Readings screen uses it to show "open now / opens in N days".
- **Payment details / Links:** leave a field empty to use the supplier's value. "Effective payment details" shows
  the result, including the payment purpose filled with this contract's numbers.
- **Tariff:** the priced components of this contract (see [Tariffs](#9-tariffs)). Available after the contract is
  saved. Tap a row to edit it; the arrows change the order.
- **Custom fields:** extra fields (text, number, amount, date) that will appear when you enter a bill or a bill line
  for this contract.
- **Bills:** the last six bills of the contract with the month, the total and the status (*Unpaid*, *Paid ✓*…). Tap a
  row to open the bill, **All bills** opens the Bills screen for this property with the contract highlighted, ＋ enters a
  new bill for this contract. Available after the contract is saved. See [Bills](#11-bills).
- **Balance:** billed, paid and the result — *Debt*, *Paid in advance* or *Settled* — the money that pays no bill yet
  (*Use for open bills*) and the latest payments with **All payments** and ＋ for a new payment. Available after the
  contract is saved. See [Payments](#12-payments).

## 6. Meters

Open a property → card **Meters** → **Add meter**.
- **General:** resource (cold/hot water, electricity, gas, heat, other), unit (m³, kWh, MWh, Gcal), serial number,
  location, notes, **photo** (camera on Android, image file on Windows).
- **Dial digits:** how many digits the dial has. Needed to recognise a roll-over (99999 → 00000).
- **Registers:** *Total* for ordinary meters; *Day* and *Night* (or the "Dual tariff" switch) for two-rate
  electricity meters; *Export* counts energy fed into the grid (solar panels).
- **Installation and verification:** installed on, last verified, verification interval (water meters usually 72
  months) → the app shows "Next verification" and warns when it is overdue.
- **Contracts:** switch on every contract that uses this meter. A hot water meter is usually linked to both the
  water and the heating contract.
- **Readings:** the latest readings and "All readings". A *Submit readings* link of the contract or supplier is
  shown on the meter card.
- **Replace meter…** — use this when a meter is physically replaced (verification, defect). A wizard asks for the
  date, the final reading of the old meter, the new meter with its initial reading, and the contracts the new meter
  belongs to. The old meter is kept as removed ("Show removed"), so consumption across the change is correct.
  A meter that already has readings cannot be deleted — replace it instead.

## 7. Readings

Menu **Readings**: the fastest way to enter monthly values.
1. Choose the **property** and check the **reading date** (today by default).
2. Every active meter register has a field with the last value ("last: …"). Enter the new values; empty fields are
   skipped. **Save all** saves them together.
3. Warnings appear under a value but do not block saving: *Unusually high*, *Zero consumption*, *Roll-over assumed*,
   *The date is before the last reading*. A value lower than the last one on a meter without dial digits is an
   error.
4. The header shows each contract's reading window ("submit 25–28 — open now").
5. **Submit online** opens the supplier's *Submit readings* link. When you come back, the app asks **Readings
   submitted?** — *Mark submitted* stores the date, so you can see which readings were sent.

Tap a meter to see its reading history; tap a reading to edit it (date, value, kind, note, submitted). Initial and
final readings belong to a meter replacement and cannot be deleted.

## 8. Categories

**Settings → Categories.** Categories group contracts and payments in lists and reports and give them a colour.
Built-in categories can be renamed (leave the name empty to return to the default) and archived but not deleted;
you can add your own.

## 9. Tariffs

A **tariff** tells the app how a contract is priced, so that it can later work out the expected charge for a month.
It consists of **components** — the lines of the supplier's bill, for example "Water supply" and "Sewerage" on a
water bill, or "Energy" and "Distribution" on an electricity bill. Each component has a **basis** (what is
multiplied by the price), a **VAT rate** and one or more **prices**, each valid from a date.

Open a saved contract → card **Tariff**. The card lists the components with their current price, for example
"1.41 €/m³ excl. VAT · VAT 21 %" or "52.30 m² × 0.6711 €/m² incl. VAT". Tap a row to edit it, ＋ adds a component, the
arrows reorder the lines. While the card is empty, **quick-start presets** create ready-made components without
prices (you enter the prices afterwards): *Water + sewerage*, *Water heating + heating*, *Electricity*,
*Area-based fee*, *Per resident*, *Fixed monthly*.

**Component editor**
- **General:** name, *charge kind* (what the line counts toward in reports; the contract's service by default),
  category (empty = the contract's category), basis and notes.
- **Basis** — what the price is multiplied by:

| Basis | Quantity | Example |
|---|---|---|
| **By consumption (meters)** | consumption from the contract's meters | Cold water 10 m³ × 1.41 € |
| **By area** | area of the property (a parameter of the property) | Maintenance 52.3 m² × 0.6711 € |
| **Per resident** | declared residents | Waste 3 residents × 5.40 € |
| **Fixed amount per month** | 1 | Internet 22.95 € a month |
| **From the supplier's bill** | no own calculation; the amount is taken from the bill | Heating by the building heat meter |
| **Net export (credit)** | energy fed into the grid (Export register), counted as a credit | Solar panels |

- **What is counted:** for consumption-based components, switch the **meters** and **registers** the component
  counts. All meters on means "all meters of the contract, also those linked later". A hot-water meter that feeds
  both the water and the heating contract can be counted by "Water heating" only. After a meter replacement the new
  meter is counted automatically, so you do not need to edit the tariff. For *By area* and *Per resident* pick
  the **parameter** of the property (total area, heated area, residents, or your own); the line "now: 52.3 m²" shows its
  current value — change it in the property (Parameters card).
- **VAT:** choose a rate from the catalog (it shows the percent valid today, e.g. "Standard rate 21 %") or
  **Custom %** and type the percent yourself. See [VAT rates](#vat-rates).
- **Prices:** a list of prices, newest first, each with **Valid from**. When a supplier changes a price, **add a new
  price** with the new date instead of editing the old one — earlier months are then still calculated with the old
  price. The price dialog has the price (up to 6 decimals), *Price includes VAT* (suppliers often quote gross
  prices) and the currency of the contract.
- **Stepped price:** switch *Stepped price* on to enter steps, for example "up to 100 kWh: 0.1065 €, above: 0.0818 €".
  The last step is "and above" (no limit). *Tier period* says whether the thresholds count per month or per year.
- **Example:** a live calculation with the newest price, for example "10 m³ → 14.10 + VAT 2.96 = 17.06 €". For a
  stepped price each step is shown on its own line. It helps to check that the numbers you typed are sensible.

Invented example — a water contract: "Water supply" 1.41 and "Sewerage" 0.86, both *By consumption*, VAT *Standard
rate*, valid from 01.01.2026. A heating contract: "Water heating" 4.56, *By consumption*, only the hot-water meters
switched on, VAT *Reduced rate*; "Heating" *From the supplier's bill*.

## 10. Charges

Menu **Charges**: what each contract **should** cost for a month, so you can check the supplier's bill. Nothing is
stored here — the numbers are calculated every time you open the screen from your readings, tariffs and VAT rates, so
they always follow the latest data. The supplier's bill is entered on the [Bills](#11-bills) screen and compared with
these numbers.

**Where the numbers come from:** readings → consumption (end reading − start reading) → × price from the tariff →
net amount → VAT → total. Area-based and per-resident lines use the property's parameters (area, declared residents)
valid on the first day of the month; fixed lines are one month's price; a line *From the supplier's bill* has no
calculation. As on a supplier's invoice, the net amount is rounded to 0.01 first and the VAT is
calculated from that rounded amount (6.500 m³ × 1.41 € = 9.165 → 9.17 €; VAT 21 % = 1.93 €; total 11.10 €).

**Choosing the month.** Pick the **property** and the **month** with ‹ › or by tapping the month. When you open the
screen it shows the **current** month if the reading window of one of the property's contracts has already started
this month (for example the window is 25–28 October: from 25 October the screen shows October), otherwise the
**previous** month. The top of the screen shows the total of all contracts: net, VAT and total, and says
*Incomplete* when some amount could not be calculated.

**One card per contract** that is active in the month, with its category colour, supplier, status (*Complete*,
*Incomplete*, *From the bill*, *No tariff*) and the month's total. Tap the header to expand it (on a phone the cards
start collapsed, on a wide window expanded). A contract without a tariff shows *No tariff — add it in the contract*
with a link to the contract.

**Reading a line.** Each line shows the component, the quantity with its unit (*5,500 m³*, *52,30 m²*,
*2 residents*, *1 month*), the unit price (*× 1,41 € excl. VAT* or *incl. VAT*, as the supplier quotes it), the net
amount, the VAT percent and amount, and the line total. A stepped price shows one line per step. A line *Amount from
the supplier's bill* has no amounts.

**The readings behind a quantity.** Tap a metered line to see, per meter register, the start reading and the end
reading with their dates and the consumption. If the meter was replaced in the month, both meters are shown:
*old meter: 123,000 → final 125,500; new meter: initial 0,000 → 2,000*. Notes flag a roll-over of the dial, an
estimated reading, a reading taken outside the reading window, and unusually high or zero consumption.

**Warnings** are shown under the card, each with a button that opens the place where you fix it. When you come back
(Back) the screen is recalculated and the warning is gone if the problem is fixed.

| Warning | What it means | How to fix |
|---|---|---|
| *No price valid on … for «…»* | The component has no price for that date. | **Open tariff**, add a price with a *Valid from* date on or before it. |
| *«…» has no value on …* | The area or residents used by the line are not entered for that date. | **Open property**, add the parameter value. |
| *Readings missing for …* | The meter has no usable start or end reading. | **Open readings**, enter the missing reading. |
| *No meter is linked for «…»* | The line counts meter consumption, but no meter of the contract fits. | **Open contract**, link a meter. |
| VAT code unknown, currency differs, stepped price on a net export line | The tariff itself is wrong. | **Open tariff** or *Settings → VAT rates*. |
| *The contract is not active in this month* | The contract ended or has not started yet. | Check the contract's dates. |

In the contract editor the tariff card has a **Show charges** button that opens this screen for the contract's
property, scrolled to that contract.

**The bill of the month.** Under the header of each card you see either the bill — *Bill: 46,02 € · +0,92 € · Paid ✓*
(the total, the difference to the calculation, the status); tap it to open the bill — or the button **Enter bill**. It
opens a new bill with the contract and the month already chosen, and the calculated total shown in the empty *Total*
field as a hint (you still type the real amount from the supplier's bill).

## 11. Bills

Menu **Bills**: the invoices you receive from suppliers (and any one-off expense), how far each one is from the app's
calculation, and whether it is paid. A **bill** is a record of its own: the *charge* (see [Charges](#10-charges)) is
what the app expects, the bill is what the supplier asked for, the *payment* is what you paid.

**The list.** At the top: filters **Property** (all or one), **Period** and **Status**
(*All*, *Open*, *Overdue*, *Paid*); the filters are remembered until you close the app. **Period** has three choices:
*All years*, *Year* and *Month*; with *Year* or *Month* the ‹ › buttons step to the previous or next year or month
(a bill belongs to the month of its billing period), and the totals below follow the chosen period. Below them the totals of the
list: *Billed · Paid · Open*. Each row shows the month, the contract (or the title of an expense), the supplier, the
category colour, the **total**, the **status badge** and — when the app has a calculation to compare with — the
difference, for example *+2,15 € (+3,4 %)*: **red** when the bill is more than 5 % above the calculation, **green**
when it is more than 5 % below, grey otherwise. Tap a row to open the bill. **New bill** (＋ on a phone) starts a new one.

**Status badges** are never typed by hand — they follow the payments of the bill:

| Badge | Meaning |
|---|---|
| *Unpaid* (grey) | Nothing paid yet. |
| *Partially paid · open 26,02 €* (yellow) | Part of the total is paid. |
| *Overdue* (red) | Something is open and the due date has passed. |
| *Paid ✓* (green) | Everything is paid. |
| *Prepaid ✓* (green) | Paid, and the month of the bill has not started yet (internet paid in October for November). |

**Which month does a bill belong to?** To the month of the **service**, not to the month it was issued: readings of
25–28 October, bill arrives in November → the bill is for **October**. Services paid **in advance** (internet, TV; set
on the contract as *In advance*) belong to the month they are for: paid in October for November → **November**. The
editor suggests the right month for the contract and you can change it with ‹ › or by tapping the month.

### Entering a bill

**New bill** asks first what it is for: **For a contract** (choose the property, then the contract) or **One-off
expense** (a repair, a purchase — no contract; it needs a *Title* and a *Category*). The cards of the editor:

1. **General** — period, kind (regular, equalized payment, settlement, correction, tax installment), title, bill
   number, *Issued on*, *Due on* (suggested from the contract's payment due day), the service period if one bill covers
   several months, category and supplier (both taken from the contract; change them if this bill differs). Once saved,
   the contract cannot be changed.
2. **Amounts** — *Amount excl. VAT*, *VAT %*, *VAT* and *Total*. **Fill in any two** of the amount excl. VAT, the VAT % and
   the total; the app calculates the rest (the two you changed last count). The *Total* alone is enough. The VAT % starts
   with the one of the contract's tariff.
3. **Comparison with the calculation** (contracts with a tariff) — the calculated total for the month and the
   **difference** to your bill. *Quantity on the bill* is what the supplier billed in m³; next to the metered quantity it
   shows **Water losses / difference**, e.g. billed 12,500 m³, metered 12,150 m³ → 0,350 m³. **Show charges** opens the
   charges of that month. When you save, the calculation of that moment is kept with the bill (a *snapshot*), so later
   price changes do not rewrite history. If the calculation has changed since, the card says *The calculation changed
   since the bill was entered (snapshot 45,10 € → now 46,02 €)* and **Update snapshot** takes the new one (save the bill
   afterwards). The comparison always uses the snapshot.
4. **Lines** (folded by default) — optional detail rows of the bill: description, quantity, unit, unit price, the
   amounts (any two, as above), a category of their own and a note. If the lines do not add up to the total you see a
   warning, not an error — suppliers round differently.
5. **Custom fields** — the fields you defined on the contract for bills (for example *Supplier reference*).
6. **Bill document** — attach the PDF or a photo of the bill (camera or gallery on a phone, a file on Windows). Tap a
   document to open it in the system viewer. The file is stored when you save the bill.
7. **Payment** — the status, how much is paid and open, and the payments of this bill. **Paid** records the open amount
   at once; **Add payment** opens the [payment editor](#12-payments) for this bill (tap a payment to open it);
   **Use prepayment** appears when the contract has money that pays no bill yet.
8. **Comment.**

Saving keeps you in the editor. Deleting a bill asks first; the money you paid for it stays on the contract as
prepayment (a payment made with the **Paid** button for this bill alone is removed together with the bill).

### Paid, Undo and Remove payment

The **Paid** button (on a row of the list and in the *Payment* card) records that you paid the bill: one bank
transfer for the **open amount**, dated **today**, with the payment purpose of the contract as its reference. The bill turns
*Paid ✓* and a bar appears for a few seconds: **Bill marked as paid · Undo**. A wrong tap costs nothing — **Undo**
removes that payment again.

Later, in the *Payment* card of the bill, **Remove payment** takes a payment away (the dialog shows the amount and the
date). A payment made with the **Paid** button for this bill alone is hidden completely; a payment that also covers other
bills only loses this bill's share, and *the rest stays on the contract as prepayment*. Nothing is ever erased — the
history is kept. Payments for another date or amount, or for several bills, are entered on the [Payments](#12-payments)
screen.

## 12. Payments

Menu **Payments**: the money you actually paid (or got back). The *bill* says what the supplier asked for, the *payment*
says what left your account, and the **allocation** says which bill the money settles. There are two ways to record it:

- **The Paid button** on a bill — one tap records a bank transfer for the open amount, dated today
  (see [Bills](#11-bills)). It is enough for the usual case: one bill, one payment.
- **A real payment** on this screen (or **Add payment** in a bill) — for another date or amount, one transfer that pays
  several bills, money paid before the bill arrived, a partial payment or a refund.

**The list.** At the top: filters **Property** (all or one), **Period** and **Contract**; they are
remembered until you close the app. **Period** has three choices: *All years*, *Year* and *Month* (by the date of the
payment); with *Year* or *Month* the ‹ › buttons step to the previous or next year or month, and the totals below follow
the chosen period. Below them the totals of the list: *Paid · of it unallocated (prepayment)*. Each row
shows the date, the amount, the contract (or the property, or *Several contracts*), the method and the bills it pays, for
example *Water 09.2026, Water 10.2026 +1* (two bills are named, then *+N*). A yellow badge **Prepayment 12,30 €** means that
part of the payment pays no bill; a refund is shown in green as *−12,00 € refund*; the small word *quick* marks a
payment made with the **Paid** button. **New payment** (＋ on a phone) starts a new one.

### Entering a payment

1. **General** — *Property* and *Contract* (choose *No contract* for a transfer that covers bills of several
   contracts of the property), *Paid on* (today), *Amount* (a **negative amount is a refund** you received), *Method*
   (bank transfer by default), *Reference* and a comment. **Fill from contract** writes the payment purpose of the
   contract; when exactly one bill is listed below, its month and number are used. Once saved, the contract cannot be
   changed.
2. **Bills paid** — the bills this payment settles. ＋ offers the **open bills** of the contract (of the property when
   there is no contract); the amount of a new row is the open amount of the bill or what is left of the payment,
   whichever is smaller. Tap a row to change its amount, the trash can removes it. **Allocate automatically** spreads
   what is left over the oldest open bills. The line below shows *Allocated 58,32 € · Prepayment 12,30 €*; if the bills
   take more than the payment, it turns red (*Over-allocated by …*) and the payment cannot be saved. A bill never gets more
   than is open on it.

Examples (invented):

- **One transfer for two bills:** 58,32 € without a contract → ＋ *Water 09.2026* (30,00 €) → ＋ *Waste 09.2026*
  (28,32 €). Both bills turn *Paid ✓*. If you **delete** the payment, both become unpaid again.
- **Prepayment:** you pay 70,00 € for a bill of 57,70 €. The payment shows *Prepayment 12,30 €*.
- **Partial payment:** a bill of 46,02 €, you pay 20,00 € → the bill is *Partially paid · open 26,02 €*.
- **Refund:** the manager returns 12,40 € after the yearly settlement. The settlement is a bill with a negative total;
  record the refund as a payment of −12,40 € and allocate it to that bill.

### Prepayment

The part of a payment that pays no bill stays on the contract as a **prepayment**; it raises the balance of the contract.
When the next bill arrives, open it: the card *Payment* offers **Use prepayment 12,30 €**. The money is allocated to the bill
(never more than its open amount, oldest payments first), the bar *Prepayment used · Undo* appears, and **Undo** takes it
back.

### Balance of a contract

The card **Balance** in the contract editor (after the contract is saved) compares what was billed with what was paid, up to
today:

| Result | Meaning |
|---|---|
| *Debt 45,00 €* (red) | The bills are more than the payments: you owe this amount. |
| *Paid in advance 12,30 €* (green) | The payments are more than the bills: the supplier holds your money. |
| *Settled* | Billed and paid are equal. |

Example: three bills of 46,02 € = 138,06 €. Paid 120,00 € → *Debt 18,06 €*; paid 150,00 € → *Paid in advance 11,94 €*.

If some payments have money no bill uses, the card shows *Unallocated payments: 12,30 €* and **Use for open bills**, which
spreads that money over the open bills of the contract, oldest first (with **Undo**). Below you see the latest six
payments (tap one to open it), **All payments** (this screen filtered to the contract) and ＋ (a new payment for the contract).

### Changing and deleting

Tap a payment to change its date, amount, method or bills. A payment made with the **Paid** button is an ordinary payment
here; if you turn it into a partial payment or add other bills, it stops being a "Paid button" payment. **Delete** hides the
payment — *the bills it paid become unpaid again* — and the history is kept.

## 13. Settings

Language (English / Русский), theme (system / light / dark), categories, VAT rates, version, database file location,
and the **Error log**: if the app reports an error, use *Copy log* and send the text to the developer.

### VAT rates

**Settings → VAT rates** is the catalog of VAT percentages that tariff components refer to. It starts with three
entries — *Standard rate*, *Reduced rate* and *Zero / exempt* — with their Latvian history. **These defaults were
entered from memory: please check them against your bills** and correct them if needed. The list shows the percent
valid today.

Open an entry to see its **name**, **code** and the **percentages over time**: each has a *Valid from* date, so a
change of the national rate is a new row, and past months keep using the old percent. Add rows with ＋, tap a row to
edit it. The code of the three default entries cannot be changed and they cannot be deleted; you can add your own
rates (for example for a special case). A rate that is used by a tariff component cannot be deleted — the app tells
you how many components use it. A component can also skip the catalog and use its own *Custom %*.

### Backup

Everything you type (properties, suppliers, contracts, meters, readings) and all attachments and photos live only
inside the app. If you uninstall the app, they are deleted with it. A backup is a single file
(`HomeUtilities-backup-YYYY-MM-DD-HHmm.zip`) that you keep outside the app and can restore later, also on another
device (for example Windows → Android).

- **Save backup…** — a save dialog opens: on Android choose a folder (for example Downloads; Drive if an account is set up),
  on Windows choose a folder. If the dialog fails on Android, the share sheet opens instead (Drive, e-mail…); on Windows (if the dialog is not available, the file goes to
  `Documents\HomeUtilities\Backups` and the message shows the path). The line under the buttons shows when the last
  backup was made on this device.
- **Restore from backup…** — pick a backup file. The app checks it and asks *Restore backup from … ?* Your current
  data will be replaced, but **a copy of it is kept** inside the app (the last three copies). After you confirm, the app
  asks you to close it; open it again and the restored data is there. If you choose a file that is not a backup, or a
  backup made by a newer version of the app, you get a message and nothing changes.

After reinstalling the app: open *Settings → Restore from backup…*, pick your file, confirm, close the app and open it
again. The backup file contains **all your data and is not encrypted** — keep it somewhere private.

## 14. Coming soon

Equalized-payment settlement and property tax installments, overview of what to do, reports and charts,
bill templates, Excel import, reminders, automatic backups and Google Drive sync.
