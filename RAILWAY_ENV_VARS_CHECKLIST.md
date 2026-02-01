# Railway Environment Variables Checklist

## Required Environment Variables

Before deploying, ensure these are set in Railway Dashboard → Your Service → Variables:

### Critical (Must Have)

- [ ] **`JWT_SECRET`** 
  - Description: Secret key for JWT token signing
  - Minimum length: 32 characters
  - Example: `your-super-secret-jwt-key-minimum-32-chars-long`
  - **⚠️ Without this, admin authentication will fail**

- [ ] **`MONGO_CONNECTIONSTRING`**
  - Description: MongoDB connection string
  - Format: `mongodb+srv://username:password@cluster.mongodb.net/`
  - **⚠️ Without this, the API cannot connect to database**

## Optional Environment Variables

These have defaults but can be customized:

- [ ] **`MONGO_DATABASENAME`**
  - Default: `travel_db`
  - Description: MongoDB database name

- [ ] **`JWT_ISSUER`**
  - Default: `steppia-travel-api`
  - Description: JWT token issuer

- [ ] **`JWT_AUDIENCE`**
  - Default: `steppia-travel-admin`
  - Description: JWT token audience

- [ ] **`ADMIN_USERNAME`**
  - Default: `admin`
  - Description: Initial admin username (only used if no admins exist)

- [ ] **`ADMIN_PASSWORD`**
  - Default: `admin123`
  - Description: Initial admin password (only used if no admins exist)
  - **⚠️ Change this in production!**

- [ ] **`PORT`**
  - Description: Server port (Railway sets this automatically)
  - Usually: `8080` or `8000`
  - **Note:** Railway sets this automatically, but verify it matches your service port

### Email confirmation (user registration)

To send the 6-digit confirmation code to users’ email, add SMTP variables. **Without these, the app still runs** but codes are only logged to the console (no email sent).

- [ ] **`SMTP_HOST`**
  - Description: SMTP server host
  - Examples: `smtp.gmail.com`, `smtp.sendgrid.net`, `smtp.office365.com`

- [ ] **`SMTP_PORT`**
  - Default: `587`
  - Description: SMTP port (587 for TLS)

- [ ] **`SMTP_USERNAME`**
  - Description: SMTP login (often your email or API username)

- [ ] **`SMTP_PASSWORD`**
  - Description: SMTP password (for Gmail use an [App Password](https://support.google.com/accounts/answer/185833))

- [ ] **`SMTP_FROM_EMAIL`**
  - Default: `noreply@example.com`
  - Description: “From” email address

- [ ] **`SMTP_FROM_NAME`**
  - Default: `Steppia Travel`
  - Description: “From” display name

- [ ] **`SMTP_ENABLE_SSL`**
  - Default: `true`
  - Description: Use TLS (use `true` for port 587)

**Gmail:**
- `SMTP_HOST=smtp.gmail.com`, `SMTP_PORT=587` (or **465** if 587 times out), `SMTP_USERNAME=your@gmail.com`, `SMTP_PASSWORD=<App Password>`
- **SMTP_FROM_EMAIL** must be your Gmail address (e.g. `your@gmail.com`). Gmail rejects sends when "From" is not the authenticated account. If you leave it empty or `noreply@example.com`, the app will use `SMTP_USERNAME` as From when using Gmail.
- **SMTP_PASSWORD** must be a [Gmail App Password](https://support.google.com/accounts/answer/185833) (16 chars), not your normal Gmail password, especially with 2-Step Verification.
- **If you see "The operation has timed out"** on Railway: outbound port 587 is often blocked. Set **SMTP_PORT=465** in Railway (SMTPS). The app uses SSL on connect for 465 and STARTTLS for 587.
- If emails still don’t arrive, check Railway logs for `[Email] Failed to send confirmation` to see the SMTP error.

## How to Set in Railway

1. Go to Railway Dashboard
2. Select your service
3. Click on "Variables" tab
4. Click "New Variable"
5. Add each variable with its value
6. Save changes

## Verification After Deployment

After deployment, check Railway logs for:
- `[Startup] JWT authentication configured` - Confirms JWT_SECRET is set
- `[Startup] MongoDB connection string: ...` - Confirms MONGO_CONNECTIONSTRING is set
- `[Startup] Created default admin user: admin` - Confirms admin initialization (if no admins exist)

## Troubleshooting

### Admin Login Returns 404
- Check if `JWT_SECRET` is set (minimum 32 characters)
- Check Railway logs for JWT configuration errors
- Verify admin user exists in MongoDB `admins` collection

### MongoDB Connection Fails
- Verify `MONGO_CONNECTIONSTRING` is correct
- Check MongoDB Atlas Network Access allows `0.0.0.0/0`
- Verify database user has correct permissions

### Application Won't Start
- Check Railway logs for startup errors
- Verify all required environment variables are set
- Check if PORT matches Railway service port
