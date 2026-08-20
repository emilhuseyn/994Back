# Code994 — ECommerce Backend (.NET 8)

A clean-architecture .NET 8 Web API for a fashion / streetwear e-commerce site (Wrangler, Lee, Eastpak, etc.). Designed to power the [ShoppingFront](../ShoppingFront) Next.js storefront and a future admin panel.

## Stack

- .NET 8 Web API
- Entity Framework Core 8 + **Pomelo MySQL** provider
- AutoMapper
- FluentValidation
- JWT Bearer Authentication
- Serilog (Console + rolling File)
- Swagger / OpenAPI

## Solution layout

```
src/
├── ECommerce.API            ← controllers, middlewares, Program.cs, Swagger, JWT, CORS
├── ECommerce.Application    ← DTOs, services, interfaces, validators, AutoMapper, common types
├── ECommerce.Domain         ← entities, enums (no dependencies)
├── ECommerce.Infrastructure ← JWT, password hashing, file storage, current-user
└── ECommerce.Persistence    ← DbContext, configurations, repositories, UnitOfWork, seeder
```

## Quick start

```powershell
# 1. Make sure MySQL is reachable and the `Shopping` database exists:
#    mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS Shopping CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
# 2. Adjust ConnectionStrings:DefaultConnection in src/ECommerce.API/appsettings.json if your
#    host/user/password differ from the default
#    (server=localhost;database=Shopping;user=root;password=...)
# 3. Run the API — migrations + seed run automatically on startup
cd src/ECommerce.API
dotnet run
```

Swagger: <http://localhost:5080/swagger>

### Manual EF Core migrations

The project ships ready for `Microsoft.EntityFrameworkCore.Tools`. To create the first migration manually:

```powershell
dotnet tool install --global dotnet-ef            # only once
cd src/ECommerce.API
dotnet ef migrations add InitialCreate --project ../ECommerce.Persistence --startup-project .
dotnet ef database update --project ../ECommerce.Persistence --startup-project .
```

If you skip this, `Program.cs` calls `Database.MigrateAsync()` on startup — but you still need at least one migration committed. To trigger an automatic schema create from the model, replace `MigrateAsync` with `EnsureCreatedAsync` in `DatabaseSeeder.SeedAsync`.

## Default seed accounts

| Role    | Email                  | Password      |
| ------- | ---------------------- | ------------- |
| Admin   | admin@code994.az       | `Admin@123`   |
| User    | customer@code994.az    | `Customer@123` |

## Configuration (`appsettings.json`)

- `ConnectionStrings:DefaultConnection` — MySQL connection (`server=...;database=...;user=...;password=...;`). The pinned MySQL version is `8.0.36` in `Persistence/DependencyInjection.cs` — update it there if you run a different server major version.
- `Jwt:SecretKey` — **change before production** (min 32 chars).
- `Jwt:AccessTokenMinutes` — access token lifetime.
- `FileStorage:RootPath` / `PublicBaseUrl` — local image upload location.
- `Cors:AllowedOrigins` — frontends allowed to call the API.

## API surface (high level)

### Auth
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh-token`
- `GET  /api/auth/me`

### Public catalog
- `GET  /api/products?page=&pageSize=&categorySlug=&brandSlug=&minPrice=&maxPrice=&gender=&color=&size=&sort=&search=`
- `GET  /api/products/{slug}`
- `GET  /api/categories/tree`
- `GET  /api/categories/{slug}`
- `GET  /api/brands`
- `GET  /api/filters`
- `GET  /api/sliders`
- `GET  /api/site-settings`

### Cart (logged-in or guest via `X-Session-Id` header)
- `GET    /api/cart`
- `POST   /api/cart/items`
- `PUT    /api/cart/items/{id}`
- `DELETE /api/cart/items/{id}`
- `DELETE /api/cart/clear`

### Orders
- `POST /api/orders`
- `GET  /api/orders/my`            (auth)
- `GET  /api/admin/orders`         (admin)
- `GET  /api/admin/orders/{id}`    (admin)
- `PUT  /api/admin/orders/{id}/status` (admin)

### Wishlist (auth)
- `GET    /api/wishlist`
- `POST   /api/wishlist/{productId}`
- `DELETE /api/wishlist/{productId}`

### Contact
- `POST /api/contact`
- `GET  /api/admin/contact-messages`        (admin)
- `PUT  /api/admin/contact-messages/{id}/read` (admin)

### Admin (Role = Admin)
- `POST /api/admin/products`
- `PUT  /api/admin/products/{id}`
- `DELETE /api/admin/products/{id}`
- `POST /api/admin/products/{id}/images` (multipart)
- `DELETE /api/admin/product-images/{imageId}`
- CRUD for categories, brands, sliders, site settings.

## Business rules

- **Price:** `EffectivePrice = DiscountPrice ?? BasePrice`.
- **Slugs:** unique, generated from name; AZ chars (`ə, ş, ç, ğ, ö, ü, ı`) folded.
- **Soft delete:** `Product`, `Category`, `Brand` use `IsDeleted + DeletedAt` + EF global query filters.
- **Cart:** logged-in users keyed by `UserId`, guests by `X-Session-Id` header.
- **Orders:** wrapped in a SQL transaction; reduce variant stock atomically; reject if insufficient stock; `OrderNumber` like `ORD-2026-000001`.
- **Auth:** JWT access token + opaque refresh token stored on user record.
- **Roles:** `Customer` (default) and `Admin`. Admin endpoints require `[Authorize(Roles = "Admin")]`.

## Response envelope

```json
{
  "success": true,
  "message": "OK",
  "data": { ... },
  "errors": null
}
```

Paginated payload:

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 12,
  "totalCount": 100,
  "totalPages": 9,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

## Notes

- `JwtSettings.SecretKey` in `appsettings.json` is a placeholder — replace it before deploying.
- Image uploads are saved under `wwwroot/uploads/products/{productId}/...` and served via `UseStaticFiles`.
- Serilog writes to `logs/ecommerce-YYYYMMDD.log` (daily rolling, 14 day retention).
