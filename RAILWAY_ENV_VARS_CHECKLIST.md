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
