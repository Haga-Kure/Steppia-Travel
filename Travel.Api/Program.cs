using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json.Serialization;
using Travel.Api.Dtos;
using Travel.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Travel.Api.Models;
using Travel.Api.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Mongo - Read from environment variables (Railway/Production) or appsettings.json (Development)
// Environment variable format: MONGO__CONNECTIONSTRING or MONGO_CONNECTIONSTRING
var mongoConn = builder.Configuration["Mongo:ConnectionString"] 
    ?? Environment.GetEnvironmentVariable("MONGO_CONNECTIONSTRING")
    ?? Environment.GetEnvironmentVariable("MONGO__CONNECTIONSTRING")
    ?? throw new InvalidOperationException("MongoDB connection string is required. Set MONGO_CONNECTIONSTRING environment variable or configure in appsettings.json");

var mongoDbName = builder.Configuration["Mongo:DatabaseName"] 
    ?? Environment.GetEnvironmentVariable("MONGO_DATABASENAME")
    ?? Environment.GetEnvironmentVariable("MONGO__DATABASENAME")
    ?? "travel_db";

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConn));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDbName);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

static string NewBookingCode()
{
    const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    var rng = Random.Shared;
    var s = new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    return $"BK-{s}";
}

static bool IsExpired(Booking b) => b.ExpiresAt <= DateTime.UtcNow;

static async Task ExpireBookingIfNeeded(IMongoCollection<Booking> bookings, Booking b)
{
    if (b.Status == BookingStatus.PendingPayment && IsExpired(b))
    {
        var update = Builders<Booking>.Update
            .Set(x => x.Status, BookingStatus.Expired)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await bookings.UpdateOneAsync(x => x.Id == b.Id && x.Status == BookingStatus.PendingPayment, update);
    }
}

var app = builder.Build();

// Configure port for Railway (uses PORT environment variable automatically)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port) && int.TryParse(port, out var portNumber))
{
    app.Urls.Add($"http://0.0.0.0:{portNumber}");
}

app.UseSwagger();
app.UseSwaggerUI();

// 1) Mongo health check
app.MapGet("/health/mongo", async (IMongoDatabase db) =>
{
    var result = await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
    return Results.Ok(new { ok = true, result = result.ToString() });
});

// 2) List tours (raw Bson for now, easiest)
app.MapGet("/tours", async (IMongoDatabase db) =>
{
    var col = db.GetCollection<Tour>("tours");

    var list = await col.Find(t => t.IsActive).Limit(50).ToListAsync();

    var dto = list.Select(t => new TourDto(
        t.Id.ToString(),
        t.Slug,
        t.Title,
        t.Type,
        t.Summary,
        t.DurationDays,
        t.BasePrice,
        t.Currency,
        t.Locations
    )).ToList();

    return Results.Ok(dto);
});

app.MapGet("/tours/{slug}", async (string slug, IMongoDatabase db) =>
{
    var col = db.GetCollection<Tour>("tours");
    var tour = await col.Find(t => t.Slug == slug && t.IsActive).FirstOrDefaultAsync();
    return tour is null ? Results.NotFound() : Results.Ok(tour);
});

app.MapPost("/bookings", async (CreateBookingRequest req, IMongoDatabase db) =>
{
    // basic validation
    if (string.IsNullOrWhiteSpace(req.TourId)) return Results.BadRequest("TourId is required.");
    if (req.Guests is null || req.Guests.Count == 0) return Results.BadRequest("Guests are required.");
    if (req.Contact is null || string.IsNullOrWhiteSpace(req.Contact.Email) || string.IsNullOrWhiteSpace(req.Contact.FullName))
        return Results.BadRequest("Contact fullName and email are required.");

    var isGroup = string.Equals(req.TourType, "Group", StringComparison.OrdinalIgnoreCase);
    var isPrivate = string.Equals(req.TourType, "Private", StringComparison.OrdinalIgnoreCase);
    if (!isGroup && !isPrivate) return Results.BadRequest("TourType must be 'Private' or 'Group'.");

    if (isGroup && string.IsNullOrWhiteSpace(req.TourDateId))
        return Results.BadRequest("TourDateId is required for Group tours.");

    if (isPrivate && req.TravelDate is null)
        return Results.BadRequest("TravelDate is required for Private tours.");

    if (!ObjectId.TryParse(req.TourId, out var tourId))
        return Results.BadRequest("Invalid TourId.");

    ObjectId? tourDateId = null;
    if (!string.IsNullOrWhiteSpace(req.TourDateId))
    {
        if (!ObjectId.TryParse(req.TourDateId, out var parsed)) return Results.BadRequest("Invalid TourDateId.");
        tourDateId = parsed;
    }

    var tours = db.GetCollection<Tour>("tours");
    var tourDates = db.GetCollection<TourDate>("tour_dates");
    var bookings = db.GetCollection<Booking>("bookings");

    var tour = await tours.Find(t => t.Id == tourId && t.IsActive).FirstOrDefaultAsync();
    if (tour is null) return Results.NotFound("Tour not found.");

    // pricing
    var currency = tour.Currency;
    decimal pricePerBooking = tour.BasePrice;

    TourDate? dateDoc = null;
    if (isGroup)
    {
        dateDoc = await tourDates.Find(d => d.Id == tourDateId && d.TourId == tourId && d.Status == TourDateStatus.Open)
                                 .FirstOrDefaultAsync();
        if (dateDoc is null) return Results.BadRequest("TourDate not found or not open.");

        if (dateDoc.PriceOverride is not null)
            pricePerBooking = dateDoc.PriceOverride.Value;

        // basic capacity check: count seats held by pending_payment (not expired) + confirmed
        var now = DateTime.UtcNow;
        var heldOrConfirmed = Builders<Booking>.Filter.And(
            Builders<Booking>.Filter.Eq(x => x.TourDateId, dateDoc.Id),
            Builders<Booking>.Filter.In(x => x.Status, new[] { BookingStatus.PendingPayment, BookingStatus.Confirmed }),
            Builders<Booking>.Filter.Gt(x => x.ExpiresAt, now) // pending holds that haven't expired
        );

        var seatsUsed = await bookings.CountDocumentsAsync(heldOrConfirmed);
        var seatsRequested = req.Guests.Count;

        if (seatsUsed + seatsRequested > dateDoc.Capacity)
            return Results.Conflict("Not enough seats available for this departure.");
    }

    // totals (simple MVP: total = basePrice; if you want per-guest pricing, multiply here)
    var subtotal = pricePerBooking;
    var discount = 0m;
    var tax = 0m;
    var total = subtotal - discount + tax;

    // generate unique booking code (retry a few times)
    string code = "";
    for (var i = 0; i < 5; i++)
    {
        code = NewBookingCode();
        var exists = await bookings.Find(x => x.BookingCode == code).AnyAsync();
        if (!exists) break;
    }
    if (string.IsNullOrWhiteSpace(code)) return Results.Problem("Failed to generate booking code.");

    var booking = new Booking
    {
        BookingCode = code,
        TourId = tour.Id,
        TourDateId = tourDateId,
        TravelDate = req.TravelDate,
        TourType = isGroup ? "Group" : "Private",
        Contact = new BookingContact
        {
            FullName = req.Contact.FullName.Trim(),
            Email = req.Contact.Email.Trim(),
            Phone = req.Contact.Phone,
            Country = req.Contact.Country
        },
        Guests = req.Guests.Select(g => new BookingGuest
        {
            FullName = g.FullName.Trim(),
            Age = g.Age,
            PassportNo = g.PassportNo
        }).ToList(),
        SpecialRequests = req.SpecialRequests,
        Pricing = new BookingPricing
        {
            Currency = currency,
            Subtotal = subtotal,
            Discount = discount,
            Tax = tax,
            Total = total
        },
        Status = BookingStatus.PendingPayment,
        ExpiresAt = DateTime.UtcNow.AddMinutes(30),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    await bookings.InsertOneAsync(booking);

    return Results.Ok(new CreateBookingResponse(
        booking.Id.ToString(),
        booking.BookingCode,
        booking.Status.ToString(),
        booking.ExpiresAt,
        booking.Pricing.Total,
        booking.Pricing.Currency
    ));
});

app.MapGet("/bookings/{bookingCode}", async (string bookingCode, IMongoDatabase db) =>
{
    var bookings = db.GetCollection<Booking>("bookings");
    var b = await bookings.Find(x => x.BookingCode == bookingCode).FirstOrDefaultAsync();
    if (b is null) return Results.NotFound();

    await ExpireBookingIfNeeded(bookings, b);

    // re-read after potential expire
    b = await bookings.Find(x => x.BookingCode == bookingCode).FirstOrDefaultAsync();
    return Results.Ok(new
    {
        id = b!.Id.ToString(),
        b.BookingCode,
        b.Status,
        b.ExpiresAt,
        b.TourId,
        b.TourDateId,
        b.TravelDate,
        b.TourType,
        b.Contact,
        guestCount = b.Guests.Count,
        b.Pricing,
        b.CreatedAt
    });
});

app.MapPost("/payments", async (CreatePaymentRequest req, IMongoDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(req.BookingCode)) return Results.BadRequest("BookingCode is required.");
    if (string.IsNullOrWhiteSpace(req.Provider)) return Results.BadRequest("Provider is required.");

    var bookings = db.GetCollection<Booking>("bookings");
    var payments = db.GetCollection<Payment>("payments");

    var booking = await bookings.Find(x => x.BookingCode == req.BookingCode).FirstOrDefaultAsync();
    if (booking is null) return Results.NotFound("Booking not found.");

    await ExpireBookingIfNeeded(bookings, booking);
    booking = await bookings.Find(x => x.BookingCode == req.BookingCode).FirstOrDefaultAsync();
    if (booking!.Status != BookingStatus.PendingPayment)
        return Results.Conflict($"Booking status is {booking.Status}, payment not allowed.");

    if (IsExpired(booking)) return Results.Conflict("Booking expired.");

    // --- Provider integration placeholder ---
    // In real world:
    // 1) Create invoice/payment intent with provider
    // 2) Receive invoiceId/checkoutUrl/qrText
    var invoiceId = $"INV-{Guid.NewGuid():N}".ToUpperInvariant();
    var checkoutUrl = $"https://example.com/checkout/{invoiceId}";
    var qrText = invoiceId; // for QR-based providers

    var payment = new Payment
    {
        BookingId = booking.Id,
        Provider = req.Provider.Trim().ToLowerInvariant(),
        InvoiceId = invoiceId,
        Amount = booking.Pricing.Total,
        Currency = booking.Pricing.Currency,
        Status = PaymentStatus.Pending,
        ProviderCheckoutUrl = checkoutUrl,
        ProviderQrText = qrText,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    await payments.InsertOneAsync(payment);

    return Results.Ok(new CreatePaymentResponse(
        payment.Id.ToString(),
        payment.Status.ToString(),
        payment.Provider,
        payment.InvoiceId,
        payment.ProviderCheckoutUrl,
        payment.ProviderQrText
    ));
});

app.MapPost("/payments/webhook", async (PaymentWebhookRequest req, IMongoDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(req.InvoiceId)) return Results.BadRequest("InvoiceId required.");
    if (string.IsNullOrWhiteSpace(req.Status)) return Results.BadRequest("Status required.");

    var payments = db.GetCollection<Payment>("payments");
    var bookings = db.GetCollection<Booking>("bookings");

    var payment = await payments.Find(x => x.InvoiceId == req.InvoiceId).FirstOrDefaultAsync();
    if (payment is null) return Results.NotFound("Payment not found.");

    var normalized = req.Status.Trim().ToLowerInvariant();

    if (normalized == "paid")
    {
        // 1) mark payment paid (if not already)
        var payUpdate = Builders<Payment>.Update
            .Set(x => x.Status, PaymentStatus.Paid)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await payments.UpdateOneAsync(x => x.Id == payment.Id && x.Status != PaymentStatus.Paid, payUpdate);

        // 2) confirm booking if still pending and not expired
        var booking = await bookings.Find(x => x.Id == payment.BookingId).FirstOrDefaultAsync();
        if (booking is null) return Results.NotFound("Booking not found for payment.");

        await ExpireBookingIfNeeded(bookings, booking);
        booking = await bookings.Find(x => x.Id == payment.BookingId).FirstOrDefaultAsync();

        if (booking!.Status == BookingStatus.PendingPayment && booking.ExpiresAt > DateTime.UtcNow)
        {
            var bUpdate = Builders<Booking>.Update
                .Set(x => x.Status, BookingStatus.Confirmed)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await bookings.UpdateOneAsync(x => x.Id == booking.Id && x.Status == BookingStatus.PendingPayment, bUpdate);
        }

        return Results.Ok(new { ok = true, message = "Payment marked paid; booking confirmed if eligible." });
    }

    if (normalized == "failed")
    {
        var payUpdate = Builders<Payment>.Update
            .Set(x => x.Status, PaymentStatus.Failed)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await payments.UpdateOneAsync(x => x.Id == payment.Id, payUpdate);
        return Results.Ok(new { ok = true, message = "Payment marked failed." });
    }

    return Results.BadRequest("Status must be 'paid' or 'failed'.");
});


app.Run();
