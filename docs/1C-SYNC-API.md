# Code994 — Məhsul Sinxronizasiyası API (1C üçün)

Bu sənəd 1C-dən Code994 onlayn mağazasına məhsul göndərmək üçündür.
Bir HTTP sorğusu ilə bütün məhsulları (rəng, ölçü, qiymət, stok) göndərirsiniz.
Sistem **uid**-ə görə tanıyır: məhsul varsa **yeniləyir**, yoxsa **yaradır**.

---

## 1. Endpoint

```
POST https://code994.az/api/sync/products
Content-Type: application/json
```

Avtorizasiya ayrıca header tələb etmir — parol JSON-un içində `password` sahəsindədir.
Parol sizə Code994 tərəfindən veriləcək. Yanlış parol → **401 Unauthorized**.

---

## 2. Sorğu (Request) formatı

```json
{
  "password": "SİZƏ_VERİLƏN_PAROL",
  "Products": [
    {
      "uid": "PROD-1001",
      "name": "CARH Chase",
      "Category": { "uid": "CAT-001", "name": "T-Shirt" },
      "Brand":    { "uid": "BR-001",  "name": "Carhartt" },
      "Items": [
        {
          "Color":    { "uid": "COL-001", "name": "SILVER" },
          "Size":     { "uid": "SIZE-M",  "name": "M" },
          "Item":     "CARH Chase M T-Shirt",
          "ItemuUid": "58de62d8-63cc-11f1-98d0-e0d55eabb9d7",
          "Price":    89.9,
          "Quantity": 5
        },
        {
          "Color":    { "uid": "COL-003", "name": "BLACK" },
          "Size":     { "uid": "SIZE-L",  "name": "L" },
          "Item":     "CARH Chase L T-Shirt",
          "ItemuUid": "58de62da-63cc-11f1-98d0-e0d55eabb9d7",
          "Price":    89.9,
          "Quantity": 2
        }
      ]
    }
  ]
}
```

---

## 3. Sahələrin izahı

| Sahə | Məcburi | İzah |
|---|---|---|
| `password` | **bəli** | Sizə veriləcək gizli açar. Hər sorğuda olmalı. |
| `Products` | **bəli** | Məhsulların siyahısı. |
| `Products[].uid` | **bəli** | Məhsulun **sabit** identifikatoru (1C-dəki daxili ID və ya kod). Dəyişməməlidir — sistem buna görə tanıyır. |
| `Products[].name` | **bəli** | Məhsulun adı. |
| `Products[].Category` | xeyr | `{uid, name}`. Göndərilməsə, sistem adından təxmin edir (məs. "Pant" → Şalvarlar). |
| `Products[].Brand` | **bəli** | `{uid, name}`. Brend (Carhartt, Wrangler və s.). |
| `Products[].Items` | **bəli** | Variantlar (hər rəng+ölçü ayrı sətir). |
| `Items[].Color` | **bəli** | `{uid, name}`. Rəng. |
| `Items[].Size` | **bəli** | `{uid, name}`. Ölçü (M, L, 32, OS və s.). |
| `Items[].Item` | xeyr | Variantın adı/barkodu — variant SKU kimi saxlanır. |
| `Items[].ItemuUid` | tövsiyə | Variantın **sabit** identifikatoru (1C-dəki sətir ID-si). Saxlanır və **sifariş çəkəndə eyni ilə geri qaytarılır** ki, sifariş sətrini öz sistemində tanıya biləsən. Açarın yazılışı dəqiq `ItemuUid` (kiçik `u`) olmalıdır. |
| `Items[].Price` | **bəli** | Satış qiyməti (rəqəm, məs. 89.9). |
| `Items[].Quantity` | **bəli** | Stok sayı (tam ədəd). |

### Vacib qaydalar
- **Hər `uid` sabit olmalı** (məhsul, rəng, ölçü, brend, kateqoriya üçün). Sistem yeniləməni buna görə edir.
- Eyni məhsulun fərqli rəng/ölçüləri **ayrı `Items` sətirləridir**, amma eyni `Products[].uid` altında.
- Rəng/ölçü/brend/kateqoriya sistemdə yoxdursa **avtomatik yaradılır**.
- **Heç nə silinmir.** Göndərmədiyiniz məhsullara toxunulmur (qismən sinxron təhlükəsizdir).
- Qiymət: ən aşağı variant qiyməti baza qiymət olur, qalanları fərqlə saxlanır (hər variantın dəqiq qiyməti qalır).
- JSON açarlarının yazılışına diqqət: `Products`, `Category`, `Brand`, `Items`, `Color`, `Size`, `Item`, `Price`, `Quantity` — böyük hərflə; `password`, `uid`, `name` — kiçik hərflə (yuxarıdakı nümunədəki kimi).

---

## 4. Cavab (Response)

Uğurlu (`200 OK`):
```json
{
  "success": true,
  "message": "Sinxronizasiya tamamlandı: 1 yeni, 0 yeniləndi.",
  "data": {
    "productsCreated": 1,
    "productsUpdated": 0,
    "variantsCreated": 2,
    "variantsUpdated": 0,
    "brandsCreated": 0,
    "categoriesCreated": 0,
    "colorsCreated": 1,
    "sizesCreated": 0,
    "totalProductsReceived": 1,
    "totalItemsReceived": 2,
    "warnings": []
  }
}
```

- `*Created` / `*Updated` — nə qədər yaradıldı / yeniləndi.
- `warnings` — atlanan sətirlər (məs. uid-i olmayan məhsul) burada izahla göstərilir.

Xəta:
- `401` — parol yanlış/yoxdur.
- `400` — JSON formatı səhvdir.

---

## 5. Tövsiyə

İlk dəfə **5–10 məhsulluq kiçik test** göndərin, mağazada yoxlayın, sonra tam siyahını göndərin.
Sonrakı dəyişiklikləri (stok/qiymət) istənilən vaxt yenidən göndərə bilərsiniz — sistem yeniləyəcək.

---

# Geri istiqamət — məlumat çəkmək (GET)

1C **sifarişləri** və **istifadəçiləri** bizdən çəkə bilər. Bunlar **GET** sorğularıdır.
Avtorizasiya: ya `?password=PAROL` query-də, ya da `X-Sync-Key: PAROL` header-də.

Ümumi parametrlər (hamısı opsional): `page` (default 1), `pageSize` (default 100, maks 500),
`since` (ISO tarix — yalnız bu andan sonra yaranan/dəyişən qeydlər; artımlı çəkmə üçün).
Cavab həmişə səhifələnir: `{ page, pageSize, total, totalPages, items: [...] }`.

## 6. Sifarişlər — `GET /api/sync/orders`

```
GET https://code994.az/api/sync/orders?password=PAROL&page=1&pageSize=100
GET https://code994.az/api/sync/orders?password=PAROL&since=2026-06-01
```

Cavab nümunəsi:
```json
{
  "success": true,
  "data": {
    "page": 1, "pageSize": 100, "total": 3, "totalPages": 1,
    "items": [
      {
        "id": 3,
        "orderNumber": "ORD-2026-000003",
        "status": "Pending",
        "paymentStatus": "Pending",
        "paymentMethod": "Cash",
        "totalAmount": 149.00,
        "userId": 1,
        "customerFullName": "Əli Ağayev",
        "customerEmail": "musteri@example.com",
        "customerPhone": "+994503954614",
        "deliveryAddress": "Bakı, ...",
        "notes": "42 razmer zəhmət olmasa",
        "createdAt": "2026-05-26T07:24:36",
        "updatedAt": null,
        "items": [
          {
            "productExternalId": "PROD-1001",
            "itemExternalId": "58de62d8-63cc-11f1-98d0-e0d55eabb9d7",
            "productName": "New Balance 327 Bordo",
            "productSku": "SKU-...",
            "variantSku": "CARH Chase M T-Shirt",
            "colorName": "Bordo",
            "sizeName": "43",
            "quantity": 1,
            "unitPrice": 149.00,
            "totalPrice": 149.00
          }
        ]
      }
    ]
  }
}
```

- **`productExternalId`** = sizin göndərdiyiniz məhsul **uid**-idir → sifariş sətrini öz məhsulunuza bağlamaq üçün. (Əl ilə yaradılmış məhsulda boş ola bilər.)
- **`itemExternalId`** = sizin göndərdiyiniz **`ItemuUid`**-dir → konkret variantı (rəng+ölçü sətrini) tanımaq üçün ən dəqiq açar. (Əl ilə yaradılmış variantda boş ola bilər.)
- **`variantSku`** = sizin `Item` sahənizdə göndərdiyiniz dəyər.
- `status`: `Pending, Confirmed, Preparing, Shipped, Delivered, Cancelled`
- `paymentStatus`: `Pending, Paid, Failed, Refunded` · `paymentMethod`: `Cash, Card, Online`
- **Artımlı çəkmə:** son uğurlu çəkmənin vaxtını yadda saxlayın, növbəti dəfə `since` ilə göndərin.

## 7. İstifadəçilər — `GET /api/sync/users`

```
GET https://code994.az/api/sync/users?password=PAROL&page=1&pageSize=100
```

Cavab nümunəsi:
```json
{
  "success": true,
  "data": {
    "page": 1, "pageSize": 100, "total": 2, "totalPages": 1,
    "items": [
      {
        "id": 2,
        "fullName": "Demo Customer",
        "email": "customer@code994.az",
        "phoneNumber": "+994...",
        "role": "Customer",
        "isActive": true,
        "isEmailVerified": true,
        "orderCount": 0,
        "createdAt": "2026-05-21T19:08:37"
      }
    ]
  }
}
```

Şifrə və digər məxfi sahələr **qaytarılmır**. `role`: `Customer` və ya `Admin`.
