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

## Integration boundary

Features 21–23 are delivered separately by Opi. Existing shared entities remain part of the supplied team baseline.
