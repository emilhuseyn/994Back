using ECommerce.Application.Common;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext ctx, IPasswordHasher hasher, CancellationToken ct = default)
    {
        await ctx.Database.MigrateAsync(ct);

        await SeedUsersAsync(ctx, hasher, ct);
        await SeedColorsAsync(ctx, ct);
        await SeedSizesAsync(ctx, ct);
        await SeedBrandsAsync(ctx, ct);
        await SeedCategoriesAsync(ctx, ct);
        await SeedProductsAsync(ctx, ct);
        await SeedSlidersAsync(ctx, ct);
        await SeedSiteSettingsAsync(ctx, ct);
        await BackfillSiteSettingsAsync(ctx, ct);
        await BackfillExtraCategoriesAsync(ctx, ct);
        await BackfillExtraProductsAsync(ctx, ct);
        await BackfillEnglishAsync(ctx, ct);
    }

    /// <summary>
    /// Idempotent: inserts well-known SiteSettings rows that aren't in the DB yet.
    /// Used for content the admin can edit but ships with sensible defaults
    /// (About page text, etc.). Existing rows are never touched.
    /// </summary>
    private static async Task BackfillSiteSettingsAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        var existing = new HashSet<string>(
            await ctx.SiteSettings.Select(s => s.Key).ToListAsync(ct));

        // (key, az, ru, en)
        var defaults = new (string Key, string Az, string Ru, string En)[]
        {
            ("store.hours",
                "Hər gün 10:00 — 22:00",
                "Ежедневно 10:00 — 22:00",
                "Daily 10:00 — 22:00"),

            // Map location shown on the About page. Value is either "lat,lng"
            // coordinates (precise pin, no geocoding) or a free-text address
            // (geocoded via OSM). Admin edits it under Site settings.
            ("store.mapQuery",
                "Bakı şəhər, Səbail rayonu, Zərifə Əliyeva küçəsi 12",
                "г. Баку, Сабаильский р-н, ул. Зарифы Алиевой 12",
                "12 Zarifa Aliyeva st., Sabail district, Baku"),

            ("about.title",
                "Haqqımızda", "О нас", "About us"),

            ("about.p1",
                "Code994 — Azərbaycanda aparıcı dünya brendlərinin rəsmi distribyutorudur. Çeşidimizdə yalnız orijinal məhsullar təqdim olunur.",
                "Code994 — официальный дистрибьютор ведущих мировых брендов в Азербайджане. В нашем ассортименте представлена исключительно оригинальная продукция.",
                "Code994 is the official distributor of leading world brands in Azerbaijan. Our range features authentic products only."),

            ("about.p2",
                "Mağazamız Bakının ən mərkəzində, Zərifə Əliyeva küçəsi 12 ünvanında yerləşir. 150 ₼-dən yuxarı sifarişlərdə Bakı üzrə pulsuz çatdırılmadan istifadə edə və ya alışınızı özünüz mağazadan götürə bilərsiniz.",
                "Наш магазин расположен в самом центре Баку по адресу: ул. Зарифы Алиевой, 12. При заказе на сумму свыше 150 ₼ вы можете воспользоваться бесплатной доставкой по Баку или забрать покупку самостоятельно из магазина.",
                "Our store is located in the very centre of Baku at 12 Zarifa Aliyeva st. For orders over 150 ₼ you can use free delivery within Baku or collect your purchase from the store yourself."),

            ("about.p3",
                "Biz yüksək səviyyəli xidmət və hər müştəriyə fərdi yanaşma təmin etməyə çalışırıq. Mütəxəssislərimiz uyğun ölçü, model və stili seçməyə kömək edəcək ki, alış gözləntilərinizə tam cavab versin.",
                "Мы стремимся обеспечить высокий уровень сервиса и индивидуальный подход к каждому клиенту. Наши специалисты помогут подобрать подходящий размер, модель и стиль, чтобы покупка полностью соответствовала вашим ожиданиям.",
                "We strive to deliver a high level of service and an individual approach to every customer. Our specialists will help you pick the right size, model and style so your purchase fully meets your expectations."),

            ("about.card1.title", "Orijinallıq", "Оригинальность", "Authentic"),
            ("about.card1.body",
                "Bütün məhsullarımız orijinaldır və brendlərin rəsmi distribyutorlarından təmin olunur.",
                "Все товары оригинальные, поставляются официальными дистрибьюторами брендов.",
                "All our products are original and supplied by official brand distributors."),

            ("about.card2.title", "Pulsuz çatdırılma", "Бесплатная доставка", "Free delivery"),
            ("about.card2.body",
                "150 ₼-dən yuxarı sifariş verdikdə sifarişinizi Bakı üzrə pulsuz çatdırırıq. Həmçinin mağazamızdan özünüz götürə bilərsiniz.",
                "При заказе на сумму свыше 150 ₼ мы бесплатно доставим ваш заказ по Баку. Также вы можете воспользоваться самовывозом из нашего магазина.",
                "For orders over 150 ₼ we deliver free of charge within Baku. You can also pick up your order from our store."),

            ("about.card3.title", "Dəstək", "Поддержка", "Support"),
            ("about.card3.body",
                "Bütün suallarınız üçün biz həmişə əlaqədəyik: +99410 3151354",
                "Мы всегда на связи по любым вопросам по номеру: +99410 3151354",
                "We are always in touch for any question: +99410 3151354"),

            ("about.card4.title",
                "Bütün dünyaya çatdırılma", "Доставка по всему миру", "Worldwide delivery"),
            ("about.card4.body",
                "Sifarişləri bütün dünyaya çatdırırıq. Harada olmağınızdan asılı olmayaraq, sifariş verib etibarlı beynəlxalq çatdırılma xidmətləri vasitəsilə orijinal Code994 məhsullarını əldə edə bilərsiniz.",
                "Мы осуществляем доставку заказов по всему миру. Независимо от того, где вы находитесь, вы можете оформить заказ и получить оригинальную продукцию Code994 с помощью надежных международных служб доставки.",
                "We ship orders worldwide. Wherever you are, you can place an order and receive authentic Code994 products through reliable international delivery services."),
        };

        var toAdd = defaults
            .Where(d => !existing.Contains(d.Key))
            .Select(d => new SiteSetting
            {
                Key = d.Key,
                ValueAz = d.Az,
                ValueRu = d.Ru,
                ValueEn = d.En,
            })
            .ToList();

        if (toAdd.Count == 0) return;
        ctx.SiteSettings.AddRange(toAdd);
        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Idempotent backfill of any sub-categories from the master taxonomy that
    /// are missing from the DB. Existing categories are left untouched.
    /// </summary>
    private static async Task BackfillExtraCategoriesAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        var parents = await ctx.Categories
            .Where(c => c.ParentCategoryId == null)
            .ToDictionaryAsync(c => c.Slug, c => c, ct);
        if (parents.Count == 0) return;

        var existingSlugs = new HashSet<string>(
            await ctx.Categories.Select(c => c.Slug).ToListAsync(ct));

        // (parentSlug, nameAz, nameRu)
        var subs = new (string Parent, string NameAz, string NameRu)[]
        {
            ("geyimler", "Jiletlər", "Жилеты"),
            ("geyimler", "Longslivlər", "Лонгсливы"),
            ("geyimler", "Leggins", "Леггинсы"),
            ("geyimler", "Yubka", "Юбка"),
            ("geyimler", "Topik", "Топ"),
            ("geyimler", "Sport topiklər", "Спортивные топы"),
            ("geyimler", "Paltar", "Платья"),
            ("geyimler", "Şortlar", "Шорты"),
            ("ayaqqabilar", "İdman ayaqqabıları", "Спортивная обувь"),
            ("ayaqqabilar", "Sliponlar", "Слипоны"),
            ("aksesuarlar", "Papaqlar", "Шапки"),
            ("aksesuarlar", "Şərflər", "Шарфы"),
            ("aksesuarlar", "Bel çantalar", "Поясные сумки"),
            ("aksesuarlar", "Pul kisələri", "Кошельки"),
            ("aksesuarlar", "Mayka", "Майки"),
            ("aksesuarlar", "Çimərlik çantaları", "Пляжные сумки"),
        };

        var order = 100; // placed after the seeded ones
        var toAdd = new List<Category>();
        foreach (var (parent, nameAz, nameRu) in subs)
        {
            var slug = SlugHelper.Slugify(nameAz);
            if (existingSlugs.Contains(slug)) continue;
            if (!parents.TryGetValue(parent, out var parentCat)) continue;
            toAdd.Add(new Category
            {
                NameAz = nameAz,
                NameRu = nameRu,
                Slug = slug,
                ParentCategoryId = parentCat.Id,
                SortOrder = order++,
                IsActive = true,
            });
        }

        if (toAdd.Count == 0) return;
        ctx.Categories.AddRange(toAdd);
        await ctx.SaveChangesAsync(ct);
    }
    private static async Task SeedUsersAsync(ApplicationDbContext ctx, IPasswordHasher hasher, CancellationToken ct)
    {
        if (await ctx.Users.AnyAsync(ct)) return;
        // Seeded accounts are pre-verified so they can log in immediately.
        ctx.Users.AddRange(
            new User
            {
                FullName = "Admin",
                Email = "admin@code994.az",
                PasswordHash = hasher.Hash("Admin@123"),
                Role = UserRole.Admin,
                IsActive = true,
                IsEmailVerified = true
            },
            new User
            {
                FullName = "Demo Customer",
                Email = "customer@code994.az",
                PasswordHash = hasher.Hash("Customer@123"),
                Role = UserRole.Customer,
                IsActive = true,
                IsEmailVerified = true
            });
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task SeedColorsAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        if (await ctx.Colors.AnyAsync(ct)) return;
        var colors = new (string Az, string Ru, string Hex)[]
        {
            ("Ağ", "Белый", "#FFFFFF"),
            ("Bej", "Бежевый", "#E6D4B1"),
            ("Bordo", "Бордовый", "#7C1F2F"),
            ("Boz", "Серый", "#8B8B8B"),
            ("Göy", "Синий", "#1F3A93"),
            ("Mavi", "Голубой", "#3AA6E3"),
            ("Narıncı", "Оранжевый", "#F57E20"),
            ("Qara", "Черный", "#0C0C0C"),
            ("Qırmızı", "Красный", "#D63031"),
            ("Qəhvəyi", "Коричневый", "#5A3A1A"),
            ("Yaşıl", "Зеленый", "#2F6B3A"),
            ("Çəhrayı", "Розовый", "#F1A4B8"),
        };
        ctx.Colors.AddRange(colors.Select(c => new Color { NameAz = c.Az, NameRu = c.Ru, HexCode = c.Hex }));
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task SeedSizesAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        if (await ctx.Sizes.AnyAsync(ct)) return;
        var sizes = new[]
        {
            ("XS", 10), ("S", 20), ("M", 30), ("L", 40), ("XL", 50), ("XXL", 60), ("3XL", 70),
            ("28", 100), ("30", 110), ("31", 115), ("32", 120), ("33", 125), ("34", 130),
            ("36", 140), ("37", 145), ("38", 150), ("39", 155), ("40", 160), ("41", 165),
            ("42", 170), ("43", 175), ("44", 180), ("45", 185), ("46", 190),
            ("OS", 999)
        };
        ctx.Sizes.AddRange(sizes.Select(s => new Size { Name = s.Item1, SortOrder = s.Item2 }));
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task SeedBrandsAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        if (await ctx.Brands.AnyAsync(ct)) return;
        var names = new[]
        {
            "Asics", "Carhartt", "CP Company", "Diadora", "Dickies", "Dr.Martens",
            "Eastpak", "Ellesse", "Fila", "Fred Perry", "GStar", "Kangol",
            "Jansport", "Lee", "Napapijri", "New Balance", "Mizuno", "Patagonia",
            "Vans", "Wrangler"
        };
        ctx.Brands.AddRange(names.Select(n => new Brand
        {
            Name = n,
            Slug = SlugHelper.Slugify(n),
            IsActive = true
        }));
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task SeedCategoriesAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        if (await ctx.Categories.AnyAsync(ct)) return;

        var clothing = new Category { NameAz = "Geyimlər", NameRu = "Одежда", Slug = "geyimler", SortOrder = 1, IsActive = true };
        var shoes = new Category { NameAz = "Ayaqqabılar", NameRu = "Обувь", Slug = "ayaqqabilar", SortOrder = 2, IsActive = true };
        var accessories = new Category { NameAz = "Aksesuarlar", NameRu = "Аксессуары", Slug = "aksesuarlar", SortOrder = 3, IsActive = true };

        ctx.Categories.AddRange(clothing, shoes, accessories);
        await ctx.SaveChangesAsync(ct);

        var clothingSubs = new[]
        {
            ("Gödəkçələr", "Куртки"), ("Hudilər", "Худи"), ("Köynəklər", "Рубашки"),
            ("Sviterlər", "Свитеры"), ("Pololar", "Поло"), ("T-shirtlər", "Футболки"),
            ("Cinslər", "Джинсы"), ("Şalvarlar", "Брюки")
        };
        var shoeSubs = new[]
        {
            ("Çəkmələr", "Ботинки"), ("Krossovkalar", "Кроссовки"), ("Səndəllər", "Сандалии")
        };
        var accSubs = new[]
        {
            ("Çantalar", "Сумки"), ("Corablar", "Носки"), ("Kəmərlər", "Ремни")
        };

        int order = 1;
        foreach (var s in clothingSubs)
            ctx.Categories.Add(new Category
            {
                NameAz = s.Item1, NameRu = s.Item2,
                Slug = SlugHelper.Slugify(s.Item1),
                ParentCategoryId = clothing.Id, SortOrder = order++, IsActive = true
            });
        order = 1;
        foreach (var s in shoeSubs)
            ctx.Categories.Add(new Category
            {
                NameAz = s.Item1, NameRu = s.Item2,
                Slug = SlugHelper.Slugify(s.Item1),
                ParentCategoryId = shoes.Id, SortOrder = order++, IsActive = true
            });
        order = 1;
        foreach (var s in accSubs)
            ctx.Categories.Add(new Category
            {
                NameAz = s.Item1, NameRu = s.Item2,
                Slug = SlugHelper.Slugify(s.Item1),
                ParentCategoryId = accessories.Id, SortOrder = order++, IsActive = true
            });
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task SeedProductsAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        if (await ctx.Products.AnyAsync(ct)) return;

        var wrangler = await ctx.Brands.FirstAsync(b => b.Slug == "wrangler", ct);
        var lee = await ctx.Brands.FirstAsync(b => b.Slug == "lee", ct);
        var eastpak = await ctx.Brands.FirstAsync(b => b.Slug == "eastpak", ct);

        var sviterler = await ctx.Categories.FirstAsync(c => c.Slug == "sviterler", ct);
        var cinsler = await ctx.Categories.FirstAsync(c => c.Slug == "cinsler", ct);
        var tshirts = await ctx.Categories.FirstAsync(c => c.Slug == "t-shirtler", ct);
        var koynekler = await ctx.Categories.FirstAsync(c => c.Slug == "koynekler", ct);
        var cantalar = await ctx.Categories.FirstAsync(c => c.Slug == "cantalar", ct);

        var colorMap = await ctx.Colors.ToDictionaryAsync(c => c.NameAz, c => c, ct);
        var sizeMap = await ctx.Sizes.ToDictionaryAsync(s => s.Name, s => s, ct);

        ProductVariant V(string color, string size, int stock) => new()
        {
            ColorId = colorMap[color].Id,
            SizeId = sizeMap[size].Id,
            StockQuantity = stock,
            SKU = $"VAR-{Guid.NewGuid().ToString("N")[..6]}",
            IsActive = true
        };

        ProductImage Img(string url, int order, bool main = false) => new()
        {
            ImageUrl = url, IsMain = main, SortOrder = order
        };

        const string unsplash = "https://images.unsplash.com/";

        var products = new List<Product>
        {
            new()
            {
                NameAz = "Wrangler Men's Sign Off Crew Captain", NameRu = "Wrangler Men's Sign Off Crew Captain",
                Slug = "wrangler-mens-sign-off-crew-captain", SKU = "WR-SO-CC",
                BasePrice = 69.95m, Gender = Gender.Men, IsFeatured = true,
                BrandId = wrangler.Id, CategoryId = sviterler.Id,
                Variants = { V("Qara", "M", 10), V("Qara", "L", 8), V("Boz", "M", 5) },
                Images = { Img($"{unsplash}photo-1556821840-3a63f95609a7?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Wrangler Mom Straight Supertube", NameRu = "Wrangler Mom Straight Supertube",
                Slug = "wrangler-mom-straight-supertube", SKU = "WR-MOM-ST",
                BasePrice = 94.95m, Gender = Gender.Women,
                BrandId = wrangler.Id, CategoryId = cinsler.Id,
                Variants = { V("Göy", "30", 6), V("Göy", "32", 6), V("Mavi", "30", 4) },
                Images = { Img($"{unsplash}photo-1542272604-787c3835535d?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Wrangler Girlfriend TEE Peach Melba", NameRu = "Wrangler Girlfriend TEE Peach Melba",
                Slug = "wrangler-girlfriend-tee-peach-melba", SKU = "WR-GF-PM",
                BasePrice = 39.95m, Gender = Gender.Women,
                BrandId = wrangler.Id, CategoryId = tshirts.Id,
                Variants = { V("Çəhrayı", "S", 12), V("Çəhrayı", "M", 10) },
                Images = { Img($"{unsplash}photo-1503342217505-b0a15ec3261c?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Wrangler LS Rugged Utility Shirt", NameRu = "Wrangler LS Rugged Utility Shirt",
                Slug = "wrangler-ls-rugged-utility-shirt", SKU = "WR-LS-RU",
                BasePrice = 69.95m, Gender = Gender.Men,
                BrandId = wrangler.Id, CategoryId = koynekler.Id,
                Variants = { V("Qəhvəyi", "M", 8), V("Yaşıl", "L", 6) },
                Images = { Img($"{unsplash}photo-1602810318383-e386cc2a3ccf?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Wrangler Pocket TEE Potting Soil Brown", NameRu = "Wrangler Pocket TEE Potting Soil Brown",
                Slug = "wrangler-pocket-tee-potting-soil-brown", SKU = "WR-PT-PSB",
                BasePrice = 34.95m, Gender = Gender.Men,
                BrandId = wrangler.Id, CategoryId = tshirts.Id,
                Variants = { V("Qəhvəyi", "M", 15), V("Qəhvəyi", "L", 10) },
                Images = { Img($"{unsplash}photo-1622445275576-721325763afe?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Wrangler LS Western Shirt Dark Matcha", NameRu = "Wrangler LS Western Shirt Dark Matcha",
                Slug = "wrangler-ls-western-shirt-dark-matcha", SKU = "WR-LS-WSDM",
                BasePrice = 79.95m, Gender = Gender.Men, IsFeatured = true,
                BrandId = wrangler.Id, CategoryId = koynekler.Id,
                Variants = { V("Yaşıl", "M", 7), V("Yaşıl", "L", 5) },
                Images = { Img($"{unsplash}photo-1564584217132-2271feaeb3c5?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Wrangler TEE White", NameRu = "Wrangler TEE White",
                Slug = "wrangler-tee-white", SKU = "WR-TEE-W",
                BasePrice = 34.95m, Gender = Gender.Unisex, IsFeatured = true,
                BrandId = wrangler.Id, CategoryId = tshirts.Id,
                Variants = { V("Ağ", "S", 20), V("Ağ", "M", 18), V("Ağ", "L", 14) },
                Images = { Img($"{unsplash}photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Wrangler TEE Formula Red", NameRu = "Wrangler TEE Formula Red",
                Slug = "wrangler-tee-formula-red", SKU = "WR-TEE-FR",
                BasePrice = 34.95m, Gender = Gender.Unisex,
                BrandId = wrangler.Id, CategoryId = tshirts.Id,
                Variants = { V("Qırmızı", "S", 12), V("Qırmızı", "M", 10), V("Qırmızı", "L", 8) },
                Images = { Img($"{unsplash}photo-1576566588028-4147f3842f27?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Wrangler Americana TEE Whisper White", NameRu = "Wrangler Americana TEE Whisper White",
                Slug = "wrangler-americana-tee-whisper-white", SKU = "WR-AT-WW",
                BasePrice = 34.95m, Gender = Gender.Unisex,
                BrandId = wrangler.Id, CategoryId = tshirts.Id,
                Variants = { V("Ağ", "M", 12), V("Bej", "L", 8) },
                Images = { Img($"{unsplash}photo-1581655353564-df123a1eb820?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "Lee Carol Dark Ruby Jeans Femme", NameRu = "Lee Carol Dark Ruby Jeans Femme",
                Slug = "lee-carol-dark-ruby-jeans-femme", SKU = "LEE-CDR-J",
                BasePrice = 74.95m, Gender = Gender.Women,
                BrandId = lee.Id, CategoryId = cinsler.Id,
                Variants = { V("Bordo", "30", 5), V("Bordo", "32", 4), V("Qara", "30", 3) },
                Images = { Img($"{unsplash}photo-1541099649105-f69ad21f3246?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
            new()
            {
                NameAz = "EASTPAK DAY PAK'R", NameRu = "EASTPAK DAY PAK'R",
                Slug = "eastpak-day-pakr", SKU = "EP-DAY-PR",
                BasePrice = 139.90m, Gender = Gender.Unisex, IsFeatured = true,
                BrandId = eastpak.Id, CategoryId = cantalar.Id,
                Variants = { V("Qara", "OS", 25), V("Boz", "OS", 18) },
                Images = { Img($"{unsplash}photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=800&q=80", 0, true) }
            },
        };

        foreach (var p in products) p.IsActive = true;
        ctx.Products.AddRange(products);
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task SeedSlidersAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        if (await ctx.Sliders.AnyAsync(ct)) return;
        ctx.Sliders.AddRange(
            new Slider
            {
                TitleAz = "Yeni kolleksiya — Wrangler, Lee, Carhartt",
                TitleRu = "Новая коллекция — Wrangler, Lee, Carhartt",
                SubtitleAz = "150 ₼-dən yuxarı sifarişlər üçün Bakı üzrə pulsuz çatdırılma.",
                SubtitleRu = "Бесплатная доставка по Баку для заказов от 150 ₼.",
                ImageUrl = "https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=1800&q=80",
                ButtonTextAz = "Mağazaya keç", ButtonTextRu = "В магазин",
                ButtonUrl = "/shop", SortOrder = 1, IsActive = true
            });
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task SeedSiteSettingsAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        if (await ctx.SiteSettings.AnyAsync(ct)) return;
        ctx.SiteSettings.AddRange(
            new SiteSetting { Key = "store.name", ValueAz = "Code994 Shop", ValueRu = "Code994 Shop" },
            new SiteSetting { Key = "store.address", ValueAz = "Bakı şəhər, Səbail rayonu, Zərifə Əliyeva küçəsi 12", ValueRu = "г. Баку, Сабаильский р-н, ул. Зарифы Алиевой 12" },
            new SiteSetting { Key = "store.email", ValueAz = "code994shop@gmail.com", ValueRu = "code994shop@gmail.com" },
            new SiteSetting { Key = "store.phone", ValueAz = "+99410 3151354", ValueRu = "+99410 3151354" },
            new SiteSetting { Key = "store.announcement", ValueAz = "150 ₼-dən yuxarı SİFARİŞLƏR ÜÇÜN BAKI ÜZRƏ PULSUZ ÇATDIRILMA VƏ YA MAĞAZADAN GÖTÜRMƏ", ValueRu = "ДЛЯ ЗАКАЗОВ СВЫШЕ 150 ₼ БЕСПЛАТНАЯ ДОСТАВКА ПО БАКУ ИЛИ САМОВЫВОЗ ИЗ МАГАЗИНА" }
        );
        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Idempotent product catalogue backfill. Adds any product from the
    /// hardcoded list whose slug is missing in the DB. Existing products are
    /// left untouched so admin edits aren't reverted on restart.
    /// </summary>
    private static async Task BackfillExtraProductsAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        var brands = await ctx.Brands.ToDictionaryAsync(b => b.Slug, b => b, ct);
        var categories = await ctx.Categories.ToDictionaryAsync(c => c.Slug, c => c, ct);
        var colors = await ctx.Colors.ToDictionaryAsync(c => c.NameAz, c => c, ct);
        var sizes = await ctx.Sizes.ToDictionaryAsync(s => s.Name, s => s, ct);
        var existingSlugs = await ctx.Products.Select(p => p.Slug).ToListAsync(ct);
        var existing = new HashSet<string>(existingSlugs);

        ProductVariant V(string color, string size, int stock)
        {
            if (!colors.TryGetValue(color, out var c))
                throw new InvalidOperationException($"Color not found: {color}");
            if (!sizes.TryGetValue(size, out var s))
                throw new InvalidOperationException($"Size not found: {size}");
            return new ProductVariant
            {
                ColorId = c.Id,
                SizeId = s.Id,
                StockQuantity = stock,
                SKU = $"VAR-{Guid.NewGuid().ToString("N")[..6]}",
                IsActive = true,
            };
        }

        ProductImage Img(string url, int order = 0, bool main = true) => new()
        {
            ImageUrl = $"https://images.unsplash.com/{url}?auto=format&fit=crop&w=800&q=80",
            IsMain = main,
            SortOrder = order,
        };

        // Helper to build a product with sensible defaults. Returns null if the
        // brand or category slug isn't in the DB — caller will skip it.
        Product? P(
            string slug,
            string nameAz,
            string nameEn,
            string descriptionAz,
            string brandSlug,
            string categorySlug,
            decimal basePrice,
            Gender gender,
            string image1,
            string? image2,
            IEnumerable<ProductVariant> variants,
            decimal? discountPrice = null,
            bool isFeatured = false)
        {
            if (!brands.TryGetValue(brandSlug, out var brand)) return null;
            if (!categories.TryGetValue(categorySlug, out var category)) return null;
            var images = new List<ProductImage> { Img(image1, 0, true) };
            if (image2 is not null) images.Add(Img(image2, 1, false));
            return new Product
            {
                Slug = slug,
                NameAz = nameAz, NameRu = nameAz, NameEn = nameEn,
                DescriptionAz = descriptionAz,
                DescriptionRu = descriptionAz,
                DescriptionEn = nameEn,
                SKU = $"SKU-{slug[..Math.Min(slug.Length, 10)].ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..4]}",
                BasePrice = basePrice,
                DiscountPrice = discountPrice,
                Gender = gender,
                BrandId = brand.Id,
                CategoryId = category.Id,
                IsActive = true,
                IsFeatured = isFeatured,
                Variants = variants.ToList(),
                Images = images,
            };
        }

        var additions = new List<Product?>
        {
            // ── Wrangler ────────────────────────────────────────────────────
            P("wrangler-cargo-shorts-blue", "Wrangler Cargo Shorts Blue", "Wrangler Cargo Shorts Blue",
                "Cargo Wrangler şortlar — rahat, çoxlu cibli, möhkəm parça.", "wrangler", "sortlar",
                49.95m, Gender.Men,
                "photo-1591195853828-11db59a44f6b", "photo-1473966968600-fa801b869a1a",
                new[] { V("Göy", "30", 8), V("Göy", "32", 6), V("Göy", "34", 4) }),
            P("wrangler-denim-skirt", "Wrangler Denim Skirt", "Wrangler Denim Skirt",
                "Klassik Wrangler denim yubka, A formada kəsim.", "wrangler", "yubka",
                59.95m, Gender.Women,
                "photo-1577900232427-18219b8349f9", "photo-1542272604-787c3835535d",
                new[] { V("Göy", "S", 6), V("Göy", "M", 6), V("Göy", "L", 4) }, isFeatured: true),
            P("wrangler-leggins-black", "Wrangler Leggins Black", "Wrangler Black Leggings",
                "Lee qadın leggins, klassik qara, esnek pambıq.", "wrangler", "leggins",
                39.95m, Gender.Women,
                "photo-1591195853828-11db59a44f6b", null,
                new[] { V("Qara", "XS", 8), V("Qara", "S", 10), V("Qara", "M", 8) }),
            P("wrangler-longsleeve-grey", "Wrangler Longsleeve Grey", "Wrangler Grey Longsleeve",
                "Klassik boz uzunqollu pambıq longsleeve, gündəlik geyim üçün.", "wrangler", "longslivler",
                44.95m, Gender.Unisex,
                "photo-1581655353564-df123a1eb820", "photo-1622445275576-721325763afe",
                new[] { V("Boz", "S", 10), V("Boz", "M", 10), V("Boz", "L", 8), V("Boz", "XL", 6) }),

            // ── Lee ─────────────────────────────────────────────────────────
            P("lee-rider-jeans-blue", "Lee Rider Slim Jeans", "Lee Rider Slim Jeans",
                "Lee ikonik Rider Slim oturuş — vintage denim hissi.", "lee", "cinsler",
                89.0m, Gender.Men,
                "photo-1542272604-787c3835535d", "photo-1541099649105-f69ad21f3246",
                new[] { V("Göy", "30", 6), V("Göy", "32", 8), V("Göy", "34", 6) }, isFeatured: true),
            P("lee-mom-dress-blue", "Lee Mom Denim Dress", "Lee Mom Denim Dress",
                "Lee denim paltar, Mom kəsim, klassik düymələrlə.", "lee", "paltar",
                109.0m, Gender.Women,
                "photo-1591195853828-11db59a44f6b", "photo-1577900232427-18219b8349f9",
                new[] { V("Göy", "XS", 4), V("Göy", "S", 6), V("Göy", "M", 5) }),
            P("lee-tank-mayka-white", "Lee Tank Mayka White", "Lee Tank Top White",
                "Klassik ağ tank mayka, 100% pambıq.", "lee", "mayka",
                24.95m, Gender.Unisex,
                "photo-1581655353564-df123a1eb820", null,
                new[] { V("Ağ", "S", 15), V("Ağ", "M", 18), V("Ağ", "L", 10) }),

            // ── Carhartt ────────────────────────────────────────────────────
            P("carhartt-active-jacket", "Carhartt Active Jacket Brown", "Carhartt Active Jacket Brown",
                "Klassik Carhartt iş gödəkçəsi, möhkəm canvas, isidici astar.", "carhartt", "godekceler",
                189.95m, Gender.Men,
                "photo-1551028719-00167b16eac5", "photo-1591047139829-d91aecb6caea",
                new[] { V("Qəhvəyi", "M", 6), V("Qəhvəyi", "L", 5), V("Qara", "L", 4), V("Qara", "XL", 3) }, isFeatured: true),
            P("carhartt-watch-hat", "Carhartt Watch Hat", "Carhartt Watch Hat",
                "Klassik akrillik Carhartt watch hat, soyuq havalar üçün.", "carhartt", "papaqlar",
                35.0m, Gender.Unisex,
                "photo-1521369909029-2afed882baee", null,
                new[] { V("Qara", "OS", 20), V("Bordo", "OS", 12), V("Yaşıl", "OS", 10) }),
            P("carhartt-work-pants", "Carhartt Double Knee Pants", "Carhartt Double Knee Pants",
                "Carhartt ikiqat dizli iş şalvarı, möhkəm canvas.", "carhartt", "salvarlar",
                145.0m, Gender.Men,
                "photo-1473966968600-fa801b869a1a", null,
                new[] { V("Bej", "32", 5), V("Bej", "34", 5), V("Qara", "32", 4) }),

            // ── Dickies ─────────────────────────────────────────────────────
            P("dickies-work-shirt-beige", "Dickies Work Shirt Beige", "Dickies Work Shirt Beige",
                "Klassik iş köynəyi, möhkəm pambıq, uzun ömür.", "dickies", "koynekler",
                54.95m, Gender.Men,
                "photo-1602810316693-3667c854239a", "photo-1620012253295-c15cc3e65df4",
                new[] { V("Bej", "M", 8), V("Bej", "L", 7), V("Boz", "M", 6) }),
            P("dickies-874-work-pants", "Dickies 874 Original Work Pants", "Dickies 874 Original Work Pants",
                "Klassik Dickies 874 iş şalvarı, geniş kəsim.", "dickies", "salvarlar",
                79.0m, Gender.Men,
                "photo-1473966968600-fa801b869a1a", "photo-1591195853828-11db59a44f6b",
                new[] { V("Bej", "30", 6), V("Bej", "32", 8), V("Qara", "32", 5), V("Yaşıl", "34", 4) }, isFeatured: true),

            // ── CP Company ──────────────────────────────────────────────────
            P("cp-company-goggle-hoodie", "CP Company Goggle Hoodie", "CP Company Goggle Hoodie",
                "İkonik CP Company goggle hoodie, qol göz lensli.", "cp-company", "hudiler",
                349.0m, Gender.Men, isFeatured: true,
                image1: "photo-1556821840-3a63f95609a7", image2: "photo-1620799140408-edc6dcb6d633",
                variants: new[] { V("Qara", "M", 3), V("Qara", "L", 3), V("Yaşıl", "L", 2) }),
            P("cp-company-lens-jacket", "CP Company Lens Jacket", "CP Company Lens Jacket",
                "CP Company yüngül lens gödəkçə, qol göz lensli.", "cp-company", "godekceler",
                449.0m, Gender.Men,
                "photo-1591047139829-d91aecb6caea", null,
                new[] { V("Yaşıl", "M", 2), V("Yaşıl", "L", 2), V("Qara", "L", 2) }),

            // ── Dr.Martens ──────────────────────────────────────────────────
            P("dr-martens-1460-boots-black", "Dr.Martens 1460 Boots Black", "Dr.Martens 1460 Boots Black",
                "İkonik 1460 çəkmələr, möhkəm dəri, klassik oturuş.", "drmartens", "cekmeler",
                249.0m, Gender.Unisex, isFeatured: true,
                image1: "photo-1605812860427-4024433a70fd", image2: "photo-1542838132-92c53300491e",
                variants: new[] { V("Qara", "38", 4), V("Qara", "40", 6), V("Qara", "42", 5), V("Qara", "44", 3) }),
            P("dr-martens-1461-shoes", "Dr.Martens 1461 Oxford Shoes", "Dr.Martens 1461 Oxford Shoes",
                "3-deşikli klassik Dr.Martens Oxford ayaqqabıları.", "drmartens", "krossovkalar",
                219.0m, Gender.Unisex,
                "photo-1542291026-7eec264c27ff", null,
                new[] { V("Qara", "39", 3), V("Qara", "41", 4), V("Qəhvəyi", "42", 2) }),
            P("dr-martens-sandals-jorge", "Dr.Martens Jorge Sandals", "Dr.Martens Jorge Sandals",
                "Klassik Dr.Martens dəri səndəllər.", "drmartens", "sendeller",
                139.0m, Gender.Women,
                "photo-1542838132-92c53300491e", null,
                new[] { V("Qara", "38", 4), V("Qara", "40", 4), V("Qəhvəyi", "39", 3) }),

            // ── New Balance ─────────────────────────────────────────────────
            P("new-balance-574-grey", "New Balance 574 Grey", "New Balance 574 Grey",
                "Klassik 574 krossovkalar — rahat amortizasiya, gündəlik istifadə.", "new-balance", "krossovkalar",
                159.0m, Gender.Unisex, isFeatured: true,
                image1: "photo-1542291026-7eec264c27ff", image2: "photo-1595950653106-6c9ebd614d3a",
                variants: new[] { V("Boz", "38", 5), V("Boz", "40", 7), V("Boz", "42", 6), V("Boz", "44", 4), V("Göy", "42", 3) }),
            P("new-balance-990v6", "New Balance 990v6 Grey", "New Balance 990v6 Grey",
                "Premium New Balance 990 v6, made in USA, ENCAP texnologiyası.", "new-balance", "krossovkalar",
                319.0m, Gender.Men,
                "photo-1539185441755-769473a23570", "photo-1595950653106-6c9ebd614d3a",
                new[] { V("Boz", "40", 3), V("Boz", "42", 4), V("Boz", "44", 3) }, discountPrice: 279.0m),
            P("new-balance-2002r", "New Balance 2002R Phantom", "New Balance 2002R Phantom",
                "2002R — vintage retro krossovkalar, premium suede.", "new-balance", "krossovkalar",
                249.0m, Gender.Unisex,
                "photo-1606107557195-0e29a4b5b4aa", null,
                new[] { V("Boz", "40", 4), V("Boz", "42", 4), V("Qara", "43", 3) }),

            // ── Asics ───────────────────────────────────────────────────────
            P("asics-gel-lyte-iii", "Asics Gel-Lyte III White", "Asics Gel-Lyte III White",
                "Vintage Gel-Lyte III — bölünmüş dil, GEL amortizasiya.", "asics", "krossovkalar",
                179.0m, Gender.Unisex, isFeatured: true,
                image1: "photo-1606107557195-0e29a4b5b4aa", image2: "photo-1542291026-7eec264c27ff",
                variants: new[] { V("Ağ", "39", 4), V("Ağ", "41", 5), V("Boz", "42", 4) }),
            P("asics-gel-kayano-30", "Asics Gel-Kayano 30", "Asics Gel-Kayano 30",
                "Yüksək amortizasiyalı qaçış ayaqqabıları, gündəlik məsafələr üçün.", "asics", "idman-ayaqqabilari",
                249.0m, Gender.Men,
                "photo-1595950653106-6c9ebd614d3a", null,
                new[] { V("Qara", "41", 4), V("Qara", "43", 4), V("Mavi", "42", 3) }),
            P("asics-running-socks", "Asics Running Socks 3-pack", "Asics Running Socks 3-pack",
                "Yüngül qaçış corabları, üçlü qutu.", "asics", "corablar",
                15.0m, Gender.Unisex,
                "photo-1582966772680-860e372bb558", null,
                new[] { V("Ağ", "S", 30), V("Ağ", "M", 30), V("Qara", "M", 25) }),

            // ── Vans ────────────────────────────────────────────────────────
            P("vans-old-skool-black", "Vans Old Skool Black/White", "Vans Old Skool Black/White",
                "Klassik Vans Old Skool, ikonik yan zolaq.", "vans", "sliponlar",
                99.0m, Gender.Unisex, isFeatured: true,
                image1: "photo-1525966222134-fcfa99b8ae77", image2: "photo-1607522370275-f14206abe5d3",
                variants: new[] { V("Qara", "38", 6), V("Qara", "40", 8), V("Qara", "42", 7), V("Ağ", "41", 4) }),
            P("vans-classic-slip-on-checker", "Vans Classic Slip-On Checkerboard", "Vans Classic Slip-On Checkerboard",
                "Klassik Vans slip-on, şahmat naxışlı.", "vans", "sliponlar",
                79.0m, Gender.Unisex,
                "photo-1525966222134-fcfa99b8ae77", null,
                new[] { V("Qara", "38", 5), V("Qara", "40", 6), V("Qara", "42", 5) }),
            P("vans-sk8-hi", "Vans SK8-Hi Black", "Vans SK8-Hi Black",
                "Yüksək boğazlı klassik Vans SK8-Hi.", "vans", "krossovkalar",
                109.0m, Gender.Unisex,
                "photo-1607522370275-f14206abe5d3", null,
                new[] { V("Qara", "39", 3), V("Qara", "41", 5), V("Qara", "43", 4) }),

            // ── Fila ────────────────────────────────────────────────────────
            P("fila-disruptor-ii-white", "Fila Disruptor II White", "Fila Disruptor II White",
                "Chunky Fila Disruptor II — 90s nostalgiyası.", "fila", "krossovkalar",
                129.0m, Gender.Women,
                "photo-1595950653106-6c9ebd614d3a", "photo-1539185441755-769473a23570",
                new[] { V("Ağ", "37", 4), V("Ağ", "39", 5), V("Ağ", "41", 4), V("Çəhrayı", "38", 3) }),
            P("fila-track-jacket", "Fila Track Jacket Vintage", "Fila Track Jacket Vintage",
                "Vintage Fila track jacket — retro idman uslubu.", "fila", "godekceler",
                119.0m, Gender.Unisex,
                "photo-1551028719-00167b16eac5", null,
                new[] { V("Ağ", "M", 4), V("Ağ", "L", 4), V("Mavi", "M", 3) }),

            // ── Fred Perry ──────────────────────────────────────────────────
            P("fred-perry-polo-twin-tipped-black", "Fred Perry Polo Twin Tipped Black", "Fred Perry Polo Twin Tipped Black",
                "Klassik Twin Tipped polo köynək — ikonik dəfnə yarpağı loqo.", "fred-perry", "pololar",
                119.0m, Gender.Men, isFeatured: true,
                image1: "photo-1583743814966-8936f5b7be1a", image2: "photo-1603252109303-2751441dd157",
                variants: new[] { V("Qara", "S", 6), V("Qara", "M", 8), V("Qara", "L", 6), V("Yaşıl", "M", 4) }),
            P("fred-perry-track-jacket-burgundy", "Fred Perry Track Jacket Burgundy", "Fred Perry Track Jacket Burgundy",
                "Klassik Fred Perry track jacket, twin tipped zolaqlar.", "fred-perry", "godekceler",
                159.0m, Gender.Men,
                "photo-1551028719-00167b16eac5", null,
                new[] { V("Bordo", "M", 4), V("Bordo", "L", 4), V("Qara", "M", 3) }),

            // ── Napapijri ───────────────────────────────────────────────────
            P("napapijri-rainforest-jacket", "Napapijri Rainforest Pocket Jacket", "Napapijri Rainforest Pocket Jacket",
                "Klassik Napapijri Rainforest gödəkçə, sudayan parça.", "napapijri", "godekceler",
                229.0m, Gender.Men,
                "photo-1591047139829-d91aecb6caea", "photo-1551028719-00167b16eac5",
                new[] { V("Göy", "M", 4), V("Göy", "L", 4), V("Qara", "L", 3), V("Yaşıl", "XL", 2) }),
            P("napapijri-hoodie-vermont", "Napapijri Vermont Hoodie", "Napapijri Vermont Hoodie",
                "Yumşaq Vermont logo hoodie.", "napapijri", "hudiler",
                129.0m, Gender.Unisex,
                "photo-1556821840-3a63f95609a7", null,
                new[] { V("Boz", "M", 5), V("Boz", "L", 4), V("Yaşıl", "L", 3) }),

            // ── Patagonia ───────────────────────────────────────────────────
            P("patagonia-retro-x-fleece", "Patagonia Retro-X Fleece Pullover", "Patagonia Retro-X Fleece Pullover",
                "Patagonia ikonik Retro-X fleece pullover.", "patagonia", "sviterler",
                149.0m, Gender.Unisex, isFeatured: true,
                image1: "photo-1620799140408-edc6dcb6d633", image2: "photo-1556821840-3a63f95609a7",
                variants: new[] { V("Bej", "M", 4), V("Bej", "L", 4), V("Yaşıl", "L", 3) }),
            P("patagonia-fleece-vest", "Patagonia Retro Pile Vest", "Patagonia Retro Pile Vest",
                "Patagonia retro fleece jilet — yüngül və isti.", "patagonia", "jiletler",
                119.0m, Gender.Unisex,
                "photo-1591047139829-d91aecb6caea", null,
                new[] { V("Boz", "S", 4), V("Boz", "M", 5), V("Qara", "L", 3) }),

            // ── GStar ───────────────────────────────────────────────────────
            P("gstar-3301-slim-jeans", "G-Star 3301 Slim Jeans", "G-Star 3301 Slim Jeans",
                "G-Star 3301 slim oturuşlu cins şalvar.", "gstar", "cinsler",
                134.0m, Gender.Men,
                "photo-1542272604-787c3835535d", "photo-1541099649105-f69ad21f3246",
                new[] { V("Göy", "30", 5), V("Göy", "32", 6), V("Göy", "34", 5), V("Qara", "32", 4) }),
            P("gstar-cargo-pants-green", "G-Star Cargo Pants Green", "G-Star Cargo Pants Green",
                "G-Star cargo şalvarı, çoxlu cib, möhkəm parça.", "gstar", "salvarlar",
                149.0m, Gender.Men,
                "photo-1473966968600-fa801b869a1a", null,
                new[] { V("Yaşıl", "30", 3), V("Yaşıl", "32", 5), V("Bej", "34", 3) }),

            // ── Diadora ─────────────────────────────────────────────────────
            P("diadora-n9000-sneakers", "Diadora N9000 Italia White", "Diadora N9000 Italia White",
                "Retro Diadora N9000 krossovkaları — italyan dizaynı.", "diadora", "krossovkalar",
                145.0m, Gender.Unisex,
                "photo-1606107557195-0e29a4b5b4aa", "photo-1542291026-7eec264c27ff",
                new[] { V("Ağ", "39", 4), V("Ağ", "41", 5), V("Boz", "42", 4) }),

            // ── Mizuno ──────────────────────────────────────────────────────
            P("mizuno-wave-rider-26", "Mizuno Wave Rider 26", "Mizuno Wave Rider 26",
                "Mizuno Wave Rider qaçış ayaqqabıları, Wave plate texnologiyası.", "mizuno", "idman-ayaqqabilari",
                169.0m, Gender.Men,
                "photo-1595950653106-6c9ebd614d3a", "photo-1539185441755-769473a23570",
                new[] { V("Boz", "41", 4), V("Boz", "43", 4), V("Mavi", "42", 3) }),

            // ── Ellesse ─────────────────────────────────────────────────────
            P("ellesse-heritage-hoodie", "Ellesse Heritage Hoodie Orange", "Ellesse Heritage Hoodie Orange",
                "Yumşaq Heritage hoodie — klassik Ellesse loqo.", "ellesse", "hudiler",
                99.95m, Gender.Unisex,
                "photo-1556821840-3a63f95609a7", "photo-1620799140408-edc6dcb6d633",
                new[] { V("Narıncı", "M", 5), V("Narıncı", "L", 5), V("Boz", "M", 4), V("Qara", "L", 4) }),
            P("ellesse-track-pants", "Ellesse Track Pants Navy", "Ellesse Track Pants Navy",
                "Klassik Ellesse track pants, retro idman uslubu.", "ellesse", "salvarlar",
                79.0m, Gender.Unisex,
                "photo-1591195853828-11db59a44f6b", null,
                new[] { V("Qara", "S", 5), V("Qara", "M", 6), V("Boz", "L", 4) }),

            // ── Kangol ──────────────────────────────────────────────────────
            P("kangol-bermuda-bucket-hat", "Kangol Bermuda Bucket Hat", "Kangol Bermuda Bucket Hat",
                "İkonik Kangol bucket papaq.", "kangol", "papaqlar",
                79.0m, Gender.Unisex,
                "photo-1521369909029-2afed882baee", "photo-1556306535-0f09a537f0a3",
                new[] { V("Qara", "OS", 12), V("Bej", "OS", 10), V("Yaşıl", "OS", 8) }),
            P("kangol-504-tropic", "Kangol 504 Tropic Cap", "Kangol 504 Tropic Cap",
                "Klassik Kangol 504 forma papaq, tropic seriyası.", "kangol", "papaqlar",
                89.0m, Gender.Unisex,
                "photo-1556306535-0f09a537f0a3", null,
                new[] { V("Qara", "OS", 8), V("Boz", "OS", 7) }),
            P("kangol-beach-bag", "Kangol Beach Tote", "Kangol Beach Tote",
                "Yüngül çimərlik çantası — yay üçün ideal.", "kangol", "cimerlik-cantalari",
                55.0m, Gender.Unisex,
                "photo-1601762603339-fd61e28b698a", null,
                new[] { V("Ağ", "OS", 8), V("Bej", "OS", 6) }),

            // ── Jansport ────────────────────────────────────────────────────
            P("jansport-superbreak-backpack", "JanSport SuperBreak Backpack", "JanSport SuperBreak Backpack",
                "JanSport-un ən populyar bel çantası — yüngül və möhkəm.", "jansport", "cantalar",
                89.0m, Gender.Unisex,
                "photo-1553062407-98eeb64c6a62", "photo-1622560480605-d83c853bc5c3",
                new[] { V("Qara", "OS", 15), V("Göy", "OS", 12), V("Bordo", "OS", 8) }),
            P("jansport-right-pack", "JanSport Right Pack", "JanSport Right Pack",
                "Klassik dəri əsaslı JanSport — orijinal forma.", "jansport", "cantalar",
                129.0m, Gender.Unisex,
                "photo-1622560480605-d83c853bc5c3", null,
                new[] { V("Qara", "OS", 10), V("Boz", "OS", 8) }),
            P("jansport-knit-scarf", "JanSport Knit Scarf", "JanSport Knit Scarf",
                "Yumşaq toxunma şərf — soyuq günlər üçün.", "jansport", "serfler",
                29.0m, Gender.Unisex,
                "photo-1601762603339-fd61e28b698a", null,
                new[] { V("Boz", "OS", 20), V("Bordo", "OS", 15) }),

            // ── Eastpak ─────────────────────────────────────────────────────
            P("eastpak-padded-pakr-black", "Eastpak Padded Pak'r Black", "Eastpak Padded Pak'r Black",
                "Yastıqlı Eastpak çantası — gündəlik və laptop üçün.", "eastpak", "cantalar",
                119.0m, Gender.Unisex,
                "photo-1622560480605-d83c853bc5c3", "photo-1553062407-98eeb64c6a62",
                new[] { V("Qara", "OS", 18), V("Mavi", "OS", 12) }),
            P("eastpak-bumbag-orbit", "Eastpak Bumbag Orbit", "Eastpak Bumbag Orbit",
                "Eastpak klassik bel çantası.", "eastpak", "bel-cantalar",
                49.0m, Gender.Unisex,
                "photo-1553062407-98eeb64c6a62", null,
                new[] { V("Qara", "OS", 15), V("Göy", "OS", 10) }),
            P("eastpak-wallet", "Eastpak Bifold Wallet", "Eastpak Bifold Wallet",
                "Klassik kətan pul kisəsi.", "eastpak", "pul-kiseleri",
                35.0m, Gender.Unisex,
                "photo-1627123424574-724758594e93", null,
                new[] { V("Qara", "OS", 25), V("Bordo", "OS", 15) }),

            // ── More accessories scattered across brands ─────────────────────
            P("wrangler-leather-belt", "Wrangler Leather Belt Brown", "Wrangler Leather Belt Brown",
                "Klassik dəri Wrangler kəmər.", "wrangler", "kemerler",
                45.0m, Gender.Men,
                "photo-1624222247344-550fb60583dc", "photo-1611923853013-1d2bce03c8c0",
                new[] { V("Qəhvəyi", "M", 10), V("Qəhvəyi", "L", 8), V("Qara", "M", 6) }),
            P("lee-leather-wallet", "Lee Leather Bifold Wallet", "Lee Leather Bifold Wallet",
                "Klassik Lee dəri pul kisəsi.", "lee", "pul-kiseleri",
                39.0m, Gender.Unisex,
                "photo-1627123424574-724758594e93", null,
                new[] { V("Qəhvəyi", "OS", 12), V("Qara", "OS", 10) }),

            // ── Discounted / sport tops ──────────────────────────────────────
            P("wrangler-crop-topik", "Wrangler Crop Topik White", "Wrangler Crop Topik White",
                "Crop Wrangler topik — yay üçün ideal.", "wrangler", "topik",
                29.95m, Gender.Women,
                "photo-1521572163474-6864f9cf17ab", null,
                new[] { V("Ağ", "XS", 10), V("Ağ", "S", 12), V("Çəhrayı", "M", 8) }),
            P("wrangler-sport-top", "Wrangler Sport Top", "Wrangler Sport Top",
                "Yüngül və rahat Wrangler sport top.", "wrangler", "sport-topikler",
                39.95m, Gender.Women,
                "photo-1591195853828-11db59a44f6b", null,
                new[] { V("Qara", "S", 10), V("Çəhrayı", "M", 8), V("Ağ", "L", 6) }),

            // ── Final sneakers + boots variety ───────────────────────────────
            P("vans-era-classic", "Vans Era Black", "Vans Era Black",
                "Klassik Vans Era — alçaq profilli sneaker.", "vans", "sliponlar",
                85.0m, Gender.Unisex,
                "photo-1525966222134-fcfa99b8ae77", null,
                new[] { V("Qara", "39", 4), V("Qara", "41", 5), V("Qara", "43", 4) }),
            P("new-balance-327-bordo", "New Balance 327 Bordo", "New Balance 327 Bordo",
                "Retro-inspired New Balance 327 — böyük \"N\" loqo.", "new-balance", "krossovkalar",
                189.0m, Gender.Unisex,
                "photo-1539185441755-769473a23570", null,
                new[] { V("Bordo", "39", 3), V("Bordo", "41", 4), V("Bordo", "43", 3) }, discountPrice: 149.0m, isFeatured: true),
            P("asics-onitsuka-tiger-mexico-66", "Onitsuka Tiger Mexico 66", "Onitsuka Tiger Mexico 66",
                "Vintage Mexico 66 — ikonik onitsuka şərq cizgisi.", "asics", "krossovkalar",
                155.0m, Gender.Unisex,
                "photo-1606107557195-0e29a4b5b4aa", null,
                new[] { V("Ağ", "39", 4), V("Ağ", "41", 5), V("Ağ", "43", 4) }),
        };

        var newProducts = additions
            .Where(p => p is not null && !existing.Contains(p.Slug))
            .Select(p => p!)
            .ToList();
        if (newProducts.Count == 0) return;

        ctx.Products.AddRange(newProducts);
        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Idempotent backfill of NameEn / DescriptionEn / TitleEn / ValueEn for the
    /// seeded data. Runs every startup but only touches rows where the English
    /// field is NULL — manual admin edits won't be overwritten.
    /// </summary>
    private static async Task BackfillEnglishAsync(ApplicationDbContext ctx, CancellationToken ct)
    {
        // Categories
        var categoryEn = new Dictionary<string, string>
        {
            ["geyimler"] = "Clothing",
            ["ayaqqabilar"] = "Shoes",
            ["aksesuarlar"] = "Accessories",
            ["godekceler"] = "Jackets",
            ["hudiler"] = "Hoodies",
            ["koynekler"] = "Shirts",
            ["sviterler"] = "Sweaters",
            ["pololar"] = "Polos",
            ["t-shirtler"] = "T-shirts",
            ["cinsler"] = "Jeans",
            ["salvarlar"] = "Trousers",
            ["cekmeler"] = "Boots",
            ["krossovkalar"] = "Sneakers",
            ["sendeller"] = "Sandals",
            ["cantalar"] = "Bags",
            ["corablar"] = "Socks",
            ["kemerler"] = "Belts",
        };
        var categories = await ctx.Categories.Where(c => c.NameEn == null).ToListAsync(ct);
        foreach (var c in categories)
        {
            if (categoryEn.TryGetValue(c.Slug, out var en))
                c.NameEn = en;
        }

        // Colors
        var colorEn = new Dictionary<string, string>
        {
            ["Ağ"] = "White",
            ["Bej"] = "Beige",
            ["Bordo"] = "Burgundy",
            ["Boz"] = "Grey",
            ["Göy"] = "Blue",
            ["Mavi"] = "Sky blue",
            ["Narıncı"] = "Orange",
            ["Qara"] = "Black",
            ["Qırmızı"] = "Red",
            ["Qəhvəyi"] = "Brown",
            ["Yaşıl"] = "Green",
            ["Çəhrayı"] = "Pink",
        };
        var colors = await ctx.Colors.Where(c => c.NameEn == null).ToListAsync(ct);
        foreach (var c in colors)
        {
            if (colorEn.TryGetValue(c.NameAz, out var en))
                c.NameEn = en;
        }

        // Products — translated names + descriptions
        var productEn = new Dictionary<string, (string Name, string Desc)>
        {
            ["wrangler-mens-sign-off-crew-captain"] = ("Wrangler Men's Sign Off Crew Captain",
                "Classic Wrangler crew sweater. Soft cotton blend, ideal for daily wear."),
            ["wrangler-mom-straight-supertube"] = ("Wrangler Mom Straight Supertube",
                "High-waisted, straight fit Mom jeans. Vintage feel with a modern cut."),
            ["wrangler-girlfriend-tee-peach-melba"] = ("Wrangler Girlfriend TEE Peach Melba",
                "Soft, relaxed-fit girlfriend t-shirt for daily comfort."),
            ["wrangler-ls-rugged-utility-shirt"] = ("Wrangler LS Rugged Utility Shirt",
                "Long-sleeve utility shirt with two chest pockets, sturdy fabric, classic denim style."),
            ["wrangler-pocket-tee-potting-soil-brown"] = ("Wrangler Pocket TEE Potting Soil Brown",
                "Classic t-shirt with a chest pocket. Pure cotton, comfortable fit."),
            ["wrangler-ls-western-shirt-dark-matcha"] = ("Wrangler LS Western Shirt Dark Matcha",
                "Classic long-sleeve western-style shirt with snap buttons."),
            ["wrangler-tee-white"] = ("Wrangler TEE White", "Classic white Wrangler t-shirt. Pairs with anything."),
            ["wrangler-tee-formula-red"] = ("Wrangler TEE Formula Red", "Classic red t-shirt. 100% cotton."),
            ["wrangler-americana-tee-whisper-white"] = ("Wrangler Americana TEE Whisper White",
                "Wrangler t-shirt with Americana graphic. Soft cotton."),
            ["lee-carol-dark-ruby-jeans-femme"] = ("Lee Carol Dark Ruby Jeans Femme",
                "Lee Carol women's jeans, classic cut, comfortable fit."),
            ["eastpak-day-pakr"] = ("EASTPAK DAY PAK'R",
                "Eastpak Day Pak'r — sturdy and compact backpack for everyday use."),
        };
        var products = await ctx.Products.Where(p => p.NameEn == null).ToListAsync(ct);
        foreach (var p in products)
        {
            if (productEn.TryGetValue(p.Slug, out var en))
            {
                p.NameEn = en.Name;
                p.DescriptionEn = en.Desc;
            }
        }

        // Sliders
        var sliders = await ctx.Sliders.Where(s => s.TitleEn == null).ToListAsync(ct);
        foreach (var s in sliders)
        {
            s.TitleEn = "New collection — Wrangler, Lee, Carhartt";
            s.SubtitleEn = "Free delivery in Baku for orders over 150 ₼, or pick up from our store.";
            s.ButtonTextEn = "Shop now";
        }

        // Site settings
        var settingEn = new Dictionary<string, string>
        {
            ["store.name"] = "Code994 Shop",
            ["store.address"] = "12 Zarifa Aliyeva st., Sabail district, Baku",
            ["store.email"] = "code994shop@gmail.com",
            ["store.phone"] = "+99410 3151354",
            ["store.announcement"] = "FREE DELIVERY IN BAKU OR IN-STORE PICKUP FOR ORDERS OVER 150 ₼",
        };
        var settings = await ctx.SiteSettings.Where(s => s.ValueEn == null).ToListAsync(ct);
        foreach (var s in settings)
        {
            if (settingEn.TryGetValue(s.Key, out var en))
                s.ValueEn = en;
        }

        await ctx.SaveChangesAsync(ct);
    }
}
