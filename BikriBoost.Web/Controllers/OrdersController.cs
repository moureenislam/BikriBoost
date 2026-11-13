using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static BikriBoost.Web.Models.Data.ReferenceData.RoleNames;

namespace BikriBoost.Web.Controllers;

/// <summary>
/// Feature 2 (Capture → Draft → Create)
/// for orders. The rules live in the Model-layer services; this class only routes.
/// </summary>
public class OrdersController(
    AppDbContext db,
    OrderTextParser parser,
    OrderService orders) : AppController
{
    public async Task<IActionResult> Index(OrderStatus? status)
    {
        var counts = await db.Orders.GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var rows = await db.Orders
            .Where(o => status == null || o.Status == status)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderRow(o.Id, o.OrderNumber, o.ShippingName, o.ShippingPhone, o.District!.NameEn,
                o.Total, o.Status, o.IsLockedForAdvance, o.CreatedAt))
            .Take(200).ToListAsync();
        return View(new OrderListViewModel { Status = status, Counts = counts, Orders = rows });
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await db.Orders
            .Include(o => o.Items).ThenInclude(i => i.ProductVariant!).ThenInclude(v => v.Product)
            .Include(o => o.District).Include(o => o.Thana)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        var vm = new OrderDetailsViewModel
        {
            Order = order,
            DistrictName = order.District!.NameEn,
            ThanaName = order.Thana?.NameEn,
            Items = order.Items.Select(i => (i, $"{i.ProductVariant!.Product!.Name} ({OrderTextParser.VariantLabel(i.ProductVariant)}) · {i.ProductVariant.Sku}")).ToList(),
        };
        return View(vm);
    }

    // ── Feature 2 · paste a chat message → parsed draft → order ──────────────

    [HttpGet, Authorize(Roles = $"{Owner},{Moderator}")]
    public IActionResult Capture() => View();

    [HttpPost, Authorize(Roles = $"{Owner},{Moderator}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Capture(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            TempData["Error"] = "Paste the customer's message first.";
            return View();
        }

        var parsed = await parser.ParseAsync(rawText);
        var draft = await orders.SaveDraftAsync(rawText, parsed, UserId);

        var form = new NewOrderInput
        {
            DraftId = draft.Id,
            Name = parsed.Name,
            Phone = parsed.Phone,
            Address = parsed.Address,
            DistrictId = parsed.DistrictId,
            ThanaId = parsed.ThanaId,
            DeliveryCharge = OrderService.DefaultDeliveryCharge(parsed.DistrictName),
            Items = parsed.Items.Select(i => new NewOrderLine
            {
                ProductVariantId = i.VariantUnclear ? null : i.ProductVariantId, // unclear size: the moderator picks it
                Quantity = i.Quantity,
            }).ToList(),
        };
        var notes = parsed.Items.Where(i => i.VariantUnclear)
            .Select(i => $"\"{i.ProductName}\" × {i.Quantity}: the message does not say which size/colour. Pick it below.").ToList();
        if (parsed.DistrictName is not null) notes.Insert(0, $"District detected: {parsed.DistrictName}{(parsed.ThanaName is null ? "" : $", thana {parsed.ThanaName}")}.");

        return View("Draft", await BuildDraftAsync(rawText, form, parsed.Missing, notes, parsed.UnmatchedItemsText));
    }

    [HttpPost, Authorize(Roles = $"{Owner},{Moderator}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] NewOrderInput form, string rawText)
    {
        try
        {
            var order = await orders.CreateAsync(form, UserId);
            TempData["Success"] = $"Order {order.OrderNumber} created (Pending).";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
        catch (RuleViolationException e)
        {
            ModelState.AddModelError("", e.Message);
            return View("Draft", await BuildDraftAsync(rawText, form, [], [], null));
        }
    }

    private async Task<DraftViewModel> BuildDraftAsync(string rawText, NewOrderInput form, List<string> missing, List<string> notes, string? unmatched)
    {
        while (form.Items.Count < 4) form.Items.Add(new NewOrderLine { Quantity = 1 }); // spare rows to add items by hand

        var variants = await db.ProductVariants.Include(v => v.Product)
            .Where(v => v.Product!.IsActive).OrderBy(v => v.Product!.Name).ThenBy(v => v.Id).ToListAsync();
        var thanas = await db.Thanas.Include(t => t.District).OrderBy(t => t.NameEn).ToListAsync();

        return new DraftViewModel
        {
            RawText = rawText,
            Form = form,
            Missing = missing,
            DetectedNotes = notes,
            UnmatchedItemsText = unmatched,
            Districts = new SelectList(await db.Districts.OrderBy(d => d.NameEn).ToListAsync(), "Id", "NameEn"),
            Thanas = new SelectList(thanas.Select(t => new { t.Id, Name = $"{t.NameEn} ({t.District!.NameEn})" }), "Id", "Name"),
            Variants = variants.Select(v => new SelectListItem(
                $"{v.Product!.Name} {OrderTextParser.VariantLabel(v)} · {v.Sku} · Tk {v.Price:N0} · {v.Available} available",
                v.Id.ToString())).ToList(),
        };
    }
}
