using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Text.Json.Serialization;
using Travel.Api.Dtos;
using Travel.Api.Models;
using BCrypt.Net;
using Microsoft.OpenApi.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

// Register MongoDB class maps FIRST, before any MongoDB operations or builder creation
// Note: Tour class also has [BsonIgnoreExtraElements] attribute for double protection
if (!BsonClassMap.IsClassMapRegistered(typeof(Tour)))
{
    BsonClassMap.RegisterClassMap<Tour>(cm =>
    {
        cm.AutoMap();
        cm.SetIgnoreExtraElements(true); // Ignore extra fields in DB that aren't in model
    });
    Console.WriteLine("[Startup] Tour BsonClassMap registered with IgnoreExtraElements");
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Travel API", 
        Version = "v1",
        Description = "Travel booking system API"
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",  // Angular dev server
                "https://steppia-travel.netlify.app"  // Production frontend (Netlify)
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Mongo - Read from environment variables (Railway/Production) or appsettings.json (Development)
// Environment variable format: MONGO__CONNECTIONSTRING or MONGO_CONNECTIONSTRING
var mongoConn = Environment.GetEnvironmentVariable("MONGO_CONNECTIONSTRING")
    ?? Environment.GetEnvironmentVariable("MONGO__CONNECTIONSTRING")
    ?? builder.Configuration["Mongo:ConnectionString"];

if (string.IsNullOrWhiteSpace(mongoConn))
{
    throw new InvalidOperationException(
        "MongoDB connection string is required. " +
        $"MONGO_CONNECTIONSTRING env var: {(Environment.GetEnvironmentVariable("MONGO_CONNECTIONSTRING") != null ? "SET" : "NOT SET")}, " +
        $"appsettings value: {(string.IsNullOrWhiteSpace(builder.Configuration["Mongo:ConnectionString"]) ? "EMPTY" : "SET")}");
}

// Log connection string (masked for security)
var maskedConn = mongoConn.Length > 20 
    ? mongoConn.Substring(0, 20) + "..." 
    : "***";
Console.WriteLine($"[Startup] MongoDB connection string: {maskedConn} (length: {mongoConn.Length})");

var mongoDbName = Environment.GetEnvironmentVariable("MONGO_DATABASENAME")
    ?? Environment.GetEnvironmentVariable("MONGO__DATABASENAME")
    ?? builder.Configuration["Mongo:DatabaseName"]
    ?? "travel_db";

// Configure MongoDB client with proper SSL/TLS settings
var mongoClientSettings = MongoClientSettings.FromConnectionString(mongoConn);
mongoClientSettings.ServerApi = new ServerApi(ServerApiVersion.V1);

// Configure SSL/TLS for Railway environment
mongoClientSettings.SslSettings = new SslSettings
{
    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
    CheckCertificateRevocation = false // Railway environment may have certificate issues
};

// Increase connection timeout for Railway
mongoClientSettings.ConnectTimeout = TimeSpan.FromSeconds(30);
mongoClientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);

Console.WriteLine("[Startup] MongoDB client configured with SSL/TLS");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoClientSettings));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDbName);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// JWT Configuration
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT_SECRET environment variable is required");

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? builder.Configuration["Jwt:Issuer"]
    ?? "steppia-travel-api";

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? builder.Configuration["Jwt:Audience"]
    ?? "steppia-travel-admin";

var key = Encoding.UTF8.GetBytes(jwtSecret);
if (key.Length < 32)
{
    throw new InvalidOperationException("JWT_SECRET must be at least 32 characters long");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

Console.WriteLine("[Startup] JWT authentication configured");

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

// Helper function to generate JWT token
static string GenerateJwtToken(string username, string role, string jwtSecret, string jwtIssuer, string jwtAudience)
{
    var key = Encoding.UTF8.GetBytes(jwtSecret);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        }),
        Expires = DateTime.UtcNow.AddHours(8), // 8 hour expiration
        Issuer = jwtIssuer,
        Audience = jwtAudience,
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var tokenHandler = new JwtSecurityTokenHandler();
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}

// Send 6-digit confirmation code to email (uses SMTP from config/env; if not configured, logs code to console)
static async Task SendConfirmationEmailAsync(IConfiguration config, string toEmail, string code)
{
    var host = config["Smtp:Host"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
    if (string.IsNullOrWhiteSpace(host))
    {
        Console.WriteLine($"[Email] SMTP not configured. Confirmation code for {toEmail}: {code}");
        return;
    }
    var port = int.TryParse(config["Smtp:Port"] ?? Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
    var user = config["Smtp:UserName"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
    var password = config["Smtp:Password"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
    var fromEmail = config["Smtp:FromEmail"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? "noreply@example.com";
    var fromName = config["Smtp:FromName"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Steppia Travel";
    var useSsl = string.Equals(config["Smtp:EnableSsl"] ?? Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL") ?? "true", "true", StringComparison.OrdinalIgnoreCase);

    var message = new MimeMessage();
    message.From.Add(new MailboxAddress(fromName, fromEmail));
    message.To.Add(new MailboxAddress(toEmail, toEmail));
    message.Subject = "Your confirmation code";
    message.Body = new TextPart("plain")
    {
        Text = $"Your confirmation code is: {code}. It expires in 15 minutes."
    };

    try
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            await client.AuthenticateAsync(user, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        Console.WriteLine($"[Email] Confirmation code sent to {toEmail}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Email] Failed to send confirmation to {toEmail}: {ex.Message}");
    }
}

// Initialize admin user if collection is empty
static async Task InitializeAdminUser(IServiceProvider services)
{
    try
    {
        var db = services.GetRequiredService<IMongoDatabase>();
        var admins = db.GetCollection<Admin>("admins");
        
        var adminCount = await admins.CountDocumentsAsync(FilterDefinition<Admin>.Empty);
        if (adminCount == 0)
        {
            var adminUsername = Environment.GetEnvironmentVariable("ADMIN_USERNAME") ?? "admin";
            var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "admin123";
            
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
            var admin = new Admin
            {
                Username = adminUsername,
                PasswordHash = passwordHash,
                Role = "admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await admins.InsertOneAsync(admin);
            Console.WriteLine($"[Startup] Created default admin user: {adminUsername}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Warning] Failed to initialize admin user: {ex.Message}");
    }
}

// Configure port for Railway - Railway sets PORT env var to match the service port
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port) && int.TryParse(port, out var portNumber))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
    Console.WriteLine($"[Startup] Configured to listen on port {portNumber} (from PORT env var)");
}
else
{
    // Fallback: try to use Railway's default or common ports
    var defaultPort = 8000; // Railway production default
    builder.WebHost.UseUrls($"http://0.0.0.0:{defaultPort}");
    Console.WriteLine($"[Startup] PORT environment variable not set. Using default port {defaultPort}");
    Console.WriteLine($"[Startup] WARNING: Make sure Railway PORT env var matches your service port (8000 for production, 8100 for test)");
}

var app = builder.Build();

Console.WriteLine($"[Startup] Application built successfully. Listening on: {string.Join(", ", app.Urls)}");

// Initialize admin user if collection is empty
await InitializeAdminUser(app.Services);

// Middleware order (IMPORTANT: CORS must be first)
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();

// 1) Mongo health check
app.MapGet("/health/mongo", async (IMongoDatabase db) =>
{
    try
    {
        var result = await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        return Results.Ok(new { ok = true, result = result.ToString() });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"MongoDB connection failed: {ex.Message}",
            statusCode: 503
        );
    }
});

// 2) List tours with pagination and filtering
app.MapGet("/tours", async (
    IMongoDatabase db,
    int page = 1,
    int pageSize = 20,
    string? search = null,
    string? type = null,
    decimal? minPrice = null,
    decimal? maxPrice = null,
    int? minDuration = null,
    int? maxDuration = null
) =>
{
    try
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Max page size limit

        var col = db.GetCollection<Tour>("tours");
        
        // Build filter
        var filter = Builders<Tour>.Filter.Eq(t => t.IsActive, true);

        // Search filter (title, summary, description)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLowerInvariant();
            var searchFilter = Builders<Tour>.Filter.Or(
                Builders<Tour>.Filter.Regex(t => t.Title, new BsonRegularExpression(search, "i")),
                Builders<Tour>.Filter.Regex(t => t.Summary, new BsonRegularExpression(search, "i")),
                Builders<Tour>.Filter.Regex(t => t.Description, new BsonRegularExpression(search, "i"))
            );
            filter = Builders<Tour>.Filter.And(filter, searchFilter);
        }

        // Type filter
        if (!string.IsNullOrWhiteSpace(type))
        {
            var typeFilter = Builders<Tour>.Filter.Eq(t => t.Type, type);
            filter = Builders<Tour>.Filter.And(filter, typeFilter);
        }

        // Price filters
        if (minPrice.HasValue)
        {
            var minPriceFilter = Builders<Tour>.Filter.Gte(t => t.BasePrice, minPrice.Value);
            filter = Builders<Tour>.Filter.And(filter, minPriceFilter);
        }

        if (maxPrice.HasValue)
        {
            var maxPriceFilter = Builders<Tour>.Filter.Lte(t => t.BasePrice, maxPrice.Value);
            filter = Builders<Tour>.Filter.And(filter, maxPriceFilter);
        }

        // Duration filters
        if (minDuration.HasValue)
        {
            var minDurationFilter = Builders<Tour>.Filter.Gte(t => t.DurationDays, minDuration.Value);
            filter = Builders<Tour>.Filter.And(filter, minDurationFilter);
        }

        if (maxDuration.HasValue)
        {
            var maxDurationFilter = Builders<Tour>.Filter.Lte(t => t.DurationDays, maxDuration.Value);
            filter = Builders<Tour>.Filter.And(filter, maxDurationFilter);
        }

        // Get total count for pagination
        var total = await col.CountDocumentsAsync(filter);

        // Apply pagination
        var skip = (page - 1) * pageSize;
        var list = await col.Find(filter)
            .SortByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        Console.WriteLine($"[Info] Found {list.Count} active tours (page {page}, total: {total})");

        // Map to DTO with full tour details
        var dto = new List<TourDto>();
        foreach (var t in list)
        {
            try
            {
                dto.Add(new TourDto(
                    Id: t.Id.ToString(),
                    Slug: t.Slug ?? string.Empty,
                    Title: t.Title ?? string.Empty,
                    Type: t.Type ?? string.Empty,
                    Summary: t.Summary,
                    Description: t.Description,
                    DurationDays: t.DurationDays,
                    Nights: t.Nights,
                    Region: t.Region,
                    TotalDistanceKm: t.TotalDistanceKm,
                    TravelStyle: t.TravelStyle,
                    Highlights: t.Highlights,
                    Accommodation: t.Accommodation,
                    Itinerary: t.Itinerary,
                    Activities: t.Activities,
                    IdealFor: t.IdealFor,
                    BasePrice: t.BasePrice,
                    Currency: t.Currency ?? "USD",
                    Locations: (t.Locations ?? new List<TourLocation>()).Select(loc => new TourLocationDto(loc.Name, loc.Latitude, loc.Longitude)).ToList(),
                    Images: t.Images?.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList()
                            ?? new List<TourImageDto>(),
                    BobbleTitle: t.BobbleTitle
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to map tour {t.Id}: {ex.Message}");
                // Skip this tour and continue
            }
        }

        return Results.Ok(new
        {
            data = dto,
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] /tours endpoint failed: {ex.Message}");
        Console.WriteLine($"[Error] Stack trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"[Error] Inner exception: {ex.InnerException.Message}");
        }
        return Results.Problem(
            detail: $"Error fetching tours: {ex.Message}",
            statusCode: 500
        );
    }
});

app.MapGet("/tours/{slug}", async (string slug, IMongoDatabase db) =>
{
    var col = db.GetCollection<Tour>("tours");
    var tour = await col.Find(t => t.Slug == slug && t.IsActive).FirstOrDefaultAsync();
    return tour is null ? Results.NotFound() : Results.Ok(tour);
});

// Get available dates for a tour (public)
app.MapGet("/tours/{slug}/dates", async (string slug, IMongoDatabase db) =>
{
    try
    {
        var tours = db.GetCollection<Tour>("tours");
        var tour = await tours.Find(t => t.Slug == slug && t.IsActive).FirstOrDefaultAsync();
        
        if (tour is null)
            return Results.NotFound("Tour not found.");

        var tourDates = db.GetCollection<TourDate>("tour_dates");
        var bookings = db.GetCollection<Booking>("bookings");
        
        // Get all open tour dates for this tour that are in the future
        var now = DateTime.UtcNow;
        var openDates = await tourDates.Find(d => 
            d.TourId == tour.Id && 
            d.Status == TourDateStatus.Open && 
            d.StartDate >= now
        ).SortBy(d => d.StartDate).ToListAsync();

        var result = new List<PublicTourDateDto>();
        
        foreach (var date in openDates)
        {
            // Calculate available spots: capacity - (pending + confirmed bookings)
            var heldOrConfirmed = Builders<Booking>.Filter.And(
                Builders<Booking>.Filter.Eq(x => x.TourDateId, date.Id),
                Builders<Booking>.Filter.In(x => x.Status, new[] { BookingStatus.PendingPayment, BookingStatus.Confirmed }),
                Builders<Booking>.Filter.Gt(x => x.ExpiresAt, now) // pending holds that haven't expired
            );

            var seatsUsed = await bookings.CountDocumentsAsync(heldOrConfirmed);
            var availableSpots = date.Capacity - (int)seatsUsed;

            // Only include dates with available spots
            if (availableSpots > 0)
            {
                // Use price override if available, otherwise use tour base price
                var price = date.PriceOverride ?? tour.BasePrice;
                
                result.Add(new PublicTourDateDto(
                    date.Id.ToString(),
                    date.StartDate,
                    date.EndDate,
                    availableSpots,
                    price,
                    tour.Currency ?? "USD"
                ));
            }
        }

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] GET /tours/{slug}/dates failed: {ex.Message}");
        return Results.Problem(
            detail: $"Error fetching tour dates: {ex.Message}",
            statusCode: 500
        );
    }
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
    var tours = db.GetCollection<Tour>("tours");
    
    var b = await bookings.Find(x => x.BookingCode == bookingCode).FirstOrDefaultAsync();
    if (b is null) return Results.NotFound();

    await ExpireBookingIfNeeded(bookings, b);

    // re-read after potential expire
    b = await bookings.Find(x => x.BookingCode == bookingCode).FirstOrDefaultAsync();
    
    // Fetch tour details
    var tour = await tours.Find(t => t.Id == b!.TourId).FirstOrDefaultAsync();
    object? tourInfo = null;
    if (tour is not null)
    {
        var imagesList = new List<object>();
        if (tour.Images != null && tour.Images.Count > 0)
        {
            foreach (var img in tour.Images)
            {
                imagesList.Add(new { img.Url, img.Alt, img.IsCover });
            }
        }
        
        tourInfo = new
        {
            id = tour.Id.ToString(),
            title = tour.Title,
            slug = tour.Slug,
            basePrice = tour.BasePrice,
            currency = tour.Currency,
            images = imagesList
        };
    }

    return Results.Ok(new
    {
        id = b!.Id.ToString(),
        b.BookingCode,
        b.Status,
        b.ExpiresAt,
        b.TourId,
        tour = tourInfo,
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

// ========== UNIFIED LOGIN (admin or user – same URL; response Role decides admin vs user section) ==========

app.MapPost("/auth/login", async (UnifiedLoginRequest req, IMongoDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(req.Login)) return Results.BadRequest("Login is required.");
    if (string.IsNullOrWhiteSpace(req.Password)) return Results.BadRequest("Password is required.");

    var login = req.Login.Trim();
    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET not set");
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "steppia-travel-api";
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "steppia-travel-admin";
    var expiresAt = DateTime.UtcNow.AddHours(8);

    // If login contains '@', treat as user (email); otherwise treat as admin (username)
    if (login.Contains('@'))
    {
        var email = login.ToLowerInvariant();
        var users = db.GetCollection<User>("users");
        var user = await users.Find(u => u.Email == email && u.IsActive).FirstOrDefaultAsync();
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Results.Unauthorized();

        var update = Builders<User>.Update
            .Set(x => x.LastLoginAt, DateTime.UtcNow)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        await users.UpdateOneAsync(u => u.Id == user.Id, update);

        var token = GenerateJwtToken(user.Email, user.Role, jwtSecret, jwtIssuer, jwtAudience);
        return Results.Ok(new UnifiedLoginResponse(
            token, expiresAt, user.Role,
            UserId: user.Id.ToString(),
            Username: null,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Phone: user.Phone
        ));
    }
    else
    {
        var admins = db.GetCollection<Admin>("admins");
        var admin = await admins.Find(a => a.Username == login && a.IsActive).FirstOrDefaultAsync();
        if (admin is null || !BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash))
            return Results.Unauthorized();

        var update = Builders<Admin>.Update.Set(x => x.LastLoginAt, DateTime.UtcNow);
        await admins.UpdateOneAsync(a => a.Id == admin.Id, update);

        var token = GenerateJwtToken(admin.Username, admin.Role, jwtSecret, jwtIssuer, jwtAudience);
        return Results.Ok(new UnifiedLoginResponse(
            token, expiresAt, admin.Role,
            UserId: null,
            Username: admin.Username,
            Email: admin.Email,
            FirstName: null,
            LastName: null,
            Phone: null
        ));
    }
}).AllowAnonymous();

// ========== USER (CUSTOMER) AUTHENTICATION ENDPOINTS ==========

// User Register – sends 6-digit confirmation code to email; user is created only after confirm-email
app.MapPost("/user/register", async (UserRegisterRequest req, IMongoDatabase db, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(req.Email)) return Results.BadRequest("Email is required.");
    if (string.IsNullOrWhiteSpace(req.FirstName)) return Results.BadRequest("First name is required.");
    if (string.IsNullOrWhiteSpace(req.LastName)) return Results.BadRequest("Last name is required.");
    if (string.IsNullOrWhiteSpace(req.Password)) return Results.BadRequest("Password is required.");
    if (req.Password.Length < 6) return Results.BadRequest("Password must be at least 6 characters.");

    var email = req.Email.Trim().ToLowerInvariant();
    var users = db.GetCollection<User>("users");
    var existing = await users.Find(u => u.Email == email).FirstOrDefaultAsync();
    if (existing is not null)
        return Results.Conflict("Email already registered.");

    var pendingCol = db.GetCollection<PendingRegistration>("pending_registrations");
    await pendingCol.DeleteManyAsync(p => p.Email == email);

    var code = Random.Shared.Next(100000, 999999).ToString();
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password.Trim());
    var expiresAt = DateTime.UtcNow.AddMinutes(15);
    var pending = new PendingRegistration
    {
        Email = email,
        FirstName = req.FirstName.Trim(),
        LastName = req.LastName.Trim(),
        Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
        PasswordHash = passwordHash,
        Code = code,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = expiresAt
    };
    await pendingCol.InsertOneAsync(pending);

    await SendConfirmationEmailAsync(config, email, code);

    return Results.Ok(new { message = "Confirmation code sent to your email. Check your inbox and confirm with the 6-digit code.", expiresInMinutes = 15 });
}).AllowAnonymous();

// User Confirm Email – match 6-digit code, then create user and return token
app.MapPost("/user/confirm-email", async (ConfirmEmailRequest req, IMongoDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(req.Email)) return Results.BadRequest("Email is required.");
    if (string.IsNullOrWhiteSpace(req.Code)) return Results.BadRequest("Code is required.");
    var code = req.Code.Trim();
    if (code.Length != 6 || !code.All(char.IsDigit)) return Results.BadRequest("Code must be 6 digits.");

    var email = req.Email.Trim().ToLowerInvariant();
    var pendingCol = db.GetCollection<PendingRegistration>("pending_registrations");
    var pending = await pendingCol.Find(p => p.Email == email).FirstOrDefaultAsync();
    if (pending is null)
        return Results.NotFound("No pending registration for this email. Please register first.");
    if (pending.ExpiresAt < DateTime.UtcNow)
    {
        await pendingCol.DeleteOneAsync(p => p.Id == pending.Id);
        return Results.BadRequest("Confirmation code expired. Please register again.");
    }
    if (pending.Code != code)
        return Results.BadRequest("Invalid confirmation code.");

    var users = db.GetCollection<User>("users");
    var user = new User
    {
        Email = pending.Email,
        FirstName = pending.FirstName,
        LastName = pending.LastName,
        Phone = pending.Phone,
        PasswordHash = pending.PasswordHash,
        Role = "user",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    await users.InsertOneAsync(user);
    await pendingCol.DeleteOneAsync(p => p.Id == pending.Id);

    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET not set");
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "steppia-travel-api";
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "steppia-travel-admin";
    var token = GenerateJwtToken(user.Email, user.Role, jwtSecret, jwtIssuer, jwtAudience);

    return Results.Ok(new UserAuthResponse(
        token,
        user.Id.ToString(),
        user.Email,
        user.FirstName,
        user.LastName,
        user.Phone,
        user.Role,
        DateTime.UtcNow.AddHours(8)
    ));
}).AllowAnonymous();

// User Login
app.MapPost("/user/login", async (UserLoginRequest req, IMongoDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(req.Email)) return Results.BadRequest("Email is required.");
    if (string.IsNullOrWhiteSpace(req.Password)) return Results.BadRequest("Password is required.");

    var email = req.Email.Trim().ToLowerInvariant();
    var users = db.GetCollection<User>("users");
    var user = await users.Find(u => u.Email == email && u.IsActive).FirstOrDefaultAsync();
    if (user is null)
        return Results.Unauthorized();

    if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var update = Builders<User>.Update
        .Set(x => x.LastLoginAt, DateTime.UtcNow)
        .Set(x => x.UpdatedAt, DateTime.UtcNow);
    await users.UpdateOneAsync(u => u.Id == user.Id, update);

    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET not set");
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "steppia-travel-api";
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "steppia-travel-admin";
    var token = GenerateJwtToken(user.Email, user.Role, jwtSecret, jwtIssuer, jwtAudience);

    return Results.Ok(new UserAuthResponse(
        token,
        user.Id.ToString(),
        user.Email,
        user.FirstName,
        user.LastName,
        user.Phone,
        user.Role,
        DateTime.UtcNow.AddHours(8)
    ));
}).AllowAnonymous();

// ========== ADMIN AUTHENTICATION ENDPOINTS ==========

// Admin Login
app.MapPost("/admin/auth/login", async (LoginRequest req, IMongoDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest("Username and password are required.");

    var admins = db.GetCollection<Admin>("admins");
    var admin = await admins.Find(a => a.Username == req.Username && a.IsActive).FirstOrDefaultAsync();

    if (admin is null)
        return Results.Unauthorized();

    if (!BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash))
        return Results.Unauthorized();

    // Update last login
    var update = Builders<Admin>.Update.Set(x => x.LastLoginAt, DateTime.UtcNow);
    await admins.UpdateOneAsync(a => a.Id == admin.Id, update);

    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET not set");
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "steppia-travel-api";
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "steppia-travel-admin";

    var token = GenerateJwtToken(admin.Username, admin.Role, jwtSecret, jwtIssuer, jwtAudience);

    return Results.Ok(new LoginResponse(
        token,
        admin.Username,
        admin.Role,
        DateTime.UtcNow.AddHours(8)
    ));
}).AllowAnonymous();

// Get current admin user
app.MapGet("/admin/auth/me", async (ClaimsPrincipal user, IMongoDatabase db) =>
{
    var username = user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(username))
        return Results.Unauthorized();

    var admins = db.GetCollection<Admin>("admins");
    var admin = await admins.Find(a => a.Username == username && a.IsActive).FirstOrDefaultAsync();

    if (admin is null)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        username = admin.Username,
        email = admin.Email,
        role = admin.Role,
        lastLoginAt = admin.LastLoginAt
    });
}).RequireAuthorization("AdminOnly");

// Refresh token (same as login, returns new token)
app.MapPost("/admin/auth/refresh", async (ClaimsPrincipal user, IMongoDatabase db) =>
{
    var username = user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(username))
        return Results.Unauthorized();

    var admins = db.GetCollection<Admin>("admins");
    var admin = await admins.Find(a => a.Username == username && a.IsActive).FirstOrDefaultAsync();

    if (admin is null)
        return Results.Unauthorized();

    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET not set");
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "steppia-travel-api";
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "steppia-travel-admin";

    var token = GenerateJwtToken(admin.Username, admin.Role, jwtSecret, jwtIssuer, jwtAudience);

    return Results.Ok(new LoginResponse(
        token,
        admin.Username,
        admin.Role,
        DateTime.UtcNow.AddHours(8)
    ));
}).RequireAuthorization("AdminOnly");

// Logout (client-side token removal, no server action needed)
app.MapPost("/admin/auth/logout", () =>
{
    return Results.Ok(new { message = "Logged out successfully" });
}).RequireAuthorization("AdminOnly");

// Change password
app.MapPut("/admin/auth/change-password", async (ChangePasswordRequest req, ClaimsPrincipal user, IMongoDatabase db) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            return Results.BadRequest("Current password and new password are required.");

        if (req.NewPassword.Length < 6)
            return Results.BadRequest("New password must be at least 6 characters long.");

        var username = user.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Results.Unauthorized();

        var admins = db.GetCollection<Admin>("admins");
        var admin = await admins.Find(a => a.Username == username && a.IsActive).FirstOrDefaultAsync();

        if (admin is null)
            return Results.Unauthorized();

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, admin.PasswordHash))
            return Results.BadRequest("Current password is incorrect.");

        // Hash new password
        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);

        // Update password
        var update = Builders<Admin>.Update
            .Set(x => x.PasswordHash, newPasswordHash)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await admins.UpdateOneAsync(a => a.Id == admin.Id, update);

        return Results.Ok(new { message = "Password changed successfully" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] PUT /admin/auth/change-password failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// ========== ADMIN TOURS CRUD ENDPOINTS ==========

// Get all tours (including inactive) with pagination
app.MapGet("/admin/tours", async (IMongoDatabase db, int page = 1, int pageSize = 20) =>
{
    try
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Max page size limit

        var col = db.GetCollection<Tour>("tours");
        
        // Get total count
        var total = await col.CountDocumentsAsync(FilterDefinition<Tour>.Empty);

        // Apply pagination
        var skip = (page - 1) * pageSize;
        var list = await col.Find(_ => true)
            .SortByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        var dto = list.Select(t => new AdminTourDto(
            t.Id.ToString(),
            t.Slug ?? string.Empty,
            t.Title ?? string.Empty,
            t.Type ?? string.Empty,
            t.Summary,
            t.Description,
            t.DurationDays,
            t.BasePrice,
            t.Currency ?? "USD",
            (t.Locations ?? new List<TourLocation>()).Select(loc => new TourLocationDto(loc.Name, loc.Latitude, loc.Longitude)).ToList(),
            t.Images?.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList() ?? new List<TourImageDto>(),
            t.IsActive,
            t.CreatedAt,
            t.UpdatedAt
        )).ToList();

        return Results.Ok(new
        {
            data = dto,
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] /admin/tours failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Create new tour
app.MapPost("/admin/tours", async (CreateTourRequest req, IMongoDatabase db) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(req.Slug)) return Results.BadRequest("Slug is required.");
        if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(req.Type)) return Results.BadRequest("Type is required.");

        var col = db.GetCollection<Tour>("tours");
        
        // Check slug uniqueness
        var existing = await col.Find(t => t.Slug == req.Slug).FirstOrDefaultAsync();
        if (existing is not null)
            return Results.BadRequest("Tour with this slug already exists.");

        var tour = new Tour
        {
            Slug = req.Slug.Trim(),
            Title = req.Title.Trim(),
            Type = req.Type.Trim(),
            Summary = req.Summary,
            Description = req.Description,
            DurationDays = req.DurationDays,
            BasePrice = req.BasePrice,
            Currency = req.Currency ?? "USD",
            Locations = (req.Locations ?? new List<TourLocationRequest>()).Select(loc => new TourLocation { Name = loc.Name ?? "", Latitude = loc.Latitude, Longitude = loc.Longitude }).ToList(),
            Images = req.Images?.Select(img => new TourImage
            {
                Url = img.Url,
                Alt = img.Alt,
                IsCover = img.IsCover
            }).ToList() ?? new List<TourImage>(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await col.InsertOneAsync(tour);

        var dto = new AdminTourDto(
            tour.Id.ToString(),
            tour.Slug,
            tour.Title,
            tour.Type,
            tour.Summary,
            tour.Description,
            tour.DurationDays,
            tour.BasePrice,
            tour.Currency,
            (tour.Locations ?? new List<TourLocation>()).Select(loc => new TourLocationDto(loc.Name, loc.Latitude, loc.Longitude)).ToList(),
            tour.Images.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList(),
            tour.IsActive,
            tour.CreatedAt,
            tour.UpdatedAt
        );

        return Results.Created($"/admin/tours/{tour.Id}", dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] POST /admin/tours failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Update tour
app.MapPut("/admin/tours/{id}", async (string id, UpdateTourRequest req, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(id, out var tourId))
            return Results.BadRequest("Invalid tour ID.");

        var col = db.GetCollection<Tour>("tours");
        var tour = await col.Find(t => t.Id == tourId).FirstOrDefaultAsync();
        
        if (tour is null)
            return Results.NotFound();

        // Check slug uniqueness if slug is being updated
        if (!string.IsNullOrWhiteSpace(req.Slug) && req.Slug != tour.Slug)
        {
            var existing = await col.Find(t => t.Slug == req.Slug && t.Id != tourId).FirstOrDefaultAsync();
            if (existing is not null)
                return Results.BadRequest("Tour with this slug already exists.");
        }

        var update = Builders<Tour>.Update
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(req.Slug))
            update = update.Set(x => x.Slug, req.Slug.Trim());
        if (!string.IsNullOrWhiteSpace(req.Title))
            update = update.Set(x => x.Title, req.Title.Trim());
        if (!string.IsNullOrWhiteSpace(req.Type))
            update = update.Set(x => x.Type, req.Type.Trim());
        if (req.Summary is not null)
            update = update.Set(x => x.Summary, req.Summary);
        if (req.Description is not null)
            update = update.Set(x => x.Description, req.Description);
        if (req.DurationDays.HasValue)
            update = update.Set(x => x.DurationDays, req.DurationDays.Value);
        if (req.BasePrice.HasValue)
            update = update.Set(x => x.BasePrice, req.BasePrice.Value);
        if (!string.IsNullOrWhiteSpace(req.Currency))
            update = update.Set(x => x.Currency, req.Currency);
        if (req.Locations is not null)
            update = update.Set(x => x.Locations, req.Locations.Select(loc => new TourLocation { Name = loc.Name ?? "", Latitude = loc.Latitude, Longitude = loc.Longitude }).ToList());
        if (req.Images is not null)
            update = update.Set(x => x.Images, req.Images.Select(img => new TourImage
            {
                Url = img.Url,
                Alt = img.Alt,
                IsCover = img.IsCover
            }).ToList());
        if (req.IsActive.HasValue)
            update = update.Set(x => x.IsActive, req.IsActive.Value);

        await col.UpdateOneAsync(t => t.Id == tourId, update);

        // Fetch updated tour
        tour = await col.Find(t => t.Id == tourId).FirstOrDefaultAsync();
        var dto = new AdminTourDto(
            tour!.Id.ToString(),
            tour.Slug ?? string.Empty,
            tour.Title ?? string.Empty,
            tour.Type ?? string.Empty,
            tour.Summary,
            tour.Description,
            tour.DurationDays,
            tour.BasePrice,
            tour.Currency ?? "USD",
            (tour.Locations ?? new List<TourLocation>()).Select(loc => new TourLocationDto(loc.Name, loc.Latitude, loc.Longitude)).ToList(),
            tour.Images?.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList() ?? new List<TourImageDto>(),
            tour.IsActive,
            tour.CreatedAt,
            tour.UpdatedAt
        );

        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] PUT /admin/tours/{id} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Delete tour (soft delete - set isActive=false)
app.MapDelete("/admin/tours/{id}", async (string id, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(id, out var tourId))
            return Results.BadRequest("Invalid tour ID.");

        var col = db.GetCollection<Tour>("tours");
        var tour = await col.Find(t => t.Id == tourId).FirstOrDefaultAsync();
        
        if (tour is null)
            return Results.NotFound();

        var update = Builders<Tour>.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await col.UpdateOneAsync(t => t.Id == tourId, update);

        return Results.Ok(new { message = "Tour deleted successfully" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] DELETE /admin/tours/{id} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// ========== ADMIN TOUR DATES MANAGEMENT ENDPOINTS ==========

// Get tour dates for a tour
app.MapGet("/admin/tours/{tourId}/dates", async (string tourId, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(tourId, out var tourObjId))
            return Results.BadRequest("Invalid tour ID.");

        var tours = db.GetCollection<Tour>("tours");
        var tour = await tours.Find(t => t.Id == tourObjId).FirstOrDefaultAsync();
        
        if (tour is null)
            return Results.NotFound("Tour not found.");

        var tourDates = db.GetCollection<TourDate>("tour_dates");
        var dates = await tourDates.Find(d => d.TourId == tourObjId)
            .SortBy(d => d.StartDate)
            .ToListAsync();

        var dto = dates.Select(d => new TourDateDto(
            d.Id.ToString(),
            d.TourId.ToString(),
            d.StartDate,
            d.EndDate,
            d.Capacity,
            d.PriceOverride,
            d.Status.ToString(),
            d.CreatedAt,
            d.UpdatedAt
        )).ToList();

        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] GET /admin/tours/{tourId}/dates failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Create tour date
app.MapPost("/admin/tours/{tourId}/dates", async (string tourId, CreateTourDateRequest req, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(tourId, out var tourObjId))
            return Results.BadRequest("Invalid tour ID.");

        var tours = db.GetCollection<Tour>("tours");
        var tour = await tours.Find(t => t.Id == tourObjId).FirstOrDefaultAsync();
        
        if (tour is null)
            return Results.NotFound("Tour not found.");

        if (req.StartDate >= req.EndDate)
            return Results.BadRequest("StartDate must be before EndDate.");

        if (req.Capacity <= 0)
            return Results.BadRequest("Capacity must be greater than 0.");

        TourDateStatus status = TourDateStatus.Open;
        if (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<TourDateStatus>(req.Status, true, out var parsedStatus))
            status = parsedStatus;

        var tourDates = db.GetCollection<TourDate>("tour_dates");
        var tourDate = new TourDate
        {
            TourId = tourObjId,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Capacity = req.Capacity,
            PriceOverride = req.PriceOverride,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await tourDates.InsertOneAsync(tourDate);

        var dto = new TourDateDto(
            tourDate.Id.ToString(),
            tourDate.TourId.ToString(),
            tourDate.StartDate,
            tourDate.EndDate,
            tourDate.Capacity,
            tourDate.PriceOverride,
            tourDate.Status.ToString(),
            tourDate.CreatedAt,
            tourDate.UpdatedAt
        );

        return Results.Created($"/admin/tour-dates/{tourDate.Id}", dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] POST /admin/tours/{tourId}/dates failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Update tour date
app.MapPut("/admin/tour-dates/{id}", async (string id, UpdateTourDateRequest req, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(id, out var tourDateId))
            return Results.BadRequest("Invalid tour date ID.");

        var tourDates = db.GetCollection<TourDate>("tour_dates");
        var tourDate = await tourDates.Find(d => d.Id == tourDateId).FirstOrDefaultAsync();
        
        if (tourDate is null)
            return Results.NotFound("Tour date not found.");

        // Validate date range if both dates are being updated
        if (req.StartDate.HasValue && req.EndDate.HasValue && req.StartDate.Value >= req.EndDate.Value)
            return Results.BadRequest("StartDate must be before EndDate.");

        if (req.StartDate.HasValue && !req.EndDate.HasValue && req.StartDate.Value >= tourDate.EndDate)
            return Results.BadRequest("StartDate must be before EndDate.");

        if (!req.StartDate.HasValue && req.EndDate.HasValue && tourDate.StartDate >= req.EndDate.Value)
            return Results.BadRequest("StartDate must be before EndDate.");

        if (req.Capacity.HasValue && req.Capacity.Value <= 0)
            return Results.BadRequest("Capacity must be greater than 0.");

        var update = Builders<TourDate>.Update
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        if (req.StartDate.HasValue)
            update = update.Set(x => x.StartDate, req.StartDate.Value);
        if (req.EndDate.HasValue)
            update = update.Set(x => x.EndDate, req.EndDate.Value);
        if (req.Capacity.HasValue)
            update = update.Set(x => x.Capacity, req.Capacity.Value);
        if (req.PriceOverride.HasValue)
            update = update.Set(x => x.PriceOverride, req.PriceOverride.Value);
        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            if (Enum.TryParse<TourDateStatus>(req.Status, true, out var newStatus))
                update = update.Set(x => x.Status, newStatus);
            else
                return Results.BadRequest("Invalid status. Must be: Open, SoldOut, or Closed.");
        }

        await tourDates.UpdateOneAsync(d => d.Id == tourDateId, update);

        // Fetch updated tour date
        tourDate = await tourDates.Find(d => d.Id == tourDateId).FirstOrDefaultAsync();
        var dto = new TourDateDto(
            tourDate!.Id.ToString(),
            tourDate.TourId.ToString(),
            tourDate.StartDate,
            tourDate.EndDate,
            tourDate.Capacity,
            tourDate.PriceOverride,
            tourDate.Status.ToString(),
            tourDate.CreatedAt,
            tourDate.UpdatedAt
        );

        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] PUT /admin/tour-dates/{id} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Delete tour date
app.MapDelete("/admin/tour-dates/{id}", async (string id, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(id, out var tourDateId))
            return Results.BadRequest("Invalid tour date ID.");

        var tourDates = db.GetCollection<TourDate>("tour_dates");
        var tourDate = await tourDates.Find(d => d.Id == tourDateId).FirstOrDefaultAsync();
        
        if (tourDate is null)
            return Results.NotFound("Tour date not found.");

        // Check if there are any bookings for this tour date
        var bookings = db.GetCollection<Booking>("bookings");
        var bookingCount = await bookings.CountDocumentsAsync(b => b.TourDateId == tourDateId);
        
        if (bookingCount > 0)
            return Results.BadRequest($"Cannot delete tour date. There are {bookingCount} booking(s) associated with this tour date.");

        await tourDates.DeleteOneAsync(d => d.Id == tourDateId);

        return Results.Ok(new { message = "Tour date deleted successfully" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] DELETE /admin/tour-dates/{id} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Get tour date by ID
app.MapGet("/admin/tour-dates/{id}", async (string id, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(id, out var tourDateId))
            return Results.BadRequest("Invalid tour date ID.");

        var tourDates = db.GetCollection<TourDate>("tour_dates");
        var tourDate = await tourDates.Find(d => d.Id == tourDateId).FirstOrDefaultAsync();
        
        if (tourDate is null)
            return Results.NotFound("Tour date not found.");

        var dto = new TourDateDto(
            tourDate.Id.ToString(),
            tourDate.TourId.ToString(),
            tourDate.StartDate,
            tourDate.EndDate,
            tourDate.Capacity,
            tourDate.PriceOverride,
            tourDate.Status.ToString(),
            tourDate.CreatedAt,
            tourDate.UpdatedAt
        );

        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] GET /admin/tour-dates/{id} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// ========== ADMIN BOOKINGS MANAGEMENT ENDPOINTS ==========

// Get all bookings with filters and pagination
app.MapGet("/admin/bookings", async (
    IMongoDatabase db, 
    int page = 1, 
    int pageSize = 20,
    string? status = null, 
    string? tourId = null, 
    DateTime? startDate = null, 
    DateTime? endDate = null
) =>
{
    try
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Max page size limit

        var bookings = db.GetCollection<Booking>("bookings");
        var tours = db.GetCollection<Tour>("tours");

        var filter = Builders<Booking>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var statusEnum))
            filter = Builders<Booking>.Filter.And(filter, Builders<Booking>.Filter.Eq(x => x.Status, statusEnum));

        if (!string.IsNullOrWhiteSpace(tourId) && ObjectId.TryParse(tourId, out var tourObjId))
            filter = Builders<Booking>.Filter.And(filter, Builders<Booking>.Filter.Eq(x => x.TourId, tourObjId));

        if (startDate.HasValue)
            filter = Builders<Booking>.Filter.And(filter, Builders<Booking>.Filter.Gte(x => x.CreatedAt, startDate.Value));

        if (endDate.HasValue)
            filter = Builders<Booking>.Filter.And(filter, Builders<Booking>.Filter.Lte(x => x.CreatedAt, endDate.Value));

        // Get total count for pagination
        var total = await bookings.CountDocumentsAsync(filter);

        // Apply pagination
        var skip = (page - 1) * pageSize;
        var list = await bookings.Find(filter)
            .SortByDescending(b => b.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        var dtoList = new List<AdminBookingDto>();
        foreach (var b in list)
        {
            var tour = await tours.Find(t => t.Id == b.TourId).FirstOrDefaultAsync();
            TourInfo? tourInfo = null;
            if (tour is not null)
            {
                tourInfo = new TourInfo(
                    tour.Id.ToString(),
                    tour.Title ?? string.Empty,
                    tour.Slug ?? string.Empty,
                    tour.BasePrice,
                    tour.Currency ?? "USD",
                    tour.Images?.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList() ?? new List<TourImageDto>()
                );
            }

            dtoList.Add(new AdminBookingDto(
                b.Id.ToString(),
                b.BookingCode,
                b.Status,
                b.ExpiresAt,
                b.TourId.ToString(),
                tourInfo,
                b.TourDateId?.ToString(),
                b.TravelDate,
                b.TourType,
                new BookingContactDto(b.Contact.FullName, b.Contact.Email, b.Contact.Phone, b.Contact.Country),
                b.Guests.Select(g => new BookingGuestDto(g.FullName, g.Age, g.PassportNo)).ToList(),
                b.Guests.Count,
                new BookingPricingDto(b.Pricing.Currency, b.Pricing.Subtotal, b.Pricing.Discount, b.Pricing.Tax, b.Pricing.Total),
                b.SpecialRequests,
                b.CreatedAt,
                b.UpdatedAt
            ));
        }

        return Results.Ok(dtoList);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] /admin/bookings failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Get booking by ID with full details
app.MapGet("/admin/bookings/{id}", async (string id, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(id, out var bookingId))
            return Results.BadRequest("Invalid booking ID.");

        var bookings = db.GetCollection<Booking>("bookings");
        var tours = db.GetCollection<Tour>("tours");

        var booking = await bookings.Find(b => b.Id == bookingId).FirstOrDefaultAsync();
        if (booking is null)
            return Results.NotFound();

        var tour = await tours.Find(t => t.Id == booking.TourId).FirstOrDefaultAsync();
        TourInfo? tourInfo = null;
        if (tour is not null)
        {
            tourInfo = new TourInfo(
                tour.Id.ToString(),
                tour.Title ?? string.Empty,
                tour.Slug ?? string.Empty,
                tour.BasePrice,
                tour.Currency ?? "USD",
                tour.Images?.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList() ?? new List<TourImageDto>()
            );
        }

        var dto = new AdminBookingDto(
            booking.Id.ToString(),
            booking.BookingCode,
            booking.Status,
            booking.ExpiresAt,
            booking.TourId.ToString(),
            tourInfo,
            booking.TourDateId?.ToString(),
            booking.TravelDate,
            booking.TourType,
            new BookingContactDto(booking.Contact.FullName, booking.Contact.Email, booking.Contact.Phone, booking.Contact.Country),
            booking.Guests.Select(g => new BookingGuestDto(g.FullName, g.Age, g.PassportNo)).ToList(),
            booking.Guests.Count,
            new BookingPricingDto(booking.Pricing.Currency, booking.Pricing.Subtotal, booking.Pricing.Discount, booking.Pricing.Tax, booking.Pricing.Total),
            booking.SpecialRequests,
            booking.CreatedAt,
            booking.UpdatedAt
        );

        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] /admin/bookings/{id} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Update booking status
app.MapPut("/admin/bookings/{id}/status", async (string id, UpdateBookingStatusRequest req, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(id, out var bookingId))
            return Results.BadRequest("Invalid booking ID.");

        if (!Enum.TryParse<BookingStatus>(req.Status, true, out var newStatus))
            return Results.BadRequest("Invalid status. Must be: PendingPayment, Confirmed, Cancelled, or Expired.");

        var bookings = db.GetCollection<Booking>("bookings");
        var booking = await bookings.Find(b => b.Id == bookingId).FirstOrDefaultAsync();
        
        if (booking is null)
            return Results.NotFound();

        // Validate status transition (basic rules)
        if (booking.Status == BookingStatus.Cancelled && newStatus != BookingStatus.Cancelled)
            return Results.BadRequest("Cannot change status from Cancelled.");

        var update = Builders<Booking>.Update
            .Set(x => x.Status, newStatus)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await bookings.UpdateOneAsync(b => b.Id == bookingId, update);

        return Results.Ok(new { message = "Booking status updated successfully", status = newStatus.ToString() });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] PUT /admin/bookings/{id}/status failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Get booking statistics
app.MapGet("/admin/bookings/stats", async (IMongoDatabase db) =>
{
    try
    {
        var bookings = db.GetCollection<Booking>("bookings");
        
        var allBookings = await bookings.Find(_ => true).ToListAsync();

        var totalBookings = allBookings.Count;
        var pendingPayment = allBookings.Count(b => b.Status == BookingStatus.PendingPayment);
        var confirmed = allBookings.Count(b => b.Status == BookingStatus.Confirmed);
        var cancelled = allBookings.Count(b => b.Status == BookingStatus.Cancelled);
        var expired = allBookings.Count(b => b.Status == BookingStatus.Expired);

        var totalRevenue = allBookings
            .Where(b => b.Status == BookingStatus.Confirmed)
            .Sum(b => b.Pricing.Total);

        var pendingRevenue = allBookings
            .Where(b => b.Status == BookingStatus.PendingPayment)
            .Sum(b => b.Pricing.Total);

        var bookingsByStatus = new Dictionary<string, int>
        {
            { "PendingPayment", pendingPayment },
            { "Confirmed", confirmed },
            { "Cancelled", cancelled },
            { "Expired", expired }
        };

        var revenueByStatus = new Dictionary<string, decimal>
        {
            { "PendingPayment", pendingRevenue },
            { "Confirmed", totalRevenue },
            { "Cancelled", 0 },
            { "Expired", 0 }
        };

        var stats = new BookingStatsDto(
            totalBookings,
            pendingPayment,
            confirmed,
            cancelled,
            expired,
            totalRevenue,
            pendingRevenue,
            bookingsByStatus,
            revenueByStatus
        );

        return Results.Ok(stats);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] /admin/bookings/stats failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// ========== PAYMENT STATUS ENDPOINTS ==========

// Get payment status by invoice ID
app.MapGet("/payments/{invoiceId}", async (string invoiceId, IMongoDatabase db) =>
{
    try
    {
        var payments = db.GetCollection<Payment>("payments");
        var bookings = db.GetCollection<Booking>("bookings");

        var payment = await payments.Find(p => p.InvoiceId == invoiceId).FirstOrDefaultAsync();
        if (payment is null)
            return Results.NotFound("Payment not found.");

        var booking = await bookings.Find(b => b.Id == payment.BookingId).FirstOrDefaultAsync();

        return Results.Ok(new
        {
            id = payment.Id.ToString(),
            invoiceId = payment.InvoiceId,
            bookingId = payment.BookingId.ToString(),
            bookingCode = booking?.BookingCode,
            provider = payment.Provider,
            amount = payment.Amount,
            currency = payment.Currency,
            status = payment.Status.ToString(),
            checkoutUrl = payment.ProviderCheckoutUrl,
            qrText = payment.ProviderQrText,
            createdAt = payment.CreatedAt,
            updatedAt = payment.UpdatedAt,
            booking = booking is not null ? new
            {
                bookingCode = booking.BookingCode,
                status = booking.Status.ToString(),
                total = booking.Pricing.Total,
                currency = booking.Pricing.Currency
            } : null
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] /payments/{invoiceId} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// Get payment status for a booking
app.MapGet("/bookings/{bookingCode}/payment", async (string bookingCode, IMongoDatabase db) =>
{
    try
    {
        var bookings = db.GetCollection<Booking>("bookings");
        var payments = db.GetCollection<Payment>("payments");

        var booking = await bookings.Find(b => b.BookingCode == bookingCode).FirstOrDefaultAsync();
        if (booking is null)
            return Results.NotFound("Booking not found.");

        var payment = await payments.Find(p => p.BookingId == booking.Id)
            .SortByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (payment is null)
            return Results.Ok(new
            {
                bookingCode = booking.BookingCode,
                hasPayment = false,
                message = "No payment found for this booking"
            });

        return Results.Ok(new
        {
            bookingCode = booking.BookingCode,
            hasPayment = true,
            payment = new
            {
                id = payment.Id.ToString(),
                invoiceId = payment.InvoiceId,
                provider = payment.Provider,
                amount = payment.Amount,
                currency = payment.Currency,
                status = payment.Status.ToString(),
                checkoutUrl = payment.ProviderCheckoutUrl,
                qrText = payment.ProviderQrText,
                createdAt = payment.CreatedAt,
                updatedAt = payment.UpdatedAt
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] /bookings/{bookingCode}/payment failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();
