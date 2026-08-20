-- Code994 — site content sync, 2026-08-11 (client copy batch).
--
-- Applies the About-page / store texts the client approved. The seeder never
-- overwrites SiteSettings rows that already exist, so the live rows have to be
-- written here once.
--
-- Run ONCE, by hand. Do NOT put this in deploy.sh — it would revert every
-- admin-panel edit on the next deploy.
--
--   mysqldump -u root -p Shopping SiteSettings > /root/sitesettings-backup.sql
--   mysql -u root -p Shopping < /opt/994Back/scripts/content-2026-08-11.sql

SET NAMES utf8mb4;

INSERT INTO `SiteSettings` (`Key`, `ValueAz`, `ValueRu`, `ValueEn`, `CreatedAt`) VALUES
('about.p1',
 'Code994 — Azərbaycanda aparıcı dünya brendlərinin rəsmi distribyutorudur. Çeşidimizdə yalnız orijinal məhsullar təqdim olunur.',
 'Code994 — официальный дистрибьютор ведущих мировых брендов в Азербайджане. В нашем ассортименте представлена исключительно оригинальная продукция.',
 'Code994 is the official distributor of leading world brands in Azerbaijan. Our range features authentic products only.',
 UTC_TIMESTAMP()),

('about.p2',
 'Mağazamız Bakının ən mərkəzində, Zərifə Əliyeva küçəsi 12 ünvanında yerləşir. 100 ₼-dən yuxarı sifarişlərdə Bakı üzrə pulsuz çatdırılmadan istifadə edə və ya alışınızı özünüz mağazadan götürə bilərsiniz.',
 'Наш магазин расположен в самом центре Баку по адресу: ул. Зарифы Алиевой, 12. При заказе на сумму свыше 100 ₼ вы можете воспользоваться бесплатной доставкой по Баку или забрать покупку самостоятельно из магазина.',
 'Our store is located in the very centre of Baku at 12 Zarifa Aliyeva st. For orders over 100 ₼ you can use free delivery within Baku or collect your purchase from the store yourself.',
 UTC_TIMESTAMP()),

('about.p3',
 'Biz yüksək səviyyəli xidmət və hər müştəriyə fərdi yanaşma təmin etməyə çalışırıq. Mütəxəssislərimiz uyğun ölçü, model və stili seçməyə kömək edəcək ki, alış gözləntilərinizə tam cavab versin.',
 'Мы стремимся обеспечить высокий уровень сервиса и индивидуальный подход к каждому клиенту. Наши специалисты помогут подобрать подходящий размер, модель и стиль, чтобы покупка полностью соответствовала вашим ожиданиям.',
 'We strive to deliver a high level of service and an individual approach to every customer. Our specialists will help you pick the right size, model and style so your purchase fully meets your expectations.',
 UTC_TIMESTAMP()),

('about.card2.body',
 '100 ₼-dən yuxarı sifariş verdikdə sifarişinizi Bakı üzrə pulsuz çatdırırıq. Həmçinin mağazamızdan özünüz götürə bilərsiniz.',
 'При заказе на сумму свыше 100 ₼ мы бесплатно доставим ваш заказ по Баку. Также вы можете воспользоваться самовывозом из нашего магазина.',
 'For orders over 100 ₼ we deliver free of charge within Baku. You can also pick up your order from our store.',
 UTC_TIMESTAMP()),

('about.card3.body',
 'Bütün suallarınız üçün həmişə bu nömrə ilə əlaqə saxlaya bilərsiniz: +99410 3151354',
 'Вы всегда на связи по любым вопросам по номеру: +99410 3151354',
 'You can always reach us with any question at: +99410 3151354',
 UTC_TIMESTAMP()),

('about.card4.title',
 'Bütün dünyaya çatdırılma',
 'Доставка по всему миру',
 'Worldwide delivery',
 UTC_TIMESTAMP()),

('about.card4.body',
 'Sifarişləri bütün dünyaya çatdırırıq. Harada olmağınızdan asılı olmayaraq, sifariş verib etibarlı beynəlxalq çatdırılma xidmətləri vasitəsilə orijinal Code994 məhsullarını əldə edə bilərsiniz.',
 'Мы осуществляем доставку заказов по всему миру. Независимо от того, где вы находитесь, вы можете оформить заказ и получить оригинальную продукцию Code994 с помощью надежных международных служб доставки.',
 'We ship orders worldwide. Wherever you are, you can place an order and receive authentic Code994 products through reliable international delivery services.',
 UTC_TIMESTAMP()),

('store.phone',
 '+99410 3151354',
 '+99410 3151354',
 '+99410 3151354',
 UTC_TIMESTAMP()),

('store.announcement',
 '100 ₼-dən yuxarı SİFARİŞLƏR ÜÇÜN BAKI ÜZRƏ PULSUZ ÇATDIRILMA VƏ YA MAĞAZADAN GÖTÜRMƏ',
 'ДЛЯ ЗАКАЗОВ СВЫШЕ 100 ₼ БЕСПЛАТНАЯ ДОСТАВКА ПО БАКУ ИЛИ САМОВЫВОЗ ИЗ МАГАЗИНА',
 'FREE DELIVERY IN BAKU OR IN-STORE PICKUP FOR ORDERS OVER 100 ₼',
 UTC_TIMESTAMP()),

('freeShipping.threshold', '100', '100', '100', UTC_TIMESTAMP())

AS new
ON DUPLICATE KEY UPDATE
  `ValueAz`    = new.`ValueAz`,
  `ValueRu`    = new.`ValueRu`,
  `ValueEn`    = new.`ValueEn`,
  `UpdatedAt`  = UTC_TIMESTAMP();

-- Check the result:
SELECT `Key`, LEFT(`ValueRu`, 60) AS ru FROM `SiteSettings`
WHERE `Key` LIKE 'about.%' OR `Key` IN ('store.phone', 'store.announcement', 'freeShipping.threshold')
ORDER BY `Key`;
