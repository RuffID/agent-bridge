# Runtime delivery probe

- Выделенный framework-dependent net10.0 test executable; binary references только из одного verified kit. Без ProjectReference/PackageReference, hosting, реальных HTTP/БД/SQL, maintenance или бизнес-операций.
- Копировать source/project и kit вне repo. Перед каждым запуском сверять kit manifest и copied output size/SHA; несовпадение, missing dependency и RID отклонять fail-fast, без fallback. Запуск только после fresh successful Build того же output.
- Actual app graph этого probe: kit closure плюс framework Microsoft.NETCore.App; app ILogger в памяти, IConfiguration in-memory, HTTP blocking handler. Это не graph production ASP.NET Core приложения.
- Probe проверяет все managed entries manifest через load/GetTypes, все native entries через отдельный Load/Free, actual facade/strict startup options, distinct scopes, app logger и оба offline BPE embedded словаря. Перед загрузкой проверяются copied output hashes. DbContext конструируется, но операции и соединения не вызываются. Load/Free не доказывает provider/server/TLS/authentication или native операции библиотеки.
- Linux cross-build не runtime. Каждая ОС/архитектура запускается отдельно после определения ресурсов. Release, AOT/trimming/single-file и IDE acceptance не подразумеваются.
