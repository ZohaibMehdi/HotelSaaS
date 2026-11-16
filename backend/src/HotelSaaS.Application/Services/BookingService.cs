using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Bookings;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IBookingService
{
    Task<PagedResult<BookingListItem>> SearchAsync(int page, int pageSize, string? search, BookingStatus? status,
        DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<BookingDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken ct = default);
    Task<BookingDto> UpdateDatesAsync(long id, DateTime checkIn, DateTime checkOut,
        IReadOnlyList<long>? roomIds = null, CancellationToken ct = default);
    Task CheckInAsync(long id, CancellationToken ct = default);
    Task<BookingDto> WalkInCheckInAsync(WalkInCheckInRequest request, CancellationToken ct = default);
    Task<long> CheckOutAsync(long id, CancellationToken ct = default); // returns invoice id
    Task CancelAsync(long id, CancelBookingRequest request, CancellationToken ct = default);
    Task MarkNoShowAsync(long id, CancellationToken ct = default);
    Task<ChargeDto> AddChargeAsync(long id, AddChargeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ChargeDto>> GetChargesAsync(long id, CancellationToken ct = default);
}

public class BookingService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAvailabilityService availability,
    IAuditService audit) : IBookingService
{
    private static readonly BookingStatus[] BlockingStatuses =
        { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn };

    // ------------------------------------------------------------------ query

    public async Task<PagedResult<BookingListItem>> SearchAsync(int page, int pageSize, string? search,
        BookingStatus? status, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.Bookings.AsNoTracking().Where(b => b.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(b => b.BookingNumber.Contains(s) ||
                             (b.PrimaryGuest != null && b.PrimaryGuest.FirstName.Contains(s)) ||
                             (b.PrimaryGuest != null && b.PrimaryGuest.LastName!.Contains(s)));
        }
        if (status.HasValue) q = q.Where(b => b.BookingStatus == status.Value);
        if (from.HasValue) q = q.Where(b => b.CheckOutDate > from.Value);
        if (to.HasValue) q = q.Where(b => b.CheckInDate < to.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(b => b.CheckInDate).ThenByDescending(b => b.BookingId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(b => b.PrimaryGuest).Include(b => b.BookingRooms).ThenInclude(br => br.Room)
            .ToListAsync(ct);
        return new PagedResult<BookingListItem>(items.Select(b => new BookingListItem(
                b.BookingId, b.BookingNumber, GuestName(b), b.BookingStatus, b.BookingType,
                b.CheckInDate, b.CheckOutDate, b.TotalAmount,
                string.Join(", ", b.BookingRooms.Select(r => r.Room!.RoomNumber)))).ToList(),
            page, pageSize, total);
    }

    public async Task<BookingDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var b = await db.Bookings.AsNoTracking()
            .Include(x => x.PrimaryGuest).Include(x => x.CorporateCustomer).Include(x => x.Agreement)
            .Include(x => x.BookingRooms).ThenInclude(br => br.Room).ThenInclude(r => r.RoomType)
            .Include(x => x.Charges)
            .FirstOrDefaultAsync(x => x.BookingId == id && x.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Booking not found.");
        return MapDetail(b);
    }

    // ----------------------------------------------------------------- create

    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken ct = default)
    {
        Validate.Input(new CreateBookingValidator(), request);
        var tenantId = currentUser.RequireTenantId();

        var guest = await db.Guests.FirstOrDefaultAsync(g =>
                g.GuestId == request.GuestId && g.TenantId == tenantId && g.IsActive, ct)
            ?? throw new BadRequestException("Primary guest does not exist in this property.");

        CorporateCustomer? corp = null;
        B2BAgreement? agreement = null;
        Dictionary<long, decimal>? contractedRates = null;
        if (request.BookingType == BookingType.Corporate)
        {
            corp = await db.CorporateCustomers.FirstOrDefaultAsync(c =>
                    c.CorporateCustomerId == request.CorporateCustomerId && c.TenantId == tenantId && c.IsActive, ct)
                ?? throw new BadRequestException("Corporate customer not found.");
            agreement = await db.B2BAgreements.Include(a => a.Rates)
                .FirstOrDefaultAsync(a => a.AgreementId == request.AgreementId && a.TenantId == tenantId &&
                                          a.CorporateCustomerId == corp.CorporateCustomerId, ct)
                ?? throw new BadRequestException("Agreement not found for this corporate customer.");
            if (!agreement.IsApplicableOn(DateOnly.FromDateTime(request.CheckInDate)))
                throw new BusinessRuleException(
                    "The selected agreement is not ACTIVE for the booking dates (PRD rule 11).");
            contractedRates = agreement.Rates.ToDictionary(r => r.RoomTypeId, r => r.ContractedRate);
        }

        var rooms = await db.Rooms.Include(r => r.RoomType)
            .Where(r => r.TenantId == tenantId && r.IsActive && request.RoomIds.Contains(r.RoomId))
            .ToListAsync(ct);
        if (rooms.Count != request.RoomIds.Count)
            throw new BadRequestException("One or more selected rooms do not exist in this property.");
        if (rooms.Any(r => r.Status is RoomStatus.Maintenance or RoomStatus.OutOfService))
            throw new BusinessRuleException("A selected room is under maintenance or out of service.");

        var nights = PricingCalculator.Nights(request.CheckInDate, request.CheckOutDate);
        var taxRate = await GetTaxRateAsync(tenantId, ct);

        await db.BeginTransactionAsync(ct);
        try
        {
            // Authoritative conflict re-check inside the transaction (PRD section 43).
            var blocked = await availability.BlockedRoomIdsAsync(tenantId,
                request.CheckInDate, request.CheckOutDate, null, ct);
            var clash = rooms.Where(r => blocked.Contains(r.RoomId)).ToList();
            if (clash.Count > 0)
                throw new ConflictException(
                    $"Room {string.Join(", ", clash.Select(r => r.RoomNumber))} is already booked for the selected dates.");

            var lines = rooms.Select(r =>
            {
                var rate = contractedRates?.TryGetValue(r.RoomTypeId, out var cr) == true
                    ? cr
                    : r.RoomType!.BasePrice; // corporate contracted rate auto-applied (PRD section 28)
                return new BookingRoom
                {
                    TenantId = tenantId, RoomId = r.RoomId, RatePerNight = rate,
                    Nights = nights, TotalRoomCharge = Money.Round(rate * nights)
                };
            }).ToList();

            var subtotal = lines.Sum(l => l.TotalRoomCharge);
            var (tax, total) = PricingCalculator.ApplyTax(subtotal, request.DiscountAmount,
                taxRate.Rate, taxRate.Inclusive);

            var number = await NextBookingNumberAsync(tenantId, ct);
            var booking = new Booking
            {
                TenantId = tenantId,
                BookingNumber = number,
                PrimaryGuestId = guest.GuestId,
                BookingType = request.BookingType,
                BookingSource = request.BookingSource,
                BookingStatus = BookingStatus.Confirmed, // created as Confirmed; front desk confirms/cancels/checks-in
                CorporateCustomerId = corp?.CorporateCustomerId,
                AgreementId = agreement?.AgreementId,
                CheckInDate = request.CheckInDate,
                CheckOutDate = request.CheckOutDate,
                NumberOfAdults = request.NumberOfAdults,
                NumberOfChildren = request.NumberOfChildren,
                DiscountAmount = request.DiscountAmount,
                TaxAmount = tax,
                DepositAmount = request.DepositAmount,
                TotalAmount = total,
                SpecialRequests = request.SpecialRequests,
                Notes = request.Notes,
                CreatedBy = currentUser.UserId,
                CreatedAt = DateTime.UtcNow
            };
            foreach (var line in lines)
            {
                line.Booking = booking;
                booking.BookingRooms.Add(line);
            }
            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);

            if (request.AccompanyGuests is { Count: > 0 })
            {
                foreach (var ag in request.AccompanyGuests)
                    db.AccompanyGuests.Add(new AccompanyGuest
                    {
                        TenantId = tenantId, BookingId = booking.BookingId,
                        FullName = ag.FullName.Trim(), IdType = ag.IdType, IdNumber = ag.IdNumber,
                        Relationship = ag.Relationship, Phone = ag.Phone, Age = ag.Age,
                        CreatedAt = DateTime.UtcNow
                    });
            }
            if (request.AdditionalGuestIds is { Count: > 0 })
            {
                var validIds = await db.Guests.Where(g =>
                    g.TenantId == tenantId && request.AdditionalGuestIds.Contains(g.GuestId))
                    .Select(g => g.GuestId).ToListAsync(ct);
                foreach (var gid in validIds.Distinct())
                    db.BookingGuests.Add(new BookingGuest
                    {
                        TenantId = tenantId, BookingId = booking.BookingId, GuestId = gid
                    });
            }
            await db.SaveChangesAsync(ct);
            await db.CommitTransactionAsync(ct);

            await audit.LogAsync("Create", nameof(Booking), booking.BookingId, null,
                new { booking.BookingNumber, Rooms = lines.Select(l => l.RoomId) }, tenantId, ct);

            return await GetByIdAsync(booking.BookingId, ct);
        }
        catch
        {
            await db.RollbackTransactionAsync(ct);
            throw;
        }
    }

    /// <summary>Reschedule / reallocate while still Pending or Confirmed.</summary>
    public async Task<BookingDto> UpdateDatesAsync(long id, DateTime checkIn, DateTime checkOut,
        IReadOnlyList<long>? roomIds = null, CancellationToken ct = default)
    {
        if (checkOut <= checkIn)
            throw new BusinessRuleException("Check-out date must be after check-in date.");
        var tenantId = currentUser.RequireTenantId();

        await db.BeginTransactionAsync(ct);
        try
        {
            var booking = await db.Bookings
                .Include(b => b.BookingRooms).ThenInclude(br => br.Room)
                .FirstOrDefaultAsync(b => b.BookingId == id && b.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Booking not found.");
            if (booking.BookingStatus is not (BookingStatus.Pending or BookingStatus.Confirmed))
                throw new BusinessRuleException("Only pending or confirmed bookings can be modified.");
            if (roomIds != null && roomIds.Count > 0 && !roomIds.All(booking.BookingRooms.Select(br => br.RoomId).Contains))
                throw new BusinessRuleException("Existing room allocations cannot be replaced here; cancel and rebook.");

            var blocked = await availability.BlockedRoomIdsAsync(tenantId, checkIn, checkOut, id, ct);
            var clash = booking.BookingRooms.Where(br => blocked.Contains(br.RoomId)).ToList();
            if (clash.Count > 0)
                throw new ConflictException("New dates overlap another reservation for the allocated room(s).");

            var oldDates = new { booking.CheckInDate, booking.CheckOutDate };
            booking.CheckInDate = checkIn;
            booking.CheckOutDate = checkOut;
            var nights = PricingCalculator.Nights(checkIn, checkOut);
            foreach (var br in booking.BookingRooms)
            {
                br.Nights = nights;
                br.TotalRoomCharge = Money.Round(br.RatePerNight * nights);
            }
            var subtotal = booking.BookingRooms.Sum(br => br.TotalRoomCharge);
            var taxRate = await GetTaxRateAsync(tenantId, ct);
            var (tax, total) = PricingCalculator.ApplyTax(subtotal, booking.DiscountAmount, taxRate.Rate, taxRate.Inclusive);
            booking.TaxAmount = tax;
            booking.TotalAmount = total;
            booking.UpdatedAt = DateTime.UtcNow;
            booking.UpdatedBy = currentUser.UserId;
            await db.SaveChangesAsync(ct);
            await db.CommitTransactionAsync(ct);

            await audit.LogAsync("Update", nameof(Booking), id, oldDates,
                new { checkIn, checkOut }, tenantId, ct);
            return await GetByIdAsync(id, ct);
        }
        catch
        {
            await db.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // -------------------------------------------------------------- check-in

    /// <summary>PRD acceptance criteria: Confirmed → CheckedIn + room Occupied, timestamps and user recorded.</summary>
    public async Task CheckInAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        await db.BeginTransactionAsync(ct);
        try
        {
            var booking = await LoadForLifecycleAsync(id, tenantId, ct);
            if (booking.BookingStatus != BookingStatus.Confirmed)
                throw new BusinessRuleException("Only CONFIRMED bookings can be checked in.");

            var now = DateTime.UtcNow;
            var blocked = await availability.BlockedRoomIdsAsync(tenantId, booking.CheckInDate,
                booking.CheckOutDate, id, ct);
            // The room must be free RIGHT NOW - an occupied room cannot host a second stay.
            foreach (var br in booking.BookingRooms)
            {
                var room = await db.Rooms.FirstAsync(r => r.RoomId == br.RoomId, ct);
                if (room.Status is RoomStatus.Occupied or RoomStatus.Maintenance or RoomStatus.OutOfService)
                    throw new ConflictException($"Room {room.RoomNumber} is currently unavailable for check-in.");
                if (blocked.Contains(room.RoomId))
                    throw new ConflictException($"Room {room.RoomNumber} has an overlapping reservation.");
                room.Status = RoomStatus.Occupied;
                room.HousekeepingStatus = HousekeepingStatus.Dirty;
                room.UpdatedAt = now;
                room.UpdatedBy = currentUser.UserId;
            }

            booking.BookingStatus = BookingStatus.CheckedIn;
            booking.ActualCheckIn = now;
            booking.CheckedInBy = currentUser.UserId;
            booking.UpdatedAt = now;
            booking.UpdatedBy = currentUser.UserId;
            await db.SaveChangesAsync(ct);
            await db.CommitTransactionAsync(ct);

            await audit.LogAsync("CheckIn", nameof(Booking), id, null,
                new { booking.BookingNumber, At = now }, tenantId, ct);
        }
        catch
        {
            await db.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<BookingDto> WalkInCheckInAsync(WalkInCheckInRequest request, CancellationToken ct = default)
    {
        Validate.Input(new WalkInCheckInValidator(), request);
        var created = await CreateAsync(new CreateBookingRequest(
            request.GuestId, BookingSource.WalkIn, DateTime.UtcNow, request.CheckOutDate,
            [request.RoomId], request.NumberOfAdults, request.NumberOfChildren,
            BookingType.Individual, null, null, 0, request.DepositAmount,
            request.AccompanyGuests, null, null, request.Notes), ct);
        await CheckInAsync(created.BookingId, ct);
        return await GetByIdAsync(created.BookingId, ct);
    }

    // ------------------------------------------------------------- check-out

    /// <summary>
    /// PRD acceptance criteria: charges finalized, invoice generated with correct balance,
    /// booking CheckedOut, rooms moved to Cleaning with a housekeeping task.
    /// </summary>
    public async Task<long> CheckOutAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        await db.BeginTransactionAsync(ct);
        try
        {
            var booking = await LoadForLifecycleAsync(id, tenantId, ct);
            if (booking.BookingStatus != BookingStatus.CheckedIn)
                throw new BusinessRuleException("Only CHECKED-IN bookings can be checked out.");

            var now = DateTime.UtcNow;
            var settings = await db.PropertySettings.AsNoTracking()
                .FirstAsync(p => p.TenantId == tenantId, ct);
            var company = await db.CompanyInformations.AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

            // Materialize room tariff into the folio so the invoice reflects lodging cost.
            // (During the stay, BookingRooms holds the tariff; folio holds services.)
            var hasRoomCharges = await db.Charges.AnyAsync(
                c => c.BookingId == id && c.ChargeType == ChargeType.Room, ct);
            if (!hasRoomCharges)
            {
                foreach (var br in booking.BookingRooms)
                    db.Charges.Add(new Charge
                    {
                        TenantId = tenantId,
                        BookingId = id,
                        ChargeType = ChargeType.Room,
                        Description = $"Room charge - {br.Room!.RoomNumber} ({br.Nights} night(s))",
                        ChargeDate = booking.ActualCheckIn ?? booking.CheckInDate,
                        Quantity = br.Nights,
                        UnitPrice = br.RatePerNight,
                        DiscountAmount = 0,
                        TaxAmount = 0,
                        TotalAmount = br.TotalRoomCharge,
                        CreatedBy = currentUser.UserId,
                        CreatedAt = DateTime.UtcNow
                    });
                await db.SaveChangesAsync(ct);
            }

            var charges = await db.Charges.Where(c => c.BookingId == id).OrderBy(c => c.CreatedAt).ToListAsync(ct);
            var subtotal = charges.Sum(c => c.TotalAmount);
            var discountTotal = charges.Sum(c => c.DiscountAmount);
            var taxTotal = charges.Sum(c => c.TaxAmount);
            var grand = subtotal; // charge totals already include their own discount/tax

            var invoice = new Invoice
            {
                TenantId = tenantId,
                InvoiceNumber = await NextInvoiceNumberAsync(tenantId, ct),
                BookingId = booking.BookingId,
                GuestId = booking.PrimaryGuestId,
                CorporateCustomerId = booking.CorporateCustomerId,
                InvoiceType = booking.BookingType == BookingType.Corporate ? InvoiceType.Corporate : InvoiceType.Guest,
                InvoiceDate = now,
                DueDate = booking.AgreementId.HasValue ? DateOnly.FromDateTime(now.AddDays(
                              (await db.B2BAgreements.AsNoTracking().FirstAsync(a => a.AgreementId == booking.AgreementId!.Value, ct))
                              .PaymentTermsDays)) : null,
                Subtotal = subtotal,
                DiscountTotal = discountTotal,
                TaxTotal = taxTotal,
                TotalAmount = grand,
                PaidAmount = 0,
                BalanceDue = grand,
                Status = InvoiceStatus.Issued,
                IssuedBy = currentUser.UserId,
                IssuedAt = now,
                Notes = company?.InvoiceFooter,
                CreatedBy = currentUser.UserId
            };

            foreach (var c in charges)
                invoice.Items.Add(new InvoiceItem
                {
                    TenantId = tenantId,
                    Description = c.Description,
                    ChargeDate = c.ChargeDate,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    DiscountAmount = c.DiscountAmount,
                    TaxAmount = c.TaxAmount,
                    TotalAmount = c.TotalAmount,
                    SourceChargeId = c.ChargeId
                });
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(ct);

            // Deposit already held converts into a traceable payment on the folio.
            if (booking.DepositAmount > 0)
                ApplyPayment(invoice, new Payment
                {
                    TenantId = tenantId, InvoiceId = invoice.InvoiceId,
                    Amount = Math.Min(booking.DepositAmount, grand),
                    PaymentMethod = PaymentMethod.Other,
                    TransactionReference = $"DEPOSIT-{booking.BookingNumber}",
                    PaymentDate = now, ReceivedBy = currentUser.UserId,
                    Notes = "Deposit transferred from booking.", CreatedBy = currentUser.UserId
                });

            booking.BookingStatus = BookingStatus.CheckedOut;
            booking.ActualCheckOut = now;
            booking.CheckedOutBy = currentUser.UserId;
            booking.UpdatedAt = now;
            booking.UpdatedBy = currentUser.UserId;

            foreach (var br in booking.BookingRooms)
            {
                var room = await db.Rooms.FirstAsync(r => r.RoomId == br.RoomId, ct);
                room.Status = RoomStatus.Cleaning;
                room.HousekeepingStatus = HousekeepingStatus.Dirty;
                room.UpdatedAt = now;
                room.UpdatedBy = currentUser.UserId;

                db.HousekeepingTasks.Add(new HousekeepingTask
                {
                    TenantId = tenantId, RoomId = room.RoomId, BookingId = booking.BookingId,
                    TaskType = HousekeepingTaskType.Cleaning, Status = HousekeepingTaskStatus.Pending,
                    ScheduledFor = now, CreatedBy = currentUser.UserId, CreatedAt = now
                });
            }

            await db.SaveChangesAsync(ct);
            await db.CommitTransactionAsync(ct);

            await audit.LogAsync("CheckOut", nameof(Booking), id, null,
                new { booking.BookingNumber, Invoice = invoice.InvoiceNumber, Total = grand }, tenantId, ct);
            return invoice.InvoiceId;
        }
        catch
        {
            await db.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // --------------------------------------------------------------- cancel

    public async Task CancelAsync(long id, CancelBookingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { ["reason"] = ["Cancellation reason is required."] });
        var tenantId = currentUser.RequireTenantId();
        var booking = await LoadForLifecycleAsync(id, tenantId, ct);
        if (booking.BookingStatus is BookingStatus.CheckedIn or BookingStatus.CheckedOut)
            throw new BusinessRuleException("Checked-in or completed stays cannot be cancelled (PRD rule 6).");
        if (booking.BookingStatus == BookingStatus.Cancelled) return;

        booking.BookingStatus = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.CancellationReason = request.Reason.Trim();
        booking.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Cancel", nameof(Booking), id, null,
            new { booking.BookingNumber, Reason = request.Reason }, tenantId, ct);
    }

    public async Task MarkNoShowAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var booking = await LoadForLifecycleAsync(id, tenantId, ct);
        if (booking.BookingStatus != BookingStatus.Confirmed)
            throw new BusinessRuleException("Only confirmed bookings can be marked as no-show.");
        booking.BookingStatus = BookingStatus.NoShow;
        booking.UpdatedAt = DateTime.UtcNow;
        booking.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("NoShow", nameof(Booking), id, null, new { booking.BookingNumber }, tenantId, ct);
    }

    // -------------------------------------------------------------- charges

    public async Task<ChargeDto> AddChargeAsync(long id, AddChargeRequest request, CancellationToken ct = default)
    {
        Validate.Input(new AddChargeValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.BookingId == id && b.TenantId == tenantId, ct)
                      ?? throw new NotFoundException("Booking not found.");

        var serviceDuringStay = booking.BookingStatus == BookingStatus.CheckedIn;
        var adjustmentAllowed = request.ChargeType == ChargeType.Adjustment &&
                                booking.BookingStatus is BookingStatus.Pending or BookingStatus.Confirmed
                                    or BookingStatus.CheckedIn;
        if (!serviceDuringStay && !adjustmentAllowed)
            throw new BusinessRuleException("Charges can only be added during an active stay (or adjustments pre-arrival).");

        var total = PricingCalculator.LineTotal(request.Quantity, request.UnitPrice, request.DiscountAmount, 0);
        var charge = new Charge
        {
            TenantId = tenantId,
            BookingId = id,
            ChargeType = request.ChargeType,
            Description = request.Description.Trim(),
            ChargeDate = DateTime.UtcNow,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            DiscountAmount = request.DiscountAmount,
            TaxAmount = 0,
            TotalAmount = total,
            CreatedBy = currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };
        db.Charges.Add(charge);

        booking.TotalAmount += total; // running estimate; final at checkout from folio
        booking.UpdatedAt = DateTime.UtcNow;
        booking.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("AddCharge", nameof(Booking), id, null,
            new { charge.Description, charge.TotalAmount }, tenantId, ct);
        return ToChargeDto(charge);
    }

    public async Task<IReadOnlyList<ChargeDto>> GetChargesAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var list = await db.Charges.AsNoTracking()
            .Where(c => c.BookingId == id && c.TenantId == tenantId)
            .OrderBy(c => c.ChargeDate).ToListAsync(ct);
        return list.Select(ToChargeDto).ToList();
    }

    // -------------------------------------------------------------- helpers

    private async Task<Booking> LoadForLifecycleAsync(long id, long tenantId, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.BookingRooms).ThenInclude(br => br.Room)
            .Include(b => b.PrimaryGuest)
            .Include(b => b.Agreement)
            .FirstOrDefaultAsync(b => b.BookingId == id && b.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Booking not found.");
        return booking;
    }

    private async Task<(decimal Rate, bool Inclusive)> GetTaxRateAsync(long tenantId, CancellationToken ct)
    {
        var s = await db.PropertySettings.AsNoTracking()
            .Where(p => p.TenantId == tenantId).Select(p => new { p.TaxRatePercent, p.TaxInclusive })
            .FirstOrDefaultAsync(ct);
        return s == null ? (0, false) : (s.TaxRatePercent, s.TaxInclusive);
    }

    private async Task<string> NextBookingNumberAsync(long tenantId, CancellationToken ct) =>
        PrefixOf(tenantId, true) + await db.IncrementCounterAsync(tenantId, true, ct);

    private async Task<string> NextInvoiceNumberAsync(long tenantId, CancellationToken ct) =>
        PrefixOf(tenantId, false) + await db.IncrementCounterAsync(tenantId, false, ct);

    private string PrefixOf(long tenantId, bool booking)
    {
        // Prefix read separately; the counter increment itself is atomic.
        var cached = db.PropertySettings.Local.FirstOrDefault(p => p.TenantId == tenantId);
        return cached == null ? (booking ? "BK-" : "INV-") : (booking ? cached.BookingPrefix : cached.InvoicePrefix);
    }

    public static void ApplyPayment(Invoice invoice, Payment payment)
    {
        invoice.Payments.Add(payment);
        invoice.PaidAmount = Money.Round(invoice.PaidAmount + payment.Amount);
        invoice.BalanceDue = Money.Round(invoice.TotalAmount - invoice.PaidAmount);
        invoice.Status = invoice.BalanceDue <= 0 ? InvoiceStatus.Paid
            : invoice.PaidAmount > 0 ? InvoiceStatus.PartiallyPaid : invoice.Status;
    }

    private static string GuestName(Booking b) =>
        b.PrimaryGuest == null ? string.Empty : $"{b.PrimaryGuest.FirstName} {b.PrimaryGuest.LastName}".Trim();

    private static ChargeDto ToChargeDto(Charge c) => new(
        c.ChargeId, c.ChargeType, c.Description, c.ChargeDate, c.Quantity, c.UnitPrice,
        c.DiscountAmount, c.TaxAmount, c.TotalAmount);

    internal static BookingDto MapDetail(Booking b) => new(
        b.BookingId, b.TenantId, b.BookingNumber, b.PrimaryGuestId, GuestName(b),
        b.BookingType, b.BookingSource, b.BookingStatus,
        b.CorporateCustomerId, b.CorporateCustomer?.CompanyName, b.AgreementId, b.Agreement?.AgreementNumber,
        b.CheckInDate, b.CheckOutDate, b.ActualCheckIn, b.ActualCheckOut,
        b.NumberOfAdults, b.NumberOfChildren, b.DiscountAmount, b.TaxAmount, b.DepositAmount, b.TotalAmount,
        b.SpecialRequests, b.Notes,
        b.BookingRooms.Select(br => new BookingRoomDto(br.BookingRoomId, br.RoomId,
            br.Room?.RoomNumber ?? string.Empty, br.Room?.RoomType?.Name ?? string.Empty,
            br.RatePerNight, br.Nights, br.TotalRoomCharge)).ToList(),
        b.Charges.OrderBy(c => c.ChargeDate).Select(ToChargeDto).ToList(),
        b.Charges.Sum(c => c.TotalAmount),
        b.CancellationReason);
}

