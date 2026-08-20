-- Code994 — site content sync, 2026-08-12.
--
-- Client's final call on the two open items:
--   • free-delivery threshold back to 150 ₼ (Baku-only wording stays)
--   • support card: "Вы всегда на связи" → "Мы всегда на связи"
--
-- Run ONCE, by hand. Do NOT put this in deploy.sh — it would revert every
-- admin-panel edit on the next deploy.
--
--   mysqldump -u root -p Shopping SiteSettings Sliders > /root/backup-2026-08-12.sql
--   mysql -u root -p Shopping < /opt/994Back/scripts/content-2026-08-12.sql

SET NAMES utf8mb4;

INSERT INTO `SiteSettings` (`Key`, `ValueAz`, `ValueRu`, `ValueEn`, `CreatedAt`) VALUES
('about.p2',
 'Mağazamız Bakının ən mərkəzində, Zərifə Əliyeva küçəsi 12 ünvanında yerləşir. 150 ₼-dən yuxarı sifarişlərdə Bakı üzrə pulsuz çatdırılmadan istifadə edə və ya alışınızı özünüz mağazadan götürə bilərsiniz.',
 'Наш магазин расположен в самом центре Баку по адресу: ул. Зарифы Алиевой, 12. При заказе на сумму свыше 150 ₼ вы можете воспользоваться бесплатной доставкой по Баку или забрать покупку самостоятельно из магазина.',
 'Our store is located in the very centre of Baku at 12 Zarifa Aliyeva st. For orders over 150 ₼ you can use free delivery within Baku or collect your purchase from the store yourself.',
 UTC_TIMESTAMP()),

('about.card2.body',
 '150 ₼-dən yuxarı sifariş verdikdə sifarişinizi Bakı üzrə pulsuz çatdırırıq. Həmçinin mağazamızdan özünüz götürə bilərsiniz.',
 'При заказе на сумму свыше 150 ₼ мы бесплатно доставим ваш заказ по Баку. Также вы можете воспользоваться самовывозом из нашего магазина.',
 'For orders over 150 ₼ we deliver free of charge within Baku. You can also pick up your order from our store.',
 UTC_TIMESTAMP()),

('about.card3.body',
 'Bütün suallarınız üçün biz həmişə əlaqədəyik: +99410 3151354',
 'Мы всегда на связи по любым вопросам по номеру: +99410 3151354',
 'We are always in touch for any question: +99410 3151354',
 UTC_TIMESTAMP()),

('store.announcement',
 '150 ₼-dən yuxarı SİFARİŞLƏR ÜÇÜN BAKI ÜZRƏ PULSUZ ÇATDIRILMA VƏ YA MAĞAZADAN GÖTÜRMƏ',
 'ДЛЯ ЗАКАЗОВ СВЫШЕ 150 ₼ БЕСПЛАТНАЯ ДОСТАВКА ПО БАКУ ИЛИ САМОВЫВОЗ ИЗ МАГАЗИНА',
 'FREE DELIVERY IN BAKU OR IN-STORE PICKUP FOR ORDERS OVER 150 ₼',
 UTC_TIMESTAMP()),

('freeShipping.threshold', '150', '150', '150', UTC_TIMESTAMP())

AS new
ON DUPLICATE KEY UPDATE
  `ValueAz`   = new.`ValueAz`,
  `ValueRu`   = new.`ValueRu`,
  `ValueEn`   = new.`ValueEn`,
  `UpdatedAt` = UTC_TIMESTAMP();

-- Hero slider carries the threshold too, and lives in its own table.
UPDATE `Sliders` SET
  `SubtitleAz` = '150 ₼-dən yuxarı sifarişlər üçün Bakı üzrə pulsuz çatdırılma.',
  `SubtitleRu` = 'Бесплатная доставка по Баку для заказов от 150 ₼.',
  `SubtitleEn` = 'Free delivery in Baku for orders over 150 ₼.',
  `UpdatedAt`  = UTC_TIMESTAMP()
WHERE `Id` = 1;

-- Check the result:
SELECT `Key`, LEFT(`ValueRu`, 70) AS ru FROM `SiteSettings`
WHERE `Key` IN ('about.p2', 'about.card2.body', 'about.card3.body',
                'store.announcement', 'freeShipping.threshold')
ORDER BY `Key`;

SELECT `Id`, `SubtitleRu` FROM `Sliders` WHERE `Id` = 1;
