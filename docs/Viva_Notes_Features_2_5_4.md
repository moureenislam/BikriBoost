# Viva notes: Features 2, 5 and 4

How each feature works, where its code is, and how to trace one request through MVC.
The rules were ported from our earlier Laravel version of BikriBoost and rewritten in C#.

**The MVC pattern every feature follows**

| Layer | What it does | Where |
|---|---|---|
| **Model** | Entities (tables) + a *service* class that holds the business rules | `Models/Entities/`, `Models/Services/` |
| **View** | Razor page that only displays a view model; no queries or calculations | `Views/<Controller>/` |
| **Controller** | Checks the role, calls **one** service method, then redirects or returns a view | `Controllers/` |

A broken rule throws `RuleViolationException("message")` in the service. The controller catches it (in `AppController.TryRunAsync`) and shows the message in red. Nothing is saved.

---

## Feature 2 · In-chat order parsing & quick draft

**What:** paste a customer's Messenger/WhatsApp message → the app fills in a draft order → the moderator checks it and saves.

**Trace:** `GET /Orders/Capture` → `OrdersController.Capture(rawText)` → `OrderTextParser.ParseAsync` → `OrderService.SaveDraftAsync` → view `Orders/Draft` → `POST /Orders/Create` → `OrderService.CreateAsync`.

**Rules (no AI, fixed rules):**
- Phone: `01[3-9]` + 8 digits; `+88`, spaces, dashes and Bangla digits (০–৯) are accepted (`PhoneNumber`).
- Labelled lines (`Name:`, `Phone:`, `ঠিকানা:` …) win; otherwise name, address and items are guessed from free text after removing greetings ("Assalamualaikum apu") and intros ("ami", "i am").
- District and thana are looked up in English and Bangla (`DistrictDetector`, using the Districts/Thanas tables).
- Items match by SKU, full product name, or "enough words / unique last word"; the quantity is read next to the item ("2 ta", "x2").
- **Never guessed:** if a product has several sizes and the message doesn't say which, it's listed under "Fix before saving".
- Saving checks everything again (valid phone, district, at least one item, max 100 per line, no duplicate lines) and creates the order as **Pending**.

**Tests:** `OrderTextParserTests`: labelled message, Bangla digits, greetings plus two items, unclear size flagged, invalid phone, shared word matches nothing, and phone boundaries (operator digits 3 and 9, 10 and 12 digits).

---

## Feature 5 · Strict state-machine order lifecycle

**What:** an order moves `Pending → Confirmed → Dispatched → Delivered → Settled`, or to `Cancelled` / `Returned`.

**Trace (Confirm):** button on `Orders/Details` → `POST /Orders/Confirm/{id}` → `OrdersController.Confirm` → `OrderLifecycleService.ConfirmAsync` → `AppDbContext.SaveChangesAsync` (one transaction).

**Rules:**
- Only the moves in the `Allowed` table; anything else is refused.
- Confirm **reserves** stock (`ProductVariant.Reserved`, a `StockReservation` row, a `StockEvent`); Cancel **releases** it; Dispatch takes it out of `Stock`.
- Dispatch needs a courier and a consignment ID (unique per courier), and is refused while an advance is still due (Feature 4).
- Only the **Owner** marks Delivered or Returned. The rule is in the service, not just the button.
- **Settled** is never offered to a user; it is reserved for the courier settlement feature (a teammate's part).
- Every move writes an `OrderStatusHistory` row (shown as the timeline).
- **Two moderators, last unit:** `ProductVariant.Version` is a concurrency token. The second save fails with `DbUpdateConcurrencyException`, and the user sees "try again".

**Tests:** `OrderLifecycleTests`: reserve/release, illegal move, short stock, exactly-available stock (boundary), owner-only, dispatch needs consignment ID, Settled never offered, the concurrency test.

---

## Feature 4 · MFS payment & TrxID verification

**What:** the shop asks for an advance; the moderator enters the customer's bKash/Nagad transaction ID; the owner confirms the money arrived; only then can the parcel be dispatched.

**Trace:** "Ask for advance" on `Orders/Details` → `POST /Payments/RequestAdvance` → `PaymentVerificationService.RequestAdvanceAsync`. TrxID form → `POST /Payments/Submit` → `SubmitAsync`. Queue: `GET /Payments/Review`; confirm: `POST /Payments/Confirm/{id}` (Owner only) → `ConfirmAsync`.

**Rules:**
- An advance can be asked for only before dispatch, from **Tk 1 up to the order total**. The order is then **locked** for dispatch.
- A TrxID can be used **once on the whole platform** per provider: checked in code *and* by the unique index `(Provider, TrxId)`. If two people submit at the same moment, the index stops the second.
- Format: bKash exactly 10 letters/digits; Nagad 8–12. Case doesn't matter (stored upper case).
- The amount must equal the advance due **exactly** (Tk 0.01 short is refused).
- One waiting claim per order. Only the Owner confirms or rejects (role attribute **and** service check).
- Confirming adds to `AdvancePaid`; once the advance is covered the order **unlocks**, and the cash the courier collects drops by the advance.

**Tests:** `PaymentVerificationTests`: asking locks dispatch until confirmed, advance limits (0, negative, total + 1, exactly the total), same TrxID from another shop in lower case refused, one poisha short refused vs exact accepted (boundary), malformed IDs, owner-only confirm unlocks the order, no advance due, one waiting claim.

---

## 5-minute demo script

1. Sign in as **Owner** (click the Owner demo button, then Sign in).
2. **New order from chat** → "Example: free text + Bangla digits" → **Read the message** → show the filled draft → **Save** (F2).
3. On the order: **Ask for advance** Tk 130 → the order shows *Locked until advance is paid* (F4).
4. **Confirm** → show the timeline and reserved stock (F5). Try **Dispatch** → refused because the advance is due.
5. Submit a TrxID with the **wrong amount** → refused; with the right amount → **Payments** → **Money arrived** → order unlocks (F4).
6. **Dispatch** (consignment ID e.g. `SF-1001`) → **Mark delivered** (F5). The timeline shows every step.
7. Sign in as **Moderator** → the Delivered/Returned buttons are not offered, and the Payments page has no confirm buttons (security).

## Known limitations (be honest in the viva)

- There is no automatic rule for *when* to ask for an advance yet; the shop decides. A fake-order risk score (a teammate's feature) can set it automatically later.
- Returned stock isn't restocked; that waits for QC inspection (Feature 11).
- Delivery charge is a simple default (Dhaka Tk 70, elsewhere Tk 130) that the moderator can edit.
