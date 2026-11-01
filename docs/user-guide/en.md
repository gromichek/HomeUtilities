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
| **Bill template** | How a payment *without a meter* looks and when it comes (internet, insurance, chimney cleaning, a yearly fee). The app creates a **draft** bill for each due month from it. See [Bill templates and drafts](#bill-templates-and-drafts). | "Home insurance": yearly in March, 120,00 € |
| **Payment** | Money you paid. It is allocated to a bill, so the bill is *Paid*; what is not allocated stays on the contract as prepayment. | 03.11.2026: 46.02 € by bank transfer |

**Order of setting things up:** properties → suppliers → contracts (in the property) → meters (in the property,
linked to contracts) → readings → bills. Then open **Overview** first every day: it shows what to do next. Enter the **tariff** (prices) of a contract when you want the app to calculate what
a bill should be and to compare your bills with it. You can add a supplier directly from the contract editor ("＋ New supplier…").

## 2. Screens and common behaviour

**Menu:** Overview, Properties, Suppliers, Readings, Charges, Bills, Payments, Reports, Help, Settings. On a phone the menu opens
with ☰; on a wide window it is always visible.

**Help:** this guide is built into the app and works offline, in the language of the app. Open it from the menu
item **Help**. Screens such as Overview, Properties, Suppliers, Readings, Charges, Bills, Payments, Reports, Settings (and each of its sections), Categories, VAT rates and the contract, meter and tariff component
editors have a **?** button in the toolbar that opens the guide at the matching section; **Back** returns to the screen.

**Layout:** every screen has **one content area**. A list fills it, and tapping a record (or **New …**) opens its editor as a
**separate page** — on a phone, a tablet and a Windows window alike. The editor always has the ✓ **Save** button at the top and
**Back** to leave; there is no Save / Cancel row at the bottom and no editor next to the list. On a wide window the list shows
its cards in two or three columns and the editor puts its cards and fields side by side instead of stretching them across the
screen. A window lower than about 480 dp (a phone held sideways) counts as a phone: **＋ New …** is then the round button at the
bottom right. *Settings → About* shows the current layout (phone / tablet / desktop).

**Saving and leaving**
- ✓ / **Save** saves and **keeps you in the editor**. Only **Back** leaves.
- Leaving with unsaved changes asks **Save changes?** — *Save*, *Don't save* or *Cancel* (stay where you are).
  This also applies when you pick another item in the menu, use the Android back gesture, or close the Windows window.
  If *Save* finds a mistake in the form, you stay and see the red message.
- A menu item always opens the **main screen of its section** (the list), never a half-finished editor you left earlier.
- On Android the **system Back** (button or gesture) does exactly what the **Back** arrow in the app does: it closes a dialog,
  leaves an editor (asking **Save changes?** if needed) and goes up one page. On the main screen of a section it takes you to
  the **Overview**; on the Overview it sends the app to the background.
  After you close an editor, the list shows the same place again and the record you opened is **highlighted**.
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

## Overview

Menu **Overview** is the start screen: it opens first and answers two questions — *what do I have to do now?* and *where does
my money stand?* It reads the records you keep elsewhere (contracts, meters, readings, bills, payments) and adds nothing of
its own, so it is only as good as the data behind it. Pull down to refresh on a phone; the screen also refreshes every time you
come back to it, for example after entering readings or a bill.

With more than one property a **Property** filter at the top limits all three cards to one property (or *All properties*);
the app remembers your choice. On a phone the cards are one below the other; on a tablet or a wide window **To do** is on the
left and **Money** and **This year** on the right. **?** opens this section.

### To do

One row per task, ordered by urgency: overdue first (red), then today, then coming up (amber), then information. Tap a row to
open the matching screen; **Back** returns to the Overview and the row is gone if you have done the task.

| Row | When it appears | When it disappears | Tap / button |
|---|---|---|---|
| **Submit readings: Water, Heating** | A contract's *reading window* (set on the contract, e.g. 25–28) is open, opens within the next 3 days, or ended less than 4 days ago — and none of the contract's meters has a reading dated inside that window. Contracts with the same window share one row. | You save a reading of the meter inside the window, or 3 days after the window ended. | Opens **Readings** of that property for today. |
| **Pay {contract} for {month}** (a bill, and **Pay Property tax …** for a tax installment) | An open bill whose due date is within the next 7 days, is today, or has passed. The amount is what is still open. | The bill is paid in full. | Tap opens the bill. **Paid** (the ✓ button) marks the open amount as paid today and shows **Bill marked as paid · Undo**; **Undo** takes it back and the row returns. |
| **Bill for {month} from {contract} not entered yet** | A contract with a *bill day* has no bill for its expected month yet: the **previous** month for a contract billed in arrears, the **next** month for a prepaid one — and today is on or after the bill day (in the month after the period, or in the month before it when prepaid). Tax contracts are not checked. | You enter a bill for that contract and month. | **Enter bill** opens a new bill with the contract and the month filled in. |
| **Draft bills to confirm: 3** | One or more **draft bills** created from your bill templates wait for you (one row per property). Information only. | You confirm or delete the drafts. | Opens **Bills** of that property, filtered to **Drafts**. |
| **Verify meter …** | A meter in use has its verification due within 60 days, or it is overdue. | The verification date of the meter is moved forward. | Opens the meter. |
| **Expected settlement: {contract}** | An equalized contract (see "Equalized payments") expects a settlement of 1.00 € or more, either way (surcharge or refund). Information only. | The settlement bill is entered, or the amount falls below 1.00 €. | Opens the settlement page. |

The thresholds (3 days, 7 days, 60 days, 1.00 €) are fixed in this version. If there is nothing to do you see
**Nothing to do — all readings submitted and bills paid.**

### Money

For each property: **Open bills** (everything still unpaid, as on the Bills screen with the status *Open*), **Overdue** (the
part past its due date), **Prepayments** (money you paid that no bill uses yet, as on the Payments screen) and the **balance** of
all the property's contracts — **Debt …** in red or **Paid in advance …** in green, the same figure as the *Balance* card of each
contract added up. Equalized contracts add a line **Expected settlement, {contract}: surcharge / refund …**. Tap a line to open
the Bills screen (filtered to open or overdue bills), the Payments screen, or the settlement page.

### This year

For each property the gross total of the bills of this year, counted by the bill's month from January up to the current month,
next to the same months of last year: *2026: 1,245.30 € · 2025 same period: 1,180.10 € (+5.5 %)*. A bill for a later month
(for example internet paid in advance) is counted when its month comes. Tap to open the Bills screen for that year.
Charts on this screen come later.

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
  from / to, archived. **Valid from** is the day the contract starts: [Charges](#10-charges) and the [reports](#15-reports)
  treat the months before it as outside the contract. If you keep older bills (or are about to import them), set it to the
  first day of the history.
- **Account:** customer number, contract number, portal login (no password).
- **Billing:**
  - *Billing mode* — **By consumption** (meters), **Fixed fee**, **Equalized payments** (the same amount every month,
    settled once a year), **Tax (annual amount)**.
  - *Billing timing* — **In arrears** (the bill comes after the month of service: water, electricity) or
    **In advance** (paid before the month: internet, TV).
  - Usual bill day, payment due day, currency.
  - *VAT is calculated on the bill total* (on by default) — most suppliers add up the net amounts of all lines with the
    same VAT rate and calculate VAT once on that sum. Switch it off only if your supplier rounds the VAT of every line
    and adds the rounded values (a difference of a cent or two). See [Charges](#10-charges).
- **Reading submission window:** the days of the month when the supplier accepts readings, e.g. 25–28. A window
  like 28–3 continues into the next month. The Readings screen uses it to show "open now / opens in N days".
  The switch **Remind me about readings** (on by default) under the days decides whether the [reminders](#reminders)
  announce the opening of this window; switch it off for a contract whose readings you never submit by a date.
- **Payment details / Links:** leave a field empty to use the supplier's value. "Effective payment details" shows
  the result, including the payment purpose filled with this contract's numbers.
- **Tariff:** the priced components of this contract (see [Tariffs](#9-tariffs)). Available after the contract is
  saved. Tap a row to edit it; the arrows change the order.
- **Custom fields:** extra fields (text, number, amount, date) that will appear when you enter a bill or a bill line
  for this contract.
- **Bills:** the last six bills of the contract with the month, the total and the status (*Unpaid*, *Paid ✓*…). Tap a
  row to open the bill, **All bills** opens the Bills screen for this property with the contract highlighted, ＋ enters a
  new bill for this contract. Available after the contract is saved. See [Bills](#11-bills).
- **Equalized plan** (billing mode *Equalized payments*): the monthly amount the supplier bills, one row per version, and
  the *Expected settlement* with **Show settlement**. See [Equalized payments](#13-equalized-payments).
- **Tax by year** (billing mode *Tax (annual amount)*): the yearly tax with its installments. See
  [Property tax](#14-property-tax).
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
  water and the heating contract. Each linked contract shows the dates of the link (*from 01.05.2004*, and *to …* if it
  ended). **The link decides which readings belong to the contract:** charges and reports (consumption by contract, unit
  cost, calculated vs billed) count a meter's readings only inside the link. A new link therefore starts on the later of the
  meter's *installed on* date and the contract's *Valid from* — not today — so the whole history of an old meter is counted.
  Tap a linked contract to open **Contract link dates**: *Valid from* and, if the meter stopped serving the contract, an end
  date. The app refuses a start before the meter was installed, an end before the start, and dates that overlap another link
  of the same meter and contract; a start before the contract's own start is allowed (with a warning), but charges only count
  the months from the contract's start. The dates are saved when you press *Save* in the dialog; the meter page stays open.
  Switching a contract off ends the link yesterday; switching it on again starts a new link the day after the old one ended.
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
final readings belong to a meter replacement and cannot be deleted. A long history (twenty years of monthly readings) opens
quickly: each register shows its newest 30 readings first, and **Show more (N left)** adds the next 30.

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
prices (you enter the prices afterwards): *Water + sewerage*, *Heating as one sum*, *Hot water + heating
(distributed)*, *Electricity*, *Area-based fee*, *Per resident*, *Fixed monthly*.

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
| **From the supplier's bill** | no own calculation; the amount is taken from the bill | Heating as one lump sum |
| **Quantity from the bill** | the supplier tells the quantity on the bill, you type it when you enter the bill; the price comes from the tariff | Heat 0.210 MWh × 70.00 € |
| **Net export (credit)** | energy fed into the grid (Export register), counted as a credit | Solar panels |

- **What is counted:** for consumption-based components, switch the **meters** and **registers** the component
  counts. All meters on means "all meters of the contract, also those linked later". A hot-water meter that feeds
  both the water and the heating contract can be counted by "Water heating" only. After a meter replacement the new
  meter is counted automatically, so you do not need to edit the tariff. For *By area* and *Per resident* pick
  the **parameter** of the property (total area, heated area, residents, or your own); the line "now: 52.3 m²" shows its
  current value — change it in the property (Parameters card).
- **Quantity from the bill:** for this basis pick the **unit** of the quantity (m³, kWh, MWh or Gcal) and enter the
  prices as usual. The quantity itself is typed in the bill (see [Bills](#11-bills)); until it is typed the line is
  *incomplete*. Use it when the supplier distributes a building meter among the flats by its own formula, which the app
  cannot repeat — for example the heat for heating and circulation in MWh and the flat's share of the building's
  hot-water difference in m³. The app does not distribute building meters itself; if you want to keep the building
  meter values, put them in a custom field of the contract. Suppliers often print the quantity rounded to three
  decimals but bill the unrounded one, so quantity × price can differ from the bill by a few cents (0,233 MWh ×
  73,76 € = 17,19 €, the bill says 17,22 €). For that case the bill has an optional field *Amount on the bill
  (excl. VAT)* next to each quantity — see [Bills](#11-bills).
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

**Which heating preset?** *Heating as one sum* (hot-water heating by your meters plus one lump sum *From the bill*) is
for a supplier whose bill you cannot reproduce — the app then cannot check the total. *Hot water + heating
(distributed)* is for a bill that lists quantities and prices: **Hot water** by your hot-water meters, **Heat for
heating/circulation** and **Hot water difference** with the quantities *from the bill*, all with the reduced VAT rate.
Enter the prices from the bill; when the supplier changes a price, add a new price with its date. With the quantities
typed from the bill, the app reproduces the supplier's total, so the comparison shows a difference of 0,00.

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

**Equalized contracts.** The card of a contract with equalized payments has one more line under the bill:
*Plan 40,00 € · actual 35,20 € · since last settlement +9,50 €* — the plan amount of the month, what the readings cost
and how much the settlement is expected to be so far (see [Equalized payments](#13-equalized-payments)).

**VAT on the total.** Suppliers do not round VAT line by line: they add the net amounts of all lines with the same VAT
rate and calculate VAT once on that sum. The app does the same (the contract switch *VAT is calculated on the bill
total* is on by default). The lines still show their own VAT for information, and under the lines of the card you see
one row per rate: *VAT 12 % of 39,18 € = 4,70 €*. Example (invented): hot water 24,00 + heat 14,70 + hot-water
difference 0,48 = 39,18 net; VAT 12 % = 4,7016 → 4,70; total 43,88 €. When all lines of a rate are priced including VAT,
the VAT is split out of their sum instead. With the switch off, the rounded VAT of each line is added up.

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
*2 residents*, *1 month*; for a quantity from the bill *0,210 MWh (from bill 10.2026)* or *quantity from the bill* while it
is missing), the unit price (*× 1,41 € excl. VAT* or *incl. VAT*, as the supplier quotes it), the net
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
| *Enter the quantity of «…» from the supplier's bill* | A *Quantity from the bill* line has no quantity yet. | Enter the bill of the month (**Enter bill**) and type the quantity in the card *Quantities from the bill*. |
| *«…»: the amount on the bill does not match quantity × price — the price may have changed.* | The *Amount on the bill (excl. VAT)* typed for a *Quantity from the bill* line is not what the quantity and the tariff price give (more than rounding of the printed quantity explains). | Check the amount and the quantity in the bill, and the price in the tariff; if the supplier changed the price, add the new price with its *Valid from* date. The line still uses the amount from the bill. |
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

**The list.** At the top: filters **Property** (all or one), **Period**, **Category** (*All categories* or one
category, shown with its colour dot) and **Status** (*All*, *Open*, *Overdue*, *Paid*, *Drafts*); the filters are remembered until you close the app.
The filters are the **header of the list**: they scroll away with the cards, so the cards get the whole screen. On a phone
the filter block is collapsed to **one summary line** (for example *All properties · 2026 · All categories · All · Billed 78,13 € · Paid 78,13 €*);
tap it, or the arrow at its right, to open the fields (**Show filters** / **Hide filters**). On a tablet and on a wide window the
fields are open and sit in two or three columns. Your choice (open or collapsed) is remembered until you close the app. **Period** is a
drop-down like the other filters with three choices: *All years*, *Year* and *Month*; with *Year* or *Month* the ‹ › buttons step to the previous or next year or month
(a bill belongs to the month of its billing period), and the totals below follow the chosen period. Below them the totals of the
list: *Billed · Paid · Open*. Each row shows the month, the contract (or the title of an expense), the supplier, the
category colour, the **total**, the **status badge** and — when the app has a calculation to compare with — the
difference, for example *+2,15 € (+3,4 %)*: **red** when the bill is more than 5 % above the calculation, **green**
when it is more than 5 % below, grey otherwise. Tap a row to open the bill. **New bill** (＋ on a phone) starts a new one. A list longer than 100 rows (all years of a long history)
shows the first 100; **Show more (N left)** below it adds the next 100 — the totals always count the whole list. When you come from a
contract with **All bills**, the contract's bills have a light tint; as soon as you tap a bill the tint disappears and only the bill you
opened has the selection border.
The **Category** filter is what a number in the [Costs report](#costs) leads to: tap a cell of a report grouped by category and
the list shows only the bills of that category, that property and that month.

**Status badges** are never typed by hand — they follow the payments of the bill:

| Badge | Meaning |
|---|---|
| *Unpaid* (grey) | Nothing paid yet. |
| *Partially paid · open 26,02 €* (yellow) | Part of the total is paid. |
| *Overdue* (red) | Something is open and the due date has passed. |
| *Paid ✓* (green) | Everything is paid. |
| *Prepaid ✓* (green) | Paid, and the month of the bill has not started yet (internet paid in October for November). |
| *Draft* (muted row) | Created from a [bill template](#bill-templates-and-drafts) and not confirmed yet. It has a **Confirm** button instead of **Paid**, and it counts nowhere (not in the totals, balances, reports or the Overview) until you confirm it. *Draft · enter the amount* means the template has no amount. |

**Which month does a bill belong to?** To the month of the **service**, not to the month it was issued: readings of
25–28 October, bill arrives in November → the bill is for **October**. Services paid **in advance** (internet, TV; set
on the contract as *In advance*) belong to the month they are for: paid in October for November → **November**. The
editor suggests the right month for the contract and you can change it with ‹ › or by tapping the month.

### Entering a bill

**New bill** asks first what it is for: **For a contract** (choose the property, then the contract) or **One-off
expense** (a repair, a purchase — no contract; it needs a *Title* and a *Category*). **From template…** (only when you
have an active [bill template](#bill-templates-and-drafts)) fills the new bill from a template instead — property, contract,
category, supplier, title, amounts, lines and custom fields — and sets the month and the issue and due dates; you only
check it, change the month with ‹ › if needed, and save. The cards of the editor:

1. **General** — period, kind (regular, equalized payment, settlement, correction, tax installment), title, bill
   number, *Issued on*, *Due on* (suggested from the contract's payment due day), the service period if one bill covers
   several months, category and supplier (both taken from the contract; change them if this bill differs). Once saved,
   the contract cannot be changed.
2. **Amounts** — *Amount excl. VAT*, *VAT %*, *VAT* and *Total*. **Fill in any two** of the amount excl. VAT, the VAT % and
   the total; the app calculates the rest (the two you changed last count). The *Total* alone is enough. The VAT % starts
   with the one of the contract's tariff.
3. **Quantities from the bill** (only for contracts whose tariff has *Quantity from the bill* components, for example
   heat in MWh) — one field per component with its unit. Type what the supplier printed on the bill. The calculation
   below uses the numbers at once; they are saved with the bill and with its snapshot, and the *Charges* screen uses
   them for that month ("from bill 10.2026"). Without them the calculation is incomplete.
   Under each quantity there is an optional field **Amount on the bill (excl. VAT)**. Leave it empty normally: the
   line is then quantity × price. Enter it when the supplier prints the quantity rounded (for example 0,233 MWh) but
   bills the exact one, so the line amount on the bill (17,22 €) differs from quantity × price (17,19 €) by a few
   cents: the calculation then uses your amount for that line, and the *Difference* to the bill stays at 0,00. If the
   price already includes VAT, type the line's total including VAT. If the amount is further from quantity × price
   than the rounding of the quantity can explain, the card shows *The amount on the bill does not match quantity ×
   price — the price may have changed.* — then check the price in the tariff. An amount needs its quantity.
4. **Comparison with the calculation** (contracts with a tariff) — the calculated total for the month and the
   **difference** to your bill. *Quantity on the bill* is what the supplier billed in m³; next to the metered quantity it
   shows **Water losses / difference**, e.g. billed 12,500 m³, metered 12,150 m³ → 0,350 m³. **Show charges** opens the
   charges of that month. When you save, the calculation of that moment is kept with the bill (a *snapshot*), so later
   price changes do not rewrite history. If the calculation has changed since, the card says *The calculation changed
   since the bill was entered (snapshot 45,10 € → now 46,02 €)* and **Update snapshot** takes the new one (save the bill
   afterwards). The comparison always uses the snapshot.
   For a bill of an **equalized** contract the card compares the bill with the **plan** (the monthly amount), not with the
   calculation, and shows the calculation as information: *Actual by readings: 35,20 €*. A new bill of such a contract
   starts as kind *Equalized payment* with the *Total* already filled in with the plan amount of its month (change it if
   the supplier billed something else). See [Equalized payments](#13-equalized-payments).
5. **Lines** (folded by default) — optional detail rows of the bill: description, quantity, unit, unit price, the
   amounts (any two, as above), a category of their own and a note. If the lines do not add up to the total you see a
   warning, not an error — suppliers round differently.
6. **Custom fields** — the fields you defined on the contract for bills (for example *Supplier reference*).
7. **Bill document** — attach the PDF or a photo of the bill (camera or gallery on a phone, a file on Windows). Tap a
   document to open it in the system viewer. The file is stored when you save the bill.
8. **Payment** — the status, how much is paid and open, and the payments of this bill. **Paid** records the open amount
   at once; **Add payment** opens the [payment editor](#12-payments) for this bill (tap a payment to open it);
   **Use prepayment** appears when the contract has money that pays no bill yet.
9. **Comment.**

**Save as template** (at the end of the form, for a saved bill) opens the template editor with this bill's content, so a
payment you typed once can become a template. A **draft** bill shows a banner *Draft from template «…» — check and confirm*
with a **Confirm** button; **Save** keeps it a draft, **Confirm** saves it as a normal bill (it needs a total above 0). A draft has no
*Payment* card.

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

## Bill templates and drafts

Some payments have no meter and no tariff, but they come again and again: internet and TV, insurance, a yearly chimney
cleaning, a garage fee — or you buy the same thing now and then. Typing each one from scratch is tedious, so you describe it
once as a **bill template**. The app then prepares a **draft bill** for every month the payment is due, and you confirm it with
one tap (or fix the amount first).

**Template or contract with a tariff?** A *contract* with a tariff is for a service the app should **calculate** from meters and
prices (water, heating, electricity). A *template* is for a payment whose amount you simply know or read from the invoice. A
template can also point to a contract (internet with the FiberNet contract) so the bills show up under it.

**Settings → Bill templates** is the list. Open a template to see its cards:

- **General** — *Name*, *Active*, *Property*, *Contract* (optional; only the contracts of the chosen property; choosing one
  takes its category, supplier and billing timing), *Category*, *Supplier* (optional) and the *Title* every bill gets.
- **Schedule** — *How often*: **Once (by hand)**, **Every month**, **Every N months** (3 = quarterly, 6 = twice a year, 2–24)
  or **Every year**; the *First period* (the month the schedule counts from — a yearly template starts in the month the
  payment falls, for example March), an optional *Last period*, and the *Issue day of the month* (1–31; 31 in February
  becomes 28 or 29). *Billing timing*: **In advance** (a prepaid bill is issued in the month **before** its period, as internet paid
  in October for November) or **In arrears** (issued in the month **after** it). *Due after … days* counts from the issue day;
  *Remind … days before* is how long before the due date the [reminder](#reminders) of the bill comes; empty = the general setting. A line **Next drafts** shows the next three periods with
  their issue and due dates, for example *10.2026 (issued 05.11.2026, due 19.11.2026)*.
- **Default amount** — any two of *Amount excl. VAT*, *VAT %* and *Total*, as in a bill. It may stay **empty** when the amount is
  different every time (the chimney sweep): the draft then says *Draft · enter the amount*.
- **Lines** and **Custom fields** — as in a bill. The lines are copied into each bill; the custom fields appear in each bill with an
  empty value.
- **Comment.**

**How drafts appear.** Every time you open **Overview** or **Bills** the app creates the drafts that are due and not made yet:
a monthly template in arrears has a draft for every month up to the **previous** month; one in advance up to the **next** month.
When you save a template whose first period lies in the past, the drafts of those months appear at once. Nothing is created
twice: a draft you **delete** does not come back, and re-opening the app adds nothing new. An inactive template creates no drafts.

**What a draft contains:** the month, property, category, contract and supplier, the title, the amounts and lines of the template,
empty values for the custom fields, and the issue and due dates from the schedule.

**Confirming.** Drafts are listed on **Bills** (muted, badge *Draft*; filter *Status → Drafts*) and on **Overview** (*Draft bills to
confirm: N*, one row per property). **Confirm** on a row makes it a normal bill at once; if the amount is missing, the bill opens
instead so you can type it. In the editor use **Confirm** after you have checked the draft. A confirmed bill behaves like any other:
**Paid**, balances, reports.

**What drafts do not affect.** Until you confirm them, drafts are not counted in the *Billed / Paid / Open* totals, bill status
and *Overdue*, contract balances, payment allocation, the Charges comparison, the reports or the *Money* and *This year* cards of the
Overview, and **Paid** is not offered.

**Once templates.** A template with *Once (by hand)* never creates drafts. Use it for something you buy now and then, for example
*Water meter replacement*: **Bills → New bill → From template…**, pick it, and you get a normal bill (not a draft) filled in for the month
that fits.

**Other ways to make a template.** In the contract editor, the *Bills* card has **＋ Template from this contract** (it takes
the property, category, supplier, timing, custom fields and the amount of the newest bill). In a bill, **Save as template** does the
same from that bill. Both open the template editor: check it and **Save**.

## 12. Payments

Menu **Payments**: the money you actually paid (or got back). The *bill* says what the supplier asked for, the *payment*
says what left your account, and the **allocation** says which bill the money settles. There are two ways to record it:

- **The Paid button** on a bill — one tap records a bank transfer for the open amount, dated today
  (see [Bills](#11-bills)). It is enough for the usual case: one bill, one payment.
- **A real payment** on this screen (or **Add payment** in a bill) — for another date or amount, one transfer that pays
  several bills, money paid before the bill arrived, a partial payment or a refund.

**The list.** At the top: filters **Property** (all or one), **Period** and **Contract**; they are
remembered until you close the app. Like on Bills, they are the header of the list (they scroll away with the cards) and are
collapsed to one summary line on a phone (tap it to open them). **Period** is a drop-down with three choices: *All years*, *Year* and *Month* (by the date of the
payment); with *Year* or *Month* the ‹ › buttons step to the previous or next year or month, and the totals below follow
the chosen period. Below them the totals of the list: *Paid · of it unallocated (prepayment)*. Like the bills, a list longer than 100 rows shows the first 100 and **Show more (N left)** adds the next 100. Each row
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

## 13. Equalized payments

Some suppliers (often electricity or gas) bill **the same amount every month** — the *plan* — and settle the difference to
the real consumption **once a year** with a settlement bill. During the year you do not see how big that settlement will
be. The app shows it: it keeps calculating what your readings cost (as in [Charges](#10-charges)), compares it with what
you were billed, and adds the differences up.

**Set up.** In the contract choose the billing mode **Equalized payments**, save the contract and use the card
**Equalized plan**: ＋ adds a version with *Valid from*, an optional *Valid to* and the *Monthly amount* (the total
with VAT, as on the supplier's letter). When the supplier changes the amount, add a new version from the new month: the
version that was running ends the day before it automatically, so the plans never overlap. Tap a row to change it, the
trash can deletes it. Enter the **tariff** of the contract as well (see [Tariffs](#9-tariffs)) — without prices and
readings the app cannot say what the real consumption costs.

**Switching an existing contract to equalized.** Keep the contract (its bills, readings and history stay), set *Billing mode* to
**Equalized payments** and add the plan from the first equalized month. Earlier months stay billed by consumption: the app decides
month by month, and a month is equalized only if a plan covers it. Before the first plan the bills are regular, the cards on
[Charges](#10-charges) are ordinary ones, an Excel import brings the rows in as regular bills, and the contract stays in the
unit cost and calculated-versus-billed reports for all months; in equalized months they use the tariff charge instead of the advance (see [Reports](#15-reports)).

**Each month.** Enter the bill as usual ([Bills](#11-bills)): the kind is *Equalized payment* and the *Total* is the
plan amount of the month. The bill is compared with the plan; the cost of the readings is shown next to it as information.

**The settlement.** The card shows *Expected settlement: +9,50 € (surcharge) for 06.2026–11.2026*. **Show settlement**
opens the page with one row per month since the last settlement bill (or since the first plan): the plan, what was
**billed**, what the readings cost (**actual**), the **difference** and the running **total**. Example (invented): the plan is
40,00 € for six months = 240,00 € billed; the readings cost 35,20 + 31,10 + 38,40 + 44,90 + 52,30 + 47,60 = 249,50 €;
the expected settlement is 249,50 − 240,00 = **+9,50 €**, and the page says *You will probably pay about 9,50 € extra*.
A negative result is a refund: *You will probably get about 12,00 € back*. A month without complete readings (or prices) is
marked *Readings missing* with a link to the Readings screen and is **not counted** on either side until it is complete;
the page tells you how many months are missing, so a result with missing months is only a rough guide.

When the supplier sends the settlement, press **Enter settlement bill**: a new bill of kind *Settlement (annual reconciliation)* for the current
month opens, with the expected amount as a hint in the empty *Total* field (it is not filled in — type what the supplier
billed; a refund is a negative amount). After you save it, the next settlement period starts with the following month.

## 14. Property tax

Property tax (land, building) is assessed **once a year** and paid in **installments** with fixed due dates. Make a contract
with the municipality, service *Property tax, building* (or *Property tax, land*) and billing mode **Tax (annual amount)**, save it and use the card
**Tax by year**.

**Enter yearly tax** (＋): the *Year* (this year by default), the *Yearly amount* and the *Number of installments* — 1, 2 or
4 (4 by default). The due dates are filled in with the usual Latvian ones — **31.03, 15.05, 15.08, 15.11** (with 2
installments 31.03 and 15.08, with 1 installment 31.03) — and you can change each date. The amount is split evenly; the
remaining cents go to the first installment (101,00 € → 25,25 € × 4; 100,03 € → 25,03 € + 25,00 € × 3). The app creates one
bill per installment, titled for example *Property tax 2026, installment 2 of 4*: its month is the month of the due date,
and its service period is the whole year, so reports can spread the tax over the year.

The card lists every year with the yearly total, the installments (due date, amount, status: *Unpaid*, *Overdue*, *Paid ✓*…),
how much is paid and what remains. Tap an installment to open its bill; pay it with **Paid** or in the payment editor like
any other bill ([Payments](#12-payments)).

**Paying the whole year at once.** **Pay whole year** opens the payment editor with all unpaid installments of that year
already allocated — one payment, one transfer, every installment becomes *Paid ✓*. You can also pay one installment after
the other.

**Entering a year again** (for example the tax was reassessed): the app asks *Installments for 2026 exist. Replace the
unpaid ones?* **Replace** removes the installments that have no payment and creates the new ones; installments that
already have a payment are **kept**, and the rest of the new yearly amount is split over the remaining due dates.

## 15. Reports

**Reports** answers the questions you ask over months and years: what each property costs, how much water, electricity
or heat was used, what one cubic metre or kilowatt-hour really costs, and how far the supplier's bills are from what the
app calculated. The numbers come from the records you already keep — **bills** (what was paid), **readings** (what was
used) and the **tariffs** of the [contracts](#5-contracts) — so a report is only as complete as those are. Every amount
is shown in euro (old amounts in lats are converted at the official rate).

Pick the report in the **Report** field at the top (it opens a list with a one-line explanation of each report: *Costs — what you paid, by month and category*, and so on; the app remembers your choice), set the filters under it, and read the table. Everything below the filters is a **table**:
the period stays on the left while the columns scroll sideways inside the table; the last row is the **total** of the
whole period. Tap a number to see what is behind it: for costs and for unit cost and calculated vs billed the
[Bills](#11-bills) screen opens for that property and month (for costs grouped by category, only the bills of that category are
shown; for a contract, its bills are highlighted), for
consumption the [Readings](#7-readings) screen opens; **Back** returns to the report.

**Filters (the same for all reports)**

The filters are **fields**: each shows its current value, and a tap opens a picker. On a wide window they sit in three
columns, on a tablet in two, on a phone in one; nothing scrolls sideways. The arrow at the right of the filter header
**collapses** the panel into one line that sums up the choices ("This year · All properties · All categories · Category ·
Gross (incl. VAT)") so the table moves up; tap the line or the arrow to open it again. The panel is collapsed on a phone
by default and stays as you left it while the app runs. The exports are in the **⋯** menu at the top (*Export to Excel*,
*Export to CSV*).

- **Period** — the field shows the choice with its months, for example *This year (01.2026–12.2026)*. The dialog offers
  *This year* (January to December), *Last 12 months*, *Last year*, *All* (from your first record to today or your last
  bill) and *Custom…*, which adds a *From* and a *To* field under the options; each opens a calendar where you pick any day of the month you mean (only the month and year count), and the calendar offers the months from your first bill or reading up to today or your last bill. The end cannot be before the start.
  Every choice dialog looks the same: the title on top, the options as rows (a round mark for one choice, a square one
  for several) that you can tap anywhere, and *Cancel* / *OK* at the bottom right; on a phone it fills the screen.
- **Properties** and **Categories** — a dialog with a check box per item (categories with their colour), the buttons
  *All* and *None* at the top and *OK* / *Cancel*. The field shows *All (3)*, the names (*Flat in town, Cottage*) or *4 of 15*.
  At least one item must stay checked, otherwise *OK* is disabled. A property you archived is off at first — check it to
  include its history. Categories apply to the costs report only.
- **Group by**, **Show**, **Rows** — a list to choose from. *Rows*: one row per **month** or per **year**.
- **Year over year** — a switch. Instead of one long list, the twelve months of the year are the rows and every year is its
  own column, so you can compare October 2026 with October 2025 at a glance. Available for costs and consumption.

**Reading a table with several properties.** Each property has its own block of columns, and each property keeps its own **colour** (its name and column headings are tinted, the same colour whichever properties you filter): a **coloured stripe** over the
property name, a **thick vertical line** before the block, every second block lightly shaded, and the block's **Total**
column in bold on a tinted background. The last block (with no property name) is the **grand total** of the row across all
properties. Column names wrap only between words; hover a header (Windows) to see its full name.

### Costs

What each property cost per month or year, from the **bills**. A bill that covers several months (it has a *service
period*) is split over those months by days; the split always adds up to the bill. **One-off expenses** are included, an
*Equalized payment* counts as the amount billed, and a *Settlement* counts in its month — so the totals agree with
the totals on the **Bills** screen for the same property and period (bills are counted in the month they belong to).

- **Group by** — columns per *Category* (default), per *Service* (a one-off expense counts under the service of its
  category), per *Contract* (one-off expenses get their own column) or one column per *Property*.
- **Show** — *Gross (incl. VAT)*, *Net (excl. VAT)* or *VAT*. If a bill has only a total, the net equals the total and the VAT is 0.
- **Categories** — choose which categories to include (see the filters above).
- With several properties each property has a **Total** column; the last column is the total of the row.

### Consumption

How much of each resource was used, from the **readings**: cold and hot water in m³, electricity in kWh (with *Day* and
*Night* columns if the meter has them), heat in MWh. The month is chosen the same way as in [Charges](#10-charges): the
latest reading in the reading window of the contract that bills the meter. A meter that was **replaced** counts as one
meter, so the replacement month shows no dip.

**Group by** decides what the columns are:

- *Resource* (default) — one column per resource: all cold-water meters of a property add up, all hot-water meters add up.
- *Contract* — one column per contract, with the sum of **all meters linked to that contract**. This answers "how much water
  did the whole flat use?": link the two cold and the two hot water meters to the *Water* contract and its column is the
  total water of the flat. Example, February: cold 5.000 + 4.000 m³ and hot 3.000 + 2.000 m³ give **Water = 14.000 m³**.
  If the hot-water meters are also linked to a *Heating* contract, that column shows **5.000 m³** — a meter counts in
  every contract it is linked to, so the columns are not meant to be added up. Only the months in which the link is valid
  count, and registers with different units are never added together: a contract with a kWh and an m³ meter gets one column
  per unit. A contract without linked meters has no column. The column heading is the contract title and the unit.
- *Meter* — one column per meter (a replaced meter and its successor count as one).

If a part of the sum cannot be determined (one meter has no reading for the month), the sum shows **≈** — the figure then
covers only the meters that could be read.

A month in which the consumption cannot be determined — there is no reading for it — shows **≈** and no number; it is
**not zero**. The next month then also covers the gap and carries a **≈**.

### Unit cost

What one unit really costs: the **bill** of the month divided by the **metered quantity** of the contract (for example
46.02 € ÷ 12.150 m³ = 3.79 €/m³). *Gross* includes VAT, *Net* does not. In a month without a bill the calculation is used
and the number carries a **≈**. Only contracts billed by consumption appear (not fixed fees, per-area, per-resident
or tax contracts). **You do not need to enter historic tariffs:** if a contract has no tariff for a month, its linked
meters give the quantity and the number is the bill ÷ the metered quantity (a contract metered in several units gets one
column per unit). In a month an **equalized** plan covers, the monthly payment is only an advance, so the number is what the
readings cost by the tariff (or the plan amount if there is no tariff) and carries a **≈**. A row of a whole year divides the
year's money by the year's quantity.

### Calculated vs billed

For each contract and month: what the app **calculated** (the calculation stored with the bill when you entered it, else
the calculation as it is now), what the supplier **billed**, and the **difference**. *Show* switches between *Difference, €*,
*Difference, %*, *Calculated*, *Billed* and **Quantity difference (losses)** — the quantity printed on the bill minus the
quantity you metered, for example the water the building lost between its main meter and your meters. A positive value
means the supplier billed more than you used. Months without a bill, or where the calculation cannot be made (missing
reading, prices taken from the bill), are marked **≈**. A contract that has meters but **no tariff** is listed too: it
shows what was billed and the quantities, while the calculated amount and the difference stay empty (there is nothing to
calculate with, which is not the same as zero). In a month an **equalized** plan covers, *billed* is the plan and
*calculated* is what the readings cost by the tariff, so the difference is what will go into the settlement (positive: you
paid too little, negative: too much); a *Settlement* bill is shown as billed in its own month.

### Charts

The same numbers can be drawn as **charts**. On a phone or a narrow window the buttons **View: Table / Chart** switch between
the two (your choice is remembered); on a wide window or a tablet in landscape the chart is shown above the table. Tap a bar or
a point to see its period and value under the chart; the legend below lists the colours. The month names on the horizontal axis
follow the app language. For a period longer than 10 years the table keeps its months, but the charts show **one point per
year** (the chart says so), which keeps long periods fast and readable.

- **Costs** — bars per month. When the columns are parts of a whole (grouped by category, service or contract) the bars are
  **stacked**, so the height of a bar is the total of the month; the colours are the colours of your
  [categories](#8-categories). Grouped by property, the properties stand side by side. The same category of two properties
  is one colour with the sum. With **Year over year** every year is one colour and the months January to December are side by side.
- **Consumption** — one line per resource (per contract or per meter, as chosen in **Group by**), and one chart for each unit: one for m³ (cold and hot water), one for kWh, one for MWh.
- **Unit cost** — one line per contract (€ per m³ or kWh), one chart per unit.
- **Calculated vs billed** — the difference as bars around the zero line: above zero the supplier billed more than the app
  calculated, below zero less. *Quantity difference (losses)* is drawn as a line in m³ because it is not money.
- **Other** — a chart shows at most eight series. If there are more (many contracts or categories), the seven largest stay and the
  rest of the money or quantities are added up as *Other* in grey. For unit cost and percentages (which cannot be added up)
  the smaller series are left out of the chart; the table still shows them.
- **≈ points** — numbers that rest on incomplete data (see below) are drawn muted: pale bars with a dashed outline, hollow dots
  on a dashed line. A gap means there is nothing for that month.

### What does ≈ mean?

A number with **≈** rests on incomplete data and is shown in a muted colour: a reading is missing or is only an
estimate, an amount was calculated because there is no bill yet, or a bill was spread over several months. Check the
readings and bills behind it when it matters.

### Export to Excel and CSV

**Export to Excel** saves what you see — the filters in a header block, then the table with the totals — as a file
`HomeUtilities-<report>-<from>-<to>.xlsx` (for example `HomeUtilities-costs-2026-01-2026-12.xlsx`). Numbers are real
numbers (2 decimals for money, 3 for quantities), months are real dates, so you can add them up or make your own charts in
Excel or LibreOffice. **Export to CSV** makes a lightweight text file with `;` between the columns that Latvian and
Russian Excel open directly (numbers with ≈ get a leading ≈ there).

On **Windows** a save dialog opens; if it is not available the file goes to `Documents\HomeUtilities\Reports`. On
**Android** a save dialog opens where you choose a folder (for example Downloads or Drive); if it fails, the share sheet
opens (Drive, e-mail…). The message at the bottom shows where the file went.

## 16. Settings

**Settings** is a **list of sections**. Each row has an icon, the name of the section and its current value (for example
*Language — Русский*, *Reminders — On, 18:30*, *Error log — Entries: 0*). Tap a row to open it; **Back** returns to the list
and the value is updated. The **?** button in the toolbar opens this chapter; inside a section it opens the matching part.

| Row | What it holds |
|---|---|
| **Language** | The language of the app: [Language and theme](#language-and-theme). |
| **Theme** | System / Light / Dark: [Language and theme](#language-and-theme). |
| **Reminders** | [Reminders](#reminders): what, when, planned reminders, a test notification. |
| **Backup** | [Backup](#backup): save a copy of all data, restore from a copy. |
| **Categories** | The categories of contracts and payments, see [Categories](#8-categories). |
| **VAT rates** | The catalog of VAT percentages, see [VAT rates](#vat-rates). |
| **Bill templates** | Payments without a meter, see [Bill templates and drafts](#bill-templates-and-drafts). |
| **Imports** | [Import from Excel](#import-from-excel), the finished imports with *Undo*, the saved profiles, the export and the meter-link repair: [Imports and profiles](#imports-and-profiles). |
| **Error log** | If the app reports an error: [Error log](#error-log). |
| **About** | Version, database file, layout: [About](#about). |

The rows *Categories*, *VAT rates*, *Bill templates* and *Imports* open the same screens as before; the other rows open a page with
the settings of that section. On a wide window the rows are in two or three columns.

### Language and theme

**Language** — *English* or *Русский*; the whole app switches at once and the row shows the new language when you go back.
**Theme** — *System* (follows the phone or Windows), *Light* or *Dark*. Both are stored on this device only.

### Reminders

**Settings → Reminders** (a page with switches, the time of day and the buttons below) makes the app remind you of the same things that are on the Overview [To do](#to-do) list, even when
the app is closed (Android). Each reminder is a notification; tapping it opens the right screen.

- **Submit readings** — on the first day of the reading window of a contract (see [Contracts](#5-contracts)), at the set
  time. Contracts of one property with the same window give one notification. Nothing is sent if the readings of that window
  are already entered, or if the contract has **Remind me about readings** switched off. If the readings are still missing on
  the **last day** of the window, a second notification "Last day to submit readings to …" comes at the same time of day
  (a one-day window has only one); once you enter the readings, it disappears.
- **Bills and tax installments due** — a few days before the due date (the setting *Remind about a bill … days before it is
  due*, 0–14, default 2; a [template](#bill-templates-and-drafts) can have its own number) and again on the due day. Only
  open bills are reminded: a paid bill, a draft or a bill without a due date is not.
- **Meter verification** — 60 days before the verification date and on the date itself.
- **Draft bills to confirm** — when drafts from templates wait for you: on the day they appear and then at most every
  third day (five reminders at most), until you confirm them.

The **time of day** is the same for all reminders (default 09:00) and is used exactly as set, also late in the evening or
early in the morning. Reminders are planned for the next 45 days, at most 60 of them, and are re-planned whenever you open
the app or change readings, bills, payments, templates, contracts or these settings — so a bill you have just paid
no longer reminds you.

**Send a test notification** shows a notification at once, to check that everything works on this device.

**Planned reminders** lists what the app plans right now: the local date and time, what it is about (readings, bill, tax
installment, meter verification, draft bills), the text of the notification and the screen a tap opens. Use it when a
reminder you expected does not arrive: if it is not in the list, nothing is due (or its time has already passed — a reminder
is planned only for a time in the future). Opening the list first hands the current plan to Android, so the list and Android
always compare the same plan. Every row is checked with Android itself: a reminder Android holds for later has no mark, one it
does not hold is marked in red ("not handed to the system — this reminder will not arrive"). Under the count the list says
how many of the planned reminders Android really holds, when the app last handed them over and why: "Handed to the system:
13 of 13. Last update: … (change)". The reason is one of *start*, *app opened again*, *change* (bills, payments, readings…
were saved), *settings*, *language change* or *leaving the app*. While the list is open, changing the time or a
switch updates it at once. For a test, set the bill's due date or the time of day a few minutes ahead and open the list.
Android delivers reminders without waking the phone exactly on time, so a reminder may come a few minutes late (in deep
sleep even later); a reminder that is more than 6 hours late is skipped.

**If a reminder does not arrive.** (1) Open *Planned reminders*: if the row is missing, nothing is due (or its time has
passed). (2) If the row is red, Android does not hold it — open *Settings → Error log*, tap *Copy log* and send the text to
the developer; failures are written there with the reminder and its time. (3) If the row is fine but nothing comes, check
that notifications are allowed for the app: if you switched them off in the Android settings, the *Reminders* page shows a
red warning with the button **Open notification settings**.

**Lock screen.** Reminders pop up like messages and show their text on the lock screen. If you see only "Content hidden", the phone hides the
content of all notifications: in the Android settings, *Notifications → Lock screen notifications*, choose to show the
content.

**Android.** When you switch the reminders on, Android asks for the permission to show notifications; if you refuse, the
switch goes back off — allow notifications for the app in the system settings and try again. The app does nothing in the
background: the notifications are handed to Android in advance, and are shown by the system a little after the set time (Android
may delay them by a few minutes to save battery). A change you save is handed over a couple of seconds later, and at once when
you leave the app, so you can add a bill and close the app right away. After a restart of the phone or an update of the app the
reminders are registered again without opening the app (a reminder missed while the phone was off is shown after the start
if it is not more than 6 hours late). Reminders are stored on each
device separately, so every phone and PC has its own switch.

**Windows.** Notifications of a closed app are not available. The setting says *Notifications are not available on this
device; reminders are shown when the app is opened*: when the app starts, one short message tells you how many things need
attention (overdue, due today or due soon), once a day; the details are on the Overview.

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

**Settings → Backup** holds two buttons, **Save backup…** and **Restore from backup…**, one under the other, and the line with the date of the last backup (also shown on the row in the list).

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

After reinstalling the app: open *Settings → Backup → Restore from backup…*, pick your file, confirm, close the app and open it
again. The backup file contains **all your data and is not encrypted** — keep it somewhere private.

### Import from Excel

If you kept your history in an Excel workbook (one sheet per supplier, one row per month), **Settings → Imports → Import from Excel**
brings it into the app. The workbook is never copied into the app: the wizard only reads it. Nothing changes until you
press *Import*, and a finished import can be undone as a whole.

**Before you start** make a backup (*Settings → Backup → Save backup…*) and create what the import needs: the **property**, the
**contract** (for example *Water*) and its **meters**, linked to the contract. The import adds *readings* to the meters of the
contract and *bills and payments* to the contract; it does not create contracts or meters and does not import prices
(tariffs stay as you entered them). If a meter or contract is missing, the wizard says so; *Open contract* takes you to it.
The file must be an `.xlsx` workbook (not the old `.xls`).

**Three dates must cover the history** — the contract's start, the meter's installation and the **link** between them
(*Meters → Contracts*). Charges and reports count a reading only when all three allow it.

**Set the contract's *Valid from* to the start of the history first.** Charges and reports ignore the months before the
contract's start date. If the sheet has older rows, those rows get a warning (*This period is before the contract's start
date…*) and the preview says *N rows are before the contract's start date 15.10.2026 — set "Valid from" in the contract…*;
the button **Set contract start to 01.01.2014** changes the contract's *Valid from* to the first day of the earliest imported
month (after a confirmation) and plans the import again. The rows are imported either way.

**Check the contract's reading window if the sheet is dated early in the month.** Gas, electricity and heating sheets are often
dated the 1st of the month after the service, and the *previous month* rule puts such a row in the month before. The charges,
however, look for a month's readings inside the contract's reading window; without a window that contains those dates they look
in the wrong month and every amount seems shifted by one row. The preview then adds *N rows have readings dated early in the month
that belong to the previous month, but the reading window of this contract does not contain those dates…* and the button
**Set reading window 28–3** (from the 28th of the service month to two days after the latest early reading day seen, at most the
10th). After a confirmation the contract gets that window and the import is planned again.

**Check the meters' installation dates and link dates too.** A meter's readings count for a contract only inside the link
between them. If a meter was linked to the contract recently (the link starts on that day), the older months would show no
consumption. When the sheet has data before the start of a meter link, the preview adds *N meter links of this contract start
after the earliest imported data (01.01.2014) — the consumption of the earlier months would be missing…*. The same button (or,
when the contract start is fine, **Start meter links at installation**) moves those links to the later of the meter's
installation and the (new) contract start, after a confirmation. A meter installed after the first imported month keeps its own
installation date — enter the real one in the meter, or let the import find the replacement.

The wizard has four steps:

1. **File and sheet** — *Choose Excel file…* and pick the sheet (with its number of rows).
2. **Contract** — choose a **profile** (a remembered column mapping; *New profile* the first time), the **property** and the
   **contract**. The **positions** of the contract are listed: one line for each meter together with the meters that replaced
   it (for example *Cold water · Bathroom*). A reading goes to the meter of that chain that was installed on the reading date,
   so you only need to set up the first meter; the app can add the replacements it finds (see below).
3. **Columns** — the first rows of the sheet are shown with column letters. For every field choose the column that holds
   it; the choices are guessed from the header texts (Russian, Latvian and English words such as *Итого / Kopā / Total*,
   *Оплачено / Apmaksāts / Paid*, *НДС / PVN / VAT*) — **check them**, or press *Guess again* after changing the header row.
   Only the **period** (month) column is required. The period column may hold a date, a month cell, `MM.yyyy`, `yyyy-MM`, or a month name
   in Russian, Latvian or English. A **date** is not always the billing month — see *Dates and periods* below. You can map:
   - for each meter position the **reading column** and, if there is one, the **reading date column** (without it the date of
     the row is used, or the last day of the month when the period cell names a month);
   - the **bill total**, or the amount excl. VAT, the VAT % and the VAT amount (any two of net / VAT % / total are enough). Each
     of the three has a second column, **used when the first is empty in a row** — for example the supplier's real total first
     and your own calculated total as the fallback. The preview tells you when the fallback was used;
   - the **paid marker**: a column and the text that means "paid in full" (default `***`), and optionally a **payment date** column;
   - the **quantity on the bill** and **notes columns** (their text is added to the bill with the column header in front);
   - the **currency**: *LVL before 2014-01, EUR after* (default, for workbooks that go back before the euro), *EUR* or *LVL*;
   - **how the date becomes the period**, a switch **The date of a row marked «paid» is the payment date** and the switch
     **Find meter replacements in the readings** (all explained below).
   Numbers may use a comma or a dot. The header row, the first data row and the last data row can be changed; without a last
   row the import stops at the first empty period.
4. **Preview** — the dry run. For every row you see what will be created (*Reading …*, *Bill …*, *Payment …*) or that the row is
   *Skipped*, and the problems found. The line on top adds up the result, for example *132 readings, 66 bills, 60 payments,
   3 rows skipped, 5 meter replacements*. Filter the list to *Errors* or *Warnings*. **Errors** skip the whole row (for example a period that cannot be
   read, a number that is not a number, or — when *Find meter replacements* is off — a reading lower than the previous one on a
   meter without dial digits). **Warnings** are imported with a note (for example a bill of that month exists
   already, the total is missing so only the readings are imported, net + VAT do not match the total so only the total is kept).
   Grey lines are only information (for example which fallback column was used). A **Period…** button on a row lets you
   correct that row (see below). Proposed **meter replacements** are listed above the rows. Press **Import** to run it.

**Dates and periods.** In many workbooks the date in the first column is not the billing month. The wizard suggests a rule from
the kind of service of the contract; you can change it, and the preview shows the result for every row (*Row 5 · 2004-04*):
- **By the contract's reading window** (water; used when the contract has a reading window): a date inside the window belongs to that
  month, a late date (after the window, before the next one) too. With the window 25–28, *26 October* is October and *1 May* is
  April. If the window runs into the next month (28–3), *1 May* is April as well.
- **Previous month** (heating, electricity): the date is the 1st of the month after the service, so *1 September* is August.
- **Previous month if the day is up to the limit, else the month of the date** (limit 10 by default): *5 September* is August,
  *26 September* is September.
- **Month of the date** and **Next month** (a service paid in advance).
The reading keeps its real date; only the bill gets the period. Sometimes the rule changed along the way (a service became
prepaid in 2023): add **Other rule from a date on** and say from which date which rule applies. For fixed-amount sheets
(maintenance, waste, internet) the date is the day the previous month was paid: choose *Previous month* and switch on **The date of a row
marked «paid» is the payment date** — the payment then gets the date of the row instead of the 1st of the month. If two rows
fall into the same period, the readings of both are imported but the bill and payment only once, from the later row (with a warning).
**Correcting a single row:** in the preview press **Period…** on the row to give it another month, to let its bill cover
several months (for example the first prepaid internet row that pays two months plus a connection fee) and to add a remark
that goes into the bill's notes. Your corrections are remembered in the profile by the date of the row, so importing the same
sheet again keeps them; *Reset* removes one.

**Meter replacements.** A meter replaced during the years shows in the sheet as a reading that suddenly drops (the new meter starts
near 0) or jumps (it started high). With *Find meter replacements* on, the preview lists each of them under **Meter replacements**
with the proposed **replacement date**, the **final reading of the old meter**, the **initial reading of the new meter** and its
serial number. How the values are found:
- an **extra row in the same month** with a much lower or higher value is the new meter's reading on the installation day: its
  date is the replacement date, the value is the initial reading, and the old meter's final reading is the last reading before;
- a drop **in a regular row**: the replacement date is taken from a note next to the row if it contains a date (for example
  *replaced 30.04.2015*), otherwise it is the day after the previous reading. A note such as
  *skaititaja nr.: Auksta - 1210007 rad:398,054 rad:0,072* gives the serial number, the old final (first `rad:`) and the new
  initial (second `rad:`) of the cold (*Auksta*) or hot (*Karsta*) meter;
- without exact values the new meter is assumed to start at 0, and the old meter's final reading is estimated: what the
  «consumption» column of the row says beyond what the new meters have counted is added to the old meters' last reading. Such
  values are marked *estimated*; the final reading is stored as an **Estimated** reading with the note *estimated during import*.
Two readings in one month without a drop or a jump (for example the switch from the 1st to the 26th) are not a replacement.
**Check the proposals against your papers**, type the right numbers and press **Apply corrections**. On import the app creates the new
meters like the replacement wizard does (same resource, unit, place and dial digits, *replaces* the old one), stores the final and
initial readings, ends the old meter's contract links the day before and starts the new links on the replacement date. *Undo
import* removes the new meters again and puts the old ones back into use. Turn the switch off if the sheet has drops that are
not replacements; they are then errors, as before. If you already entered some replacements yourself, the app follows your chain
and proposes only the missing ones.

**A sheet without readings.** The readings of a heating bill may live in the water sheet (the heating bill uses the hot-water
cubic metres). Map no reading columns on that sheet: then only bills and payments are imported (map *Quantity on the bill*
to keep the cubic metres on the bill), and nothing is imported twice.

**What is created.** Readings are *Regular* (the very first reading of an empty register is *Initial*). A bill is created per
row with a total: *Regular*, or *Equalized* for an equalized contract. A `***` in the paid column creates a **payment
equal to the bill total**, allocated to that bill, dated with the payment date column or, without it, the date of the row (when
*the date is the payment date* is on) or the 1st of the month (reference *imported*). Amounts before 2014 are stored in **lats (LVL)** and shown as they are; reports convert them to EUR
at the fixed rate. **Duplicates:** if the contract already has a bill for a month, or the register already has a reading on that
date, that part is skipped — so importing the same sheet twice adds nothing. After a successful import the profile is saved
(or updated) under the name you gave it.

### Imports and profiles

**Settings → Imports** lists the finished imports (date, file, sheet, how many readings, bills, payments and meter replacements) and the saved profiles.

- **Undo import** hides everything that import created — its bills, their payments, readings and the meters it added — in one go,
  puts the replaced meters and their contract links back, and the balances return to what they were. Nothing is erased for good. If you edited some of those records after the import, the app
  asks *Some imported records were edited later. Undo anyway?* before touching anything. After an undo you can import again.
- **Meter links** shows links that start after their meter was installed although the meter has readings before the link
  (typically a link made today for a meter installed years ago), with the date they have now and the date they would get:
  the later of the meter's installation and the contract's *Valid from*. *Start links at installation…* moves them after a
  confirmation; nothing changes before you confirm. The card only lists the first link of a meter and contract.
- A **profile** can be renamed (edit the name and press *Save name*), deleted, or **exported** to a `.json` file and loaded
  again with *Import profile from file* — for example to send it to the person who helps you. The file contains only the
  column numbers and settings (period rules, header texts): **no amounts and no readings**.

### Export like your workbook

The reverse of the import: **Settings → Imports → Export to Excel** saves your data as a workbook shaped like your own — one sheet
per contract (named *supplier + contract title*, at most 31 characters), one row per month — so you can compare what the app
holds with your original sheet, or keep a familiar spreadsheet as an extra copy. The app asks three things: **which contracts**
(a check-box list: *property · supplier contract*), **which period** (all, this year, … or your own range) and **how to show amounts
before 2014** — *as stored* (LVL, the way the bills were entered) or *converted to EUR*. The file is called
`HomeUtilities-export-YYYY-MM-DD.xlsx`.

- **A contract you imported from a sheet** is laid out exactly like that sheet: the same columns and header row, the header texts
  as they were in your file. The date column is written the way the import reads it back — the reading date for sheets dated by
  the day of the reading, the 1st of the next month for sheets where the date means "the month before", the payment date for
  fixed-amount sheets. A bill that is **paid in full** gets the paid mark (and the payment date if the sheet has such a column).
  Because the layout matches the saved profile, the file can be **imported again without choosing columns** (use
  *As stored* for the amounts, so the old LVL amounts are not read as EUR a second time).
- **A contract without an import profile** gets a standard layout: date, one column per meter (the register is added for meters
  with day and night values), consumption, net, VAT %, VAT, calculated total, bill total, difference, paid mark and payment date, notes,
  currency.
- **Meter replacements.** The month of a replacement gets an extra row on the replacement date with the new meter's first reading
  and a note such as *replaced 15.03.2014: cold - CW-20140 reading 110 reading 0* (serial number of the new meter, last reading
  of the old one, first reading of the new one) — the import understands this note, so a re-import finds the replacement again.
- The first sheet **Total** has one row per month and one column per contract, with row and column totals, always in EUR: the
  same numbers as [Reports](#15-reports) → *Costs* grouped by contract.

Values only — no formulas and no charts are written. Numbers are real numbers (2 decimals for money, 3 for readings) and dates
are real dates, so Excel or LibreOffice can sum and sort them.
### Error log

**Settings → Error log**: if the app reports an error, tap **Copy log** and send the text to the developer. The page says how
many entries the log holds. **Clear** empties the log after a confirmation (*Clear the error log? The entries cannot be restored.*).

### About

**Settings → About** shows the version of the app, the path of the database file and the current layout (phone / tablet /
desktop). In test builds it also holds *Fill with sample data* and *Delete all data*.

## 17. Coming soon

Charts on the Overview,
automatic backups and Google Drive sync.
