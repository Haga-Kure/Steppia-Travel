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
                "https://steppia-travel.com",  // Production (Vercel + custom domain)
                "https://www.steppia-travel.com"
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

static TourItineraryItem MapItineraryItemRequestToModel(TourItineraryItemRequest i)
{
    return new TourItineraryItem
    {
        Day = i.Day,
        Title = i.Title,
        Notes = i.Notes,
        Breakfast = i.Breakfast,
        Lunch = i.Lunch,
        Dinner = i.Dinner,
        Accommodation = i.Accommodation,
        Stay = i.Stay,
        DistanceKm = i.DistanceKm,
        StartPlace = i.StartPlace,
        EndPlace = i.EndPlace,
        FirstSegmentDistanceKm = i.FirstSegmentDistanceKm,
        RouteWaypoints = i.RouteWaypoints?.Select(w => new TourItineraryRouteWaypoint { Place = w.Place ?? "", DistanceToNextKm = w.DistanceToNextKm }).ToList(),
        ImageUrl = i.ImageUrl
    };
}

static List<ItineraryDayDto>? MapItineraryToDto(List<TourItineraryItem>? items)
{
    if (items is null || items.Count == 0) return null;
    return items.Select(i => new ItineraryDayDto(
        Day: i.Day,
        Title: i.Title,
        Notes: i.Notes,
        Breakfast: i.Breakfast,
        Lunch: i.Lunch,
        Dinner: i.Dinner,
        Accommodation: i.Accommodation,
        Stay: i.Stay,
        DistanceKm: i.DistanceKm,
        StartPlace: i.StartPlace,
        EndPlace: i.EndPlace,
        FirstSegmentDistanceKm: i.FirstSegmentDistanceKm,
        RouteWaypoints: i.RouteWaypoints?.Select(w => new ItineraryRouteWaypointDto(w.Place ?? "", w.DistanceToNextKm)).ToList(),
        ImageUrl: i.ImageUrl
    )).ToList();
}

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

// Send confirmation email via Resend HTTP API (works when SMTP ports are blocked, e.g. Railway).
static async Task SendConfirmationEmailViaResendAsync(string apiKey, IConfiguration config, string toEmail, string code)
{
    // Resend requires a verified "from" domain. Gmail/Yahoo etc. cannot be verified – use Resend's test sender.
    var configuredFrom = (config["RESEND_FROM_EMAIL"] ?? Environment.GetEnvironmentVariable("RESEND_FROM_EMAIL")
        ?? config["SMTP_FROM_EMAIL"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL"))?.Trim();
    var domain = configuredFrom != null && configuredFrom.Contains('@') ? configuredFrom.Split('@')[^1].ToLowerInvariant() : "";
    var isFreeEmail = domain is "gmail.com" or "yahoo.com" or "outlook.com" or "hotmail.com";
    var fromEmail = (!string.IsNullOrWhiteSpace(configuredFrom) && !isFreeEmail) ? configuredFrom : "onboarding@resend.dev";
    var fromName = config["SMTP_FROM_NAME"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Steppia Travel";
    var from = $"{fromName} <{fromEmail}>";
    var subject = "Your confirmation code";
    var text = $"Your confirmation code is: {code}. It expires in 15 minutes.";
    using var http = new HttpClient();
    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    var body = new { from, to = new[] { toEmail }, subject, text };
    var json = System.Text.Json.JsonSerializer.Serialize(body);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    var response = await http.PostAsync("https://api.resend.com/emails", content);
    if (response.IsSuccessStatusCode)
        Console.WriteLine($"[Email] Confirmation code sent to {toEmail} via Resend");
    else
        Console.WriteLine($"[Email] Resend failed for {toEmail}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
}

// Send 6-digit confirmation code to email. Prefer Resend (HTTP) if RESEND_API_KEY is set; else SMTP (often blocked on Railway).
static async Task SendConfirmationEmailAsync(IConfiguration config, string toEmail, string code)
{
    var resendKey = config["RESEND_API_KEY"] ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");
    if (!string.IsNullOrWhiteSpace(resendKey))
    {
        await SendConfirmationEmailViaResendAsync(resendKey, config, toEmail, code);
        return;
    }

    var host = config["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
    if (string.IsNullOrWhiteSpace(host))
    {
        Console.WriteLine($"[Email] Neither RESEND_API_KEY nor SMTP configured. Confirmation code for {toEmail}: {code}");
        return;
    }
    var port = int.TryParse(config["SMTP_PORT"] ?? Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
    var user = config["SMTP_USERNAME"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
    var password = config["SMTP_PASSWORD"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
    var fromEmail = config["SMTP_FROM_EMAIL"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? "noreply@example.com";
    var fromName = config["SMTP_FROM_NAME"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Steppia Travel";
    var useSsl = string.Equals(config["SMTP_ENABLE_SSL"] ?? Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL") ?? "true", "true", StringComparison.OrdinalIgnoreCase);

    // Gmail (and many providers) require "From" to be the authenticated address
    if (!string.IsNullOrWhiteSpace(user) && (string.IsNullOrWhiteSpace(fromEmail) || fromEmail.Contains("example.com", StringComparison.OrdinalIgnoreCase)))
        fromEmail = user;

    var message = new MimeMessage();
    message.From.Add(new MailboxAddress(fromName, fromEmail));
    message.To.Add(new MailboxAddress(toEmail, toEmail));
    message.Subject = "Your confirmation code";
    message.Body = new TextPart("plain")
    {
        Text = $"Your confirmation code is: {code}. It expires in 15 minutes."
    };

    // Port 465 = SMTPS (implicit SSL); 587 = STARTTLS. Some hosts block 587 – try 465.
    var secureOptions = port == 465 ? SecureSocketOptions.SslOnConnect
        : (useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
    var skipSslVerify = string.Equals(config["SMTP_SKIP_SSL_VERIFY"] ?? Environment.GetEnvironmentVariable("SMTP_SKIP_SSL_VERIFY") ?? "false", "true", StringComparison.OrdinalIgnoreCase);

    try
    {
        using var client = new SmtpClient();
        client.Timeout = 20000; // 20 seconds
        if (skipSslVerify)
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(host, port, secureOptions);
        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            await client.AuthenticateAsync(user, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        Console.WriteLine($"[Email] Confirmation code sent to {toEmail}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Email] Failed to send to {toEmail}: {ex.GetType().Name}: {ex.Message}");
        if (ex is TimeoutException)
            Console.WriteLine($"[Email] SMTP timed out (often blocked on Railway). Set RESEND_API_KEY to send via Resend instead.");
        else
            Console.WriteLine($"[Email] For port 465 SSL errors set SMTP_SKIP_SSL_VERIFY=true. Prefer RESEND_API_KEY on Railway.");
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

// Log Telegram notify config (values not printed) so deploy logs show if vars are loaded
var telegramTokenSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"));
var telegramChatSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID"));
Console.WriteLine($"[Startup] Telegram notify: TELEGRAM_BOT_TOKEN={((telegramTokenSet ? "SET" : "NOT SET"))}, TELEGRAM_CHAT_ID={(telegramChatSet ? "SET" : "NOT SET")}");

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
                    Itinerary: MapItineraryToDto(t.Itinerary),
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
    if (tour is null) return Results.NotFound();

    var tourDto = new TourDto(
        Id: tour.Id.ToString(),
        Slug: tour.Slug ?? string.Empty,
        Title: tour.Title ?? string.Empty,
        Type: tour.Type ?? string.Empty,
        Summary: tour.Summary,
        Description: tour.Description,
        DurationDays: tour.DurationDays,
        Nights: tour.Nights,
        Region: tour.Region,
        TotalDistanceKm: tour.TotalDistanceKm,
        TravelStyle: tour.TravelStyle,
        Highlights: tour.Highlights,
        Accommodation: tour.Accommodation,
        Itinerary: MapItineraryToDto(tour.Itinerary),
        Activities: tour.Activities,
        IdealFor: tour.IdealFor,
        BasePrice: tour.BasePrice,
        Currency: tour.Currency ?? "USD",
        Locations: (tour.Locations ?? new List<TourLocation>()).Select(loc => new TourLocationDto(loc.Name, loc.Latitude, loc.Longitude)).ToList(),
        Images: tour.Images?.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList() ?? new List<TourImageDto>(),
        BobbleTitle: tour.BobbleTitle
    );
    return Results.Ok(tourDto);
});

// ========== EVENTS ENDPOINTS ==========

// List active events with pagination and basic filtering
app.MapGet("/events", async (
    IMongoDatabase db,
    int page = 1,
    int pageSize = 20,
    string? search = null,
    string? type = null,
    int? year = null,
    string? region = null
) =>
{
    try
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var col = db.GetCollection<Event>("events");
        var filter = Builders<Event>.Filter.Eq(e => e.IsActive, true);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchFilter = Builders<Event>.Filter.Or(
                Builders<Event>.Filter.Regex(e => e.Title, new BsonRegularExpression(search, "i")),
                Builders<Event>.Filter.Regex(e => e.Summary, new BsonRegularExpression(search, "i")),
                Builders<Event>.Filter.Regex(e => e.Description, new BsonRegularExpression(search, "i")),
                Builders<Event>.Filter.Regex(e => e.EventDetails.Name, new BsonRegularExpression(search, "i"))
            );
            filter = Builders<Event>.Filter.And(filter, searchFilter);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var typeFilter = Builders<Event>.Filter.Eq(e => e.EventDetails.Type, type);
            filter = Builders<Event>.Filter.And(filter, typeFilter);
        }

        if (year.HasValue)
        {
            var yearFilter = Builders<Event>.Filter.Eq(e => e.EventDetails.Year, year.Value);
            filter = Builders<Event>.Filter.And(filter, yearFilter);
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            var regionFilter = Builders<Event>.Filter.Eq(e => e.Region, region);
            filter = Builders<Event>.Filter.And(filter, regionFilter);
        }

        var total = await col.CountDocumentsAsync(filter);
        var skip = (page - 1) * pageSize;
        var list = await col.Find(filter)
            .SortByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        var dto = list.Select(e => new EventDto(
            e.Id.ToString(),
            e.Slug ?? string.Empty,
            e.Title ?? string.Empty,
            new EventInfoDto(
                e.EventDetails?.Name ?? string.Empty,
                e.EventDetails?.Type ?? string.Empty,
                e.EventDetails?.Year ?? 0
            ),
            e.Summary,
            e.Description,
            e.DurationDays,
            e.Nights,
            e.StartDate,
            e.EndDate,
            e.BestSeason,
            e.Region,
            e.Locations,
            e.TravelStyle,
            e.Difficulty,
            e.GroupType,
            e.MaxGroupSize,
            e.PriceUSD,
            e.Includes,
            e.Excludes,
            e.Highlights,
            e.Images is null ? null : new EventImagesDto(e.Images.Cover, e.Images.Gallery)
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
        Console.WriteLine($"[Error] /events endpoint failed: {ex.Message}");
        return Results.Problem(detail: $"Error fetching events: {ex.Message}", statusCode: 500);
    }
});

// Get event by slug (public, active only)
app.MapGet("/events/{slug}", async (string slug, IMongoDatabase db) =>
{
    var col = db.GetCollection<Event>("events");
    var ev = await col.Find(e => e.Slug == slug && e.IsActive).FirstOrDefaultAsync();
    if (ev is null) return Results.NotFound();

    var dto = new EventDto(
        ev.Id.ToString(),
        ev.Slug ?? string.Empty,
        ev.Title ?? string.Empty,
        new EventInfoDto(
            ev.EventDetails?.Name ?? string.Empty,
            ev.EventDetails?.Type ?? string.Empty,
            ev.EventDetails?.Year ?? 0
        ),
        ev.Summary,
        ev.Description,
        ev.DurationDays,
        ev.Nights,
        ev.StartDate,
        ev.EndDate,
        ev.BestSeason,
        ev.Region,
        ev.Locations,
        ev.TravelStyle,
        ev.Difficulty,
        ev.GroupType,
        ev.MaxGroupSize,
        ev.PriceUSD,
        ev.Includes,
        ev.Excludes,
        ev.Highlights,
        ev.Images is null ? null : new EventImagesDto(ev.Images.Cover, ev.Images.Gallery)
    );

    return Results.Ok(dto);
});

// ========== ADMIN EVENTS CRUD ENDPOINTS ==========

// Get all events (including inactive) with pagination
app.MapGet("/admin/events", async (IMongoDatabase db, int page = 1, int pageSize = 20) =>
{
    try
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var col = db.GetCollection<Event>("events");
        var total = await col.CountDocumentsAsync(FilterDefinition<Event>.Empty);
        var skip = (page - 1) * pageSize;
        var list = await col.Find(_ => true)
            .SortByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        var dto = list.Select(e => new AdminEventDto(
            e.Id.ToString(),
            e.Slug ?? string.Empty,
            e.Title ?? string.Empty,
            new EventInfoDto(
                e.EventDetails?.Name ?? string.Empty,
                e.EventDetails?.Type ?? string.Empty,
                e.EventDetails?.Year ?? 0
            ),
            e.Summary,
            e.Description,
            e.DurationDays,
            e.Nights,
            e.StartDate,
            e.EndDate,
            e.BestSeason,
            e.Region,
            e.Locations,
            e.TravelStyle,
            e.Difficulty,
            e.GroupType,
            e.MaxGroupSize,
            e.PriceUSD,
            e.Includes,
            e.Excludes,
            e.Highlights,
            e.Images is null ? null : new EventImagesDto(e.Images.Cover, e.Images.Gallery),
            e.IsActive,
            e.CreatedAt,
            e.UpdatedAt
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
        Console.WriteLine($"[Error] /admin/events failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Get event by slug (admin, includes inactive)
app.MapGet("/admin/events/{slug}", async (string slug, IMongoDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(slug)) return Results.BadRequest("Slug is required.");

    var col = db.GetCollection<Event>("events");
    var ev = await col.Find(e => e.Slug == slug).FirstOrDefaultAsync();
    if (ev is null) return Results.NotFound();

    var dto = new AdminEventDto(
        ev.Id.ToString(),
        ev.Slug ?? string.Empty,
        ev.Title ?? string.Empty,
        new EventInfoDto(
            ev.EventDetails?.Name ?? string.Empty,
            ev.EventDetails?.Type ?? string.Empty,
            ev.EventDetails?.Year ?? 0
        ),
        ev.Summary,
        ev.Description,
        ev.DurationDays,
        ev.Nights,
        ev.StartDate,
        ev.EndDate,
        ev.BestSeason,
        ev.Region,
        ev.Locations,
        ev.TravelStyle,
        ev.Difficulty,
        ev.GroupType,
        ev.MaxGroupSize,
        ev.PriceUSD,
        ev.Includes,
        ev.Excludes,
        ev.Highlights,
        ev.Images is null ? null : new EventImagesDto(ev.Images.Cover, ev.Images.Gallery),
        ev.IsActive,
        ev.CreatedAt,
        ev.UpdatedAt
    );

    return Results.Ok(dto);
}).RequireAuthorization("AdminOnly");

// Create new event
app.MapPost("/admin/events", async (CreateEventRequest req, IMongoDatabase db) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(req.Slug)) return Results.BadRequest("Slug is required.");
        if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest("Title is required.");
        if (req.Event is null || string.IsNullOrWhiteSpace(req.Event.Name) || string.IsNullOrWhiteSpace(req.Event.Type))
            return Results.BadRequest("Event name and type are required.");

        var col = db.GetCollection<Event>("events");
        var existing = await col.Find(e => e.Slug == req.Slug).FirstOrDefaultAsync();
        if (existing is not null) return Results.BadRequest("Event with this slug already exists.");

        var ev = new Event
        {
            Slug = req.Slug.Trim(),
            Title = req.Title.Trim(),
            EventDetails = new EventInfo
            {
                Name = req.Event.Name.Trim(),
                Type = req.Event.Type.Trim(),
                Year = req.Event.Year
            },
            Summary = req.Summary,
            Description = req.Description,
            DurationDays = req.DurationDays,
            Nights = req.Nights,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            BestSeason = req.BestSeason,
            Region = req.Region,
            Locations = req.Locations,
            TravelStyle = req.TravelStyle,
            Difficulty = req.Difficulty,
            GroupType = req.GroupType,
            MaxGroupSize = req.MaxGroupSize,
            PriceUSD = req.PriceUSD,
            Includes = req.Includes,
            Excludes = req.Excludes,
            Highlights = req.Highlights,
            Images = req.Images is null ? null : new EventImages
            {
                Cover = req.Images.Cover,
                Gallery = req.Images.Gallery
            },
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await col.InsertOneAsync(ev);

        var dto = new AdminEventDto(
            ev.Id.ToString(),
            ev.Slug,
            ev.Title,
            new EventInfoDto(ev.EventDetails.Name, ev.EventDetails.Type, ev.EventDetails.Year),
            ev.Summary,
            ev.Description,
            ev.DurationDays,
            ev.Nights,
            ev.StartDate,
            ev.EndDate,
            ev.BestSeason,
            ev.Region,
            ev.Locations,
            ev.TravelStyle,
            ev.Difficulty,
            ev.GroupType,
            ev.MaxGroupSize,
            ev.PriceUSD,
            ev.Includes,
            ev.Excludes,
            ev.Highlights,
            ev.Images is null ? null : new EventImagesDto(ev.Images.Cover, ev.Images.Gallery),
            ev.IsActive,
            ev.CreatedAt,
            ev.UpdatedAt
        );

        return Results.Created($"/admin/events/{ev.Slug}", dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] POST /admin/events failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Update event (by slug)
app.MapPut("/admin/events/{slug}", async (string slug, UpdateEventRequest req, IMongoDatabase db) =>
{
    try
    {
        var col = db.GetCollection<Event>("events");
        if (string.IsNullOrWhiteSpace(slug))
            return Results.BadRequest("Slug is required.");

        var existing = await col.Find(e => e.Slug == slug).FirstOrDefaultAsync();
        if (existing is null) return Results.NotFound();

        if (!string.IsNullOrWhiteSpace(req.Slug) && req.Slug != existing.Slug)
        {
            var slugExists = await col.Find(e => e.Slug == req.Slug && e.Id != existing.Id).FirstOrDefaultAsync();
            if (slugExists is not null)
                return Results.BadRequest("Event with this slug already exists.");
        }

        var update = Builders<Event>.Update.Set(x => x.UpdatedAt, DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(req.Slug))
            update = update.Set(x => x.Slug, req.Slug.Trim());
        if (!string.IsNullOrWhiteSpace(req.Title))
            update = update.Set(x => x.Title, req.Title.Trim());
        if (req.Event is not null)
            update = update.Set(x => x.EventDetails, new EventInfo
            {
                Name = req.Event.Name?.Trim() ?? "",
                Type = req.Event.Type?.Trim() ?? "",
                Year = req.Event.Year
            });
        if (req.Summary is not null)
            update = update.Set(x => x.Summary, req.Summary);
        if (req.Description is not null)
            update = update.Set(x => x.Description, req.Description);
        if (req.DurationDays.HasValue)
            update = update.Set(x => x.DurationDays, req.DurationDays.Value);
        if (req.Nights.HasValue)
            update = update.Set(x => x.Nights, req.Nights.Value);
        if (req.StartDate.HasValue)
            update = update.Set(x => x.StartDate, req.StartDate);
        if (req.EndDate.HasValue)
            update = update.Set(x => x.EndDate, req.EndDate);
        if (req.BestSeason is not null)
            update = update.Set(x => x.BestSeason, req.BestSeason);
        if (req.Region is not null)
            update = update.Set(x => x.Region, req.Region);
        if (req.Locations is not null)
            update = update.Set(x => x.Locations, req.Locations);
        if (req.TravelStyle is not null)
            update = update.Set(x => x.TravelStyle, req.TravelStyle);
        if (req.Difficulty is not null)
            update = update.Set(x => x.Difficulty, req.Difficulty);
        if (req.GroupType is not null)
            update = update.Set(x => x.GroupType, req.GroupType);
        if (req.MaxGroupSize.HasValue)
            update = update.Set(x => x.MaxGroupSize, req.MaxGroupSize);
        if (req.PriceUSD.HasValue)
            update = update.Set(x => x.PriceUSD, req.PriceUSD.Value);
        if (req.Includes is not null)
            update = update.Set(x => x.Includes, req.Includes);
        if (req.Excludes is not null)
            update = update.Set(x => x.Excludes, req.Excludes);
        if (req.Highlights is not null)
            update = update.Set(x => x.Highlights, req.Highlights);
        if (req.Images is not null)
            update = update.Set(x => x.Images, new EventImages
            {
                Cover = req.Images.Cover,
                Gallery = req.Images.Gallery
            });
        if (req.IsActive.HasValue)
            update = update.Set(x => x.IsActive, req.IsActive.Value);

        await col.UpdateOneAsync(e => e.Id == existing.Id, update);

        var updated = await col.Find(e => e.Id == existing.Id).FirstOrDefaultAsync();
        if (updated is null) return Results.NotFound();

        var dto = new AdminEventDto(
            updated.Id.ToString(),
            updated.Slug ?? string.Empty,
            updated.Title ?? string.Empty,
            new EventInfoDto(
                updated.EventDetails?.Name ?? string.Empty,
                updated.EventDetails?.Type ?? string.Empty,
                updated.EventDetails?.Year ?? 0
            ),
            updated.Summary,
            updated.Description,
            updated.DurationDays,
            updated.Nights,
            updated.StartDate,
            updated.EndDate,
            updated.BestSeason,
            updated.Region,
            updated.Locations,
            updated.TravelStyle,
            updated.Difficulty,
            updated.GroupType,
            updated.MaxGroupSize,
            updated.PriceUSD,
            updated.Includes,
            updated.Excludes,
            updated.Highlights,
            updated.Images is null ? null : new EventImagesDto(updated.Images.Cover, updated.Images.Gallery),
            updated.IsActive,
            updated.CreatedAt,
            updated.UpdatedAt
        );

        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] PUT /admin/events/{slug} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

// Soft-delete event (by slug)
app.MapDelete("/admin/events/{slug}", async (string slug, IMongoDatabase db) =>
{
    try
    {
        var col = db.GetCollection<Event>("events");
        if (string.IsNullOrWhiteSpace(slug))
            return Results.BadRequest("Slug is required.");

        var update = Builders<Event>.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await col.UpdateOneAsync(e => e.Slug == slug, update);
        if (result.MatchedCount == 0) return Results.NotFound();

        return Results.Ok(new { message = "Event deactivated successfully." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] DELETE /admin/events/{slug} failed: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly");

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

    if (req.TravelDate is null)
        return Results.BadRequest("TravelDate is required.");

    if (!ObjectId.TryParse(req.TourId, out var tourId))
        return Results.BadRequest("Invalid TourId.");

    var tours = db.GetCollection<Tour>("tours");
    var bookings = db.GetCollection<Booking>("bookings");

    var tour = await tours.Find(t => t.Id == tourId && t.IsActive).FirstOrDefaultAsync();
    if (tour is null) return Results.NotFound("Tour not found.");

    // pricing
    var currency = tour.Currency;
    decimal pricePerBooking = tour.BasePrice;

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
        b.TravelDate,
        b.TourType,
        b.Contact,
        guestCount = b.Guests.Count,
        b.Pricing,
        b.CreatedAt
    });
});

app.MapPost("/payments", async (CreatePaymentRequest req, IMongoDatabase db, IConfiguration config) =>
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
    // In real world (e.g. Stripe): create Checkout Session and use session.Url as checkoutUrl.
    // Until then: redirect to your frontend payment page. Set PAYMENT_CHECKOUT_BASE_URL in env (default: https://steppia-travel.com).
    var baseUrl = Environment.GetEnvironmentVariable("PAYMENT_CHECKOUT_BASE_URL")
        ?? config["Payment:CheckoutBaseUrl"]
        ?? "https://steppia-travel.com";
    baseUrl = baseUrl.TrimEnd('/');
    var invoiceId = $"INV-{Guid.NewGuid():N}".ToUpperInvariant();
    var checkoutUrl = $"{baseUrl}/booking/pay?invoiceId={invoiceId}&bookingCode={req.BookingCode}";
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

// Telegram booking notification (when customer proceeds to payment). Set TELEGRAM_BOT_TOKEN and TELEGRAM_CHAT_ID on Railway.
app.MapPost("/notify/booking", async (NotifyBookingRequest req, IConfiguration config) =>
{
    // Try env vars first (Railway), then config (some hosts inject here)
    var token = (Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? config["TELEGRAM_BOT_TOKEN"])?.Trim();
    var chatId = (Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? config["TELEGRAM_CHAT_ID"])?.Trim();
    var tokenLen = token?.Length ?? 0;
    var chatIdLen = chatId?.Length ?? 0;
    Console.WriteLine($"[DEBUG] Token exists? {!string.IsNullOrEmpty(token)} (length={tokenLen})");
    Console.WriteLine($"[DEBUG] ChatId exists? {!string.IsNullOrEmpty(chatId)} (length={chatIdLen})");
    if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
    {
        // Log which TELEGRAM-related env vars exist (names only) to debug Railway config
        var telegramVars = Environment.GetEnvironmentVariables()
            .Keys.Cast<string>()
            .Where(k => k != null && k.IndexOf("TELEGRAM", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        Console.WriteLine($"[Notify] TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID not set; skipping. Env vars with 'TELEGRAM': {(telegramVars.Count > 0 ? string.Join(", ", telegramVars) : "(none)")}");
        return Results.Ok(new { ok = false, reason = "not configured" });
    }

    var lines = new[]
    {
        "🔔 *New booking – Proceed to Payment*",
        "",
        $"📋 Code: {req.BookingCode ?? "—"}",
        $"👤 Customer: {req.CustomerName ?? "—"}",
        $"✉️ Email: {req.CustomerEmail ?? "—"}",
        $"📞 Phone: {req.CustomerPhone ?? "—"}",
        $"🎫 Tour: {req.TourName ?? "—"}",
        $"👥 Travelers: {req.Travelers?.ToString() ?? "—"}",
        $"💰 Amount: {req.Amount ?? "—"}",
        "",
        "_Send payment details to the customer by email._",
    };
    var text = string.Join("\n", lines);

    using var http = new HttpClient();
    var url = $"https://api.telegram.org/bot{token}/sendMessage";
    var body = new { chat_id = chatId, text, parse_mode = "Markdown" };
    var json = System.Text.Json.JsonSerializer.Serialize(body);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    var res = await http.PostAsync(url, content);
    var responseBody = await res.Content.ReadAsStringAsync();

    if (!res.IsSuccessStatusCode)
    {
        Console.WriteLine($"[Notify] Telegram API error: {res.StatusCode} | {responseBody}");
        return Results.Json(new { error = "Failed to send Telegram message", detail = responseBody }, statusCode: 502);
    }
    Console.WriteLine("[Notify] Telegram notification sent successfully.");
    return Results.Ok(new { ok = true });
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

    // Send email in background so /user/register returns immediately (avoids timeout/hang)
    _ = Task.Run(() => SendConfirmationEmailAsync(config, email, code));

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
            t.Overview,
            t.Subtitle,
            t.BobbleTitle,
            t.DurationDays,
            t.Nights,
            t.BasePrice,
            t.Currency ?? "USD",
            (t.Locations ?? new List<TourLocation>()).Select(loc => new TourLocationDto(loc.Name, loc.Latitude, loc.Longitude)).ToList(),
            t.Images?.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList() ?? new List<TourImageDto>(),
            t.Region,
            t.TotalDistanceKm,
            t.Highlights,
            t.Included,
            t.Excluded,
            t.TravelStyle,
            t.Activities,
            t.IdealFor,
            t.Difficulty,
            t.GroupSize,
            MapItineraryToDto(t.Itinerary),
            t.Accommodation,
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
            Itinerary = req.Itinerary?.Select(i => MapItineraryItemRequestToModel(i)).ToList(),
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
            tour.Overview,
            tour.Subtitle,
            tour.BobbleTitle,
            tour.DurationDays,
            tour.Nights,
            tour.BasePrice,
            tour.Currency,
            (tour.Locations ?? new List<TourLocation>()).Select(loc => new TourLocationDto(loc.Name, loc.Latitude, loc.Longitude)).ToList(),
            tour.Images.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList(),
            tour.Region,
            tour.TotalDistanceKm,
            tour.Highlights,
            tour.Included,
            tour.Excluded,
            tour.TravelStyle,
            tour.Activities,
            tour.IdealFor,
            tour.Difficulty,
            tour.GroupSize,
            MapItineraryToDto(tour.Itinerary),
            tour.Accommodation,
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
app.MapPut("/admin/tours/{id}", async (string id, [Microsoft.AspNetCore.Mvc.FromBody] UpdateTourRequest req, IMongoDatabase db) =>
{
    try
    {
        if (!ObjectId.TryParse(id, out var tourId))
            return Results.BadRequest("Invalid tour ID.");

        var col = db.GetCollection<Tour>("tours");
        var tour = await col.Find(t => t.Id == tourId).FirstOrDefaultAsync();
        
        if (tour is null)
            return Results.NotFound();

        if (req.Slug is not null)
        {
            var slugTrimmed = req.Slug.Trim();
            if (string.IsNullOrWhiteSpace(slugTrimmed))
                return Results.BadRequest("Slug cannot be empty.");
            if (slugTrimmed != tour.Slug)
            {
                var existing = await col.Find(t => t.Slug == slugTrimmed && t.Id != tourId).FirstOrDefaultAsync();
                if (existing is not null)
                    return Results.BadRequest("Tour with this slug already exists.");
            }
        }

        var update = Builders<Tour>.Update
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        if (req.Slug is not null)
            update = update.Set(x => x.Slug, req.Slug.Trim());
        if (req.Title is not null)
            update = update.Set(x => x.Title, req.Title.Trim());
        if (req.Subtitle is not null)
            update = update.Set(x => x.Subtitle, req.Subtitle);
        if (req.Type is not null)
            update = update.Set(x => x.Type, req.Type.Trim());
        if (req.Summary is not null)
            update = update.Set(x => x.Summary, req.Summary);
        if (req.Description is not null)
            update = update.Set(x => x.Description, req.Description);
        if (req.Overview is not null)
            update = update.Set(x => x.Overview, req.Overview);
        if (req.BobbleTitle is not null)
            update = update.Set(x => x.BobbleTitle, req.BobbleTitle);
        if (req.DurationDays.HasValue)
            update = update.Set(x => x.DurationDays, req.DurationDays.Value);
        if (req.Nights.HasValue)
            update = update.Set(x => x.Nights, req.Nights.Value);
        if (req.BasePrice.HasValue)
            update = update.Set(x => x.BasePrice, req.BasePrice.Value);
        if (req.Currency is not null)
            update = update.Set(x => x.Currency, req.Currency);
        if (req.Locations is not null)
            update = update.Set(x => x.Locations, req.Locations.Select(loc => new TourLocation { Name = loc.Name ?? "", Latitude = loc.Latitude, Longitude = loc.Longitude }).ToList());
        if (req.Region is not null)
            update = update.Set(x => x.Region, req.Region);
        if (req.TotalDistanceKm.HasValue)
            update = update.Set(x => x.TotalDistanceKm, req.TotalDistanceKm.Value);
        if (req.ClearAccommodation == true)
            update = update.Set(x => x.Accommodation, (TourAccommodation?)null);
        else if (req.Accommodation is not null)
            update = update.Set(x => x.Accommodation, new TourAccommodation { HotelNights = req.Accommodation.HotelNights, CampNights = req.Accommodation.CampNights, Notes = req.Accommodation.Notes });
        if (req.Images is not null)
            update = update.Set(x => x.Images, req.Images.Select(img => new TourImage
            {
                Url = img.Url,
                Alt = img.Alt,
                IsCover = img.IsCover
            }).ToList());
        if (req.Highlights is not null)
            update = update.Set(x => x.Highlights, req.Highlights);
        if (req.Included is not null)
            update = update.Set(x => x.Included, req.Included);
        if (req.Excluded is not null)
            update = update.Set(x => x.Excluded, req.Excluded);
        if (req.TravelStyle is not null)
            update = update.Set(x => x.TravelStyle, req.TravelStyle);
        if (req.Activities is not null)
            update = update.Set(x => x.Activities, req.Activities);
        if (req.IdealFor is not null)
            update = update.Set(x => x.IdealFor, req.IdealFor);
        if (req.Difficulty is not null)
            update = update.Set(x => x.Difficulty, req.Difficulty);
        if (req.GroupSize is not null)
            update = update.Set(x => x.GroupSize, req.GroupSize);
        if (req.Itinerary is not null)
            update = update.Set(x => x.Itinerary, req.Itinerary.Select(i => MapItineraryItemRequestToModel(i)).ToList());
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
            tour.Overview,
            tour.Subtitle,
            tour.BobbleTitle,
            tour.DurationDays,
            tour.Nights,
            tour.BasePrice,
            tour.Currency ?? "USD",
            (tour.Locations ?? new List<TourLocation>()).Select(loc => new TourLocationDto(loc.Name, loc.Latitude, loc.Longitude)).ToList(),
            tour.Images?.Select(img => new TourImageDto(img.Url, img.Alt, img.IsCover)).ToList() ?? new List<TourImageDto>(),
            tour.Region,
            tour.TotalDistanceKm,
            tour.Highlights,
            tour.Included,
            tour.Excluded,
            tour.TravelStyle,
            tour.Activities,
            tour.IdealFor,
            tour.Difficulty,
            tour.GroupSize,
            MapItineraryToDto(tour.Itinerary),
            tour.Accommodation,
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
