# Статическая old → new ABI проверка

- Самостоятельный metadata-only test project: baseline и fresh SDK kit выбираются явно через AssemblyMetadata. Никакого поиска, пересборки frozen DLL, загрузки AgentBridge или автоматической подготовки поставки.
- Перед чтением frozen consumers сверять pinned evidence и preservation hashes. Новый kit проверять по полному manifest; framework references брать только из установленного .NET runtime, не из DLL test runner.
- Проверять semantic API и все TypeRef/MemberRef/MethodSpec двух frozen consumers через Roslyn metadata symbols. Сигнатуры, generic instantiations и доступность разрешать в новой closure; неизвестную форму metadata отклонять явно.
- Negative controls компилируют только синтетические PE в памяти и не исполняют их. Эта проверка не доказывает runtime, HTTP, SQL либо сохранность старого формата диалогов.
- Сборка конкретного проекта с GeneratePackageOnBuild=false; запуск изолированного test DLL после успешной сборки. Generated output — только artifacts.
