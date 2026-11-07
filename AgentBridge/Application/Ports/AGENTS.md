# Прикладные порты

- Однородная группа независимых контрактов application-слоя; HTTP, EF и особенности конкретного коннектора сюда не входят.
- IModelGateway.AutomaticCompactionEnabled — решение коннектора об автоматическом сжатии, не доказательство server capability. Default true сохраняет прежние реализации; false не отключает полный budget guard и не маскирует отказ явного CompactAsync.
- IModelContextGateway — добавочная capability встроенных коннекторов для локального происхождения. Не изменяет IModelGateway и не обещает upstream identity. Custom gateway без capability сохраняет прежний путь; встроенные проверяют marker opaque input до HTTP.
- Короткие storage ports сохраняют owner/expiry/version/fencing и явный Unsupported для неподдержанных возможностей. Default interface members не должны превращать неподдержанный сценарий в успешную операцию.
