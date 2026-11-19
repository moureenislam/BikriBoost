# Feature walkthrough

## Section 1: Features 18, 19, 20

### 18: Post approval and visual annotations

Create a draft with a caption, approved asset and optional SKU tags. Submit it for review. As Owner, click the image and add a note. Unresolved notes block approval. Resolve them and approve, or request changes with written feedback. Requested changes return the post to Draft. Approved posts cannot be edited. Approval history and percentage-based image markers are retained.

Implementation: `ApprovalService`, `ApprovalsController`, `Views/Approvals`, `Feature18Tests`.

### 19: Calendar and stock synchronization

Schedule an approved post using Bangladesh time. Drag it to another day or use the scheduling form. A SKU at or below its safety threshold blocks scheduling. The worker rechecks stock and approval when the post is due, placing blocked posts on Hold. Restore stock through the teammate's inventory feature and reschedule. Cancellation returns the post to Approved.

Publishing is a local simulation, with `simulated-` IDs. The database stores schedules; the background worker checks them every 30 seconds while the app runs and catches up after restart. It replaces the proposed Hangfire setup for this local build. The calendar uses local JavaScript, not a CDN.

Implementation: `SchedulingService`, `SimulatedPublisher`, `CalendarController`, `MarketingWorker`, `Views/Calendar`, `calendar.js`, `Feature19Tests`.

### 20: Assets and brand kit validation

As Owner, save a brand kit: palette, RGB tolerance, minimum resolution, fonts and optional logo. Upload PNG/JPEG/WebP images up to 8 MB and 40 megapixels. The resolution must meet the minimum, and at least 60% of sampled opaque pixels must match the palette within the configured distance. Failed uploads remain visible with reasons and cannot be selected for new drafts. Select a previous latest asset to upload a new version. TTF/OTF fonts can also be stored. Recheck existing images after changing the kit.

Checks do not inspect typography inside images or use AI. Files are served through authorized, tenant-filtered routes.

Implementation: `AssetService`, `BrandComplianceChecker`, `AssetsController`, `Views/Assets`, `Feature20Tests`.

## Section 2: Features 21, 22, 23

### 21: Campaign attribution and coupons

Create a campaign, then a fixed or percentage coupon with expiry, minimum order amount and overall/per-phone limits. Generate a tracked link with source, medium and creative tags. Visiting it records a click and sets a protected 30-day attribution cookie. In the campaign screen, apply the coupon/link to an existing pending order. Money and usage counters change in one transaction. Stacking, duplicate redemption and mismatched campaigns are rejected. Local and +880 phone formats are normalized.

Cancelled orders do not automatically release coupon usage; that requires an explicit cancellation policy in the team's order service. Automatic chat attribution requires the inbox integration. The local landing page is a demonstration, not a full checkout.

Implementation: `CampaignService`, `CampaignsController`, `Views/Campaigns`, `Feature21Tests`.

### 22: Net realized ROI

Filter by Bangladesh date range and campaign. Revenue includes only Settled orders with a settlement timestamp in range. Order totals include delivery charged to customers and subtract discounts. Costs use saved item costs, applied matched courier settlement fees (or shipment charge fallback), return losses and ad spend. Return cases are authoritative; order-level loss is a fallback to avoid double counting. Non-settled returned orders can still contribute losses. Missing recorded fees contribute zero.

ROI = (revenue - product costs - courier fees - return losses - ad spend) / ad spend × 100. Zero spend displays N/A. Enter ad spend or import CSV with header `Date,CampaignId,Platform,CreativeKey,Amount`. Dates use yyyy-MM-dd. Imports validate all rows before saving and skip exact repeated import rows. Export the filtered report to CSV or an actual PDF.

Implementation: `RoiCalculator`, `DashboardController`, `Views/Dashboard`, `Feature22Tests`.

### 23: Creative fatigue

Enter four consecutive completed Monday-based weeks. The first two form a weighted cost/order and order/click baseline. Each of the following two weeks must meet both the cost-rise and conversion-drop thresholds. Partial weeks, gaps and unusable baselines do not trigger alerts. Positive spending and clicks with zero orders count as deterioration; undefined cost/order displays “No orders”. Rechecks do not duplicate alerts for the same creative/week. Acknowledge or resolve alerts; Owner can change thresholds. Correcting data that no longer meets the rule resolves that week's alert.

Notifications stay inside the app. Weekly performance spend is separate from the financial ad-spend ledger, so entering weekly totals does not duplicate ROI expenses.

Implementation: `FatigueDetector`, `AlertsController`, `Views/Alerts`, `Feature23Tests`.

## Teammate integration points

- Features 16/5 own inventory and order transitions. These features read stock and do not introduce a second stock editor or settlement path.
- Feature 17 can create Post/PostMedia/PostTarget records and reuse the approval service. The draft form only supports Feature 18.
- Features 2/3 can call `CampaignService.Attribute` before confirmation, enforcing the same merchant boundary.
- Features 9/11 supply settlement, courier and return-loss data for ROI.
- The existing four roles have no Manager role, so only Owner approves posts.
- Sensitive mutations use serializable transactions. Existing status/counter columns are EF concurrency tokens. Public tracked links are the deliberate exception to tenant filters, keyed by random codes and exposing public campaign labels only.
