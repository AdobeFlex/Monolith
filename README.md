<p align="center"><img alt="Space Exodus" height="300" src="https://raw.githubusercontent.com/space-exodus/Monolith/0ddfa161945b7dda8c9cea018b7e72066225fae6/Resources/Textures/_Exodus/Logo/logo.png?raw=true" /><img alt="Monolith" height="50" src="https://raw.githubusercontent.com/Monolith-Station/Monolith/89d435f0d2c54c4b0e6c3b1bf4493c9c908a6ac7/Resources/Textures/_Mono/Logo/logo.png?raw=true" /></p>

"Exodus: Monolith" это репозиторий англоязычного фронтира [Monolith](https://github.com/Monolith-Station/Monolith) который работает на движке [Robust Toolbox](https://github.com/space-wizards/RobustToolbox) от Space Wizards написанном на C#.


Это основной репозиторий проекта "SS220 Exodus: Monolith".

Если вы хотите создавать или размещать контент для "SS220 Exodus: Monolith", вам нужен именно этот репозиторий. Он содержит как RobustToolbox, так и набор контента для разработки нового контента.
## Links

[Discord-сервер SS220 Exodus: Monolith](https://discord.com/invite/ss220) | [Discord-сервер Monolith](https://discord.gg/mxY4h2JuUw) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/)


## Участие в разработке

Если Вы желаете помочь в улучшении репозитория, решении проблем или создании нового контента, мы рады принять вклад от любого человека. Заходите в Discord, если хотите помочь. Не бойтесь просить о помощи!

Примечание: чтобы ваш вклад был принят, вы должны согласиться с условиями [нашей лицензии CLA](LICENSES/CLA.txt)

## Сборка

Обратитесь к [руководству Space Wizards](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) для получения общей информации о настройке среды разработки, но имейте в виду, что наш проект — это не то же самое, и многое может не подходить.
Мы предоставляем несколько скриптов, показанных ниже, чтобы упростить работу.

### Зависимости для сборки

> - Git
> - .NET SDK 10.0


### Windows

> 1. Клонируйте этот репозиторий.
> 2. Запустите `Scripts/bat/updateEngine.bat` в терминале или проводнике, чтобы загрузить движок игры.
> 3. Запустите `Scripts/bat/buildAllDebug.bat` после внесения любых изменений в исходный код (Примечание: для полноценной игры стоит запускать `Scripts/bat/buildAllRelease.bat` или `Scripts/bat/buildAllRelease.bat` для маппинга или теста).
> 4. Запустите `Scripts/bat/runQuickAll.bat` чтобы запустить клиент и сервер.
> 5. Подключитесь к localhost в клиенте и играйте.

### Linux

> 1. Клонируйте этот репозиторий.
> 2. Запустите `Scripts/sh/updateEngine.sh` в терминале или проводнике, чтобы загрузить движок игры.
> 3. Запустите `Scripts/sh/buildAllDebug.sh` после внесения любых изменений в исходный код.
> 4. Запустите `Scripts/sh/runQuickAll.sh` чтобы запустить клиент и сервер.
> 5. Подключитесь к localhost в клиенте и играйте.

### MacOS

> 1. Клонируйте этот репозиторий.
> 2. Запустите `Scripts/sh/updateEngine.sh` в терминале или проводнике, чтобы загрузить движок игры.
> 3. Запустите `Scripts/sh/buildAllDebug.sh` после внесения любых изменений в исходный код.
> 4. Запустите `Scripts/sh/runQuickAll.sh` чтобы запустить клиент и сервер.
> 5. Подключитесь к localhost в клиенте и играйте.

## Лицензия

Для получения подробной информации о лицензировании внимательно прочтите файл [LEGAL.md](LEGAL.md)


<!-- Exodus-begin: SS220 chat bans -->
## Муты OOC, LOOC и Ghost (порт SS220)

Перенесена система Chat Ban из [основного билда SS220](https://github.com/SerbiaStrong-220/space-station-14/tree/ddb370a431c2694cc90a71fbd29fa2b4fedf22ec).
Она использует общую панель банов и историю наказаний. Для выдачи и снятия нужно право **Ban** и активный режим администратора.

1. Откройте панель нужного игрока и нажмите **Ban / Забанить** (либо выполните `banpanel "Ник"` в консоли игры).
2. Выберите тип **«Чаты»**, укажите причину и срок, затем отметьте нужные каналы на вкладке **«Чаты»**.
3. Проверьте привязки наказания. Для мута только выбранного аккаунта оставьте игрока и снимите галочки IP и HWID.
4. Нажмите **«Забанить»** и подтвердите повторным нажатием.
5. История доступна через **Notes / Заметки** в панели игрока. Откройте запись мута и нажмите **«Снять мут»** с подтверждением.

Консольные команды:

```text
chatban "Ник" OOC "Флуд" 60
chatban "Ник" OOC,LOOC,Dead "Нарушение правил общения" 1440 Medium
chatban "Ник" Dead "Нарушение правил общения" 0
chatunban 123
```

`Dead` — Ghost-чат, также принимается имя `Ghost`. Вместо ника можно указать UUID отключённого игрока.
Команда, как в SS220, добавляет известный HWID игрока; панель позволяет отдельно выбрать привязки.
Срок задаётся целыми минутами; `0` или отсутствие срока означает бессрочный мут. Максимум — 5256000 минут (10 лет).
Причина обязательна, до 1024 символов. Допустимая тяжесть: `None`, `Minor`, `Medium`, `High`.

Мут переживает смену тела, реконнект, новый раунд и перезапуск сервера. Время вне игры также учитывается.
Истечение срока проверяется при отправке, поэтому новый раунд не требуется. Перекрывающиеся наказания действуют независимо.
При перенаправлении LOOC призрака проверяется ограничение Ghost. AHelp и обычная речь живого персонажа доступны.
При ограничении клиент сохраняет набранный текст. История хранит автора, каналы, причину, срок и сведения о снятии.

Миграции SQLite и PostgreSQL автоматически добавляют таблицу `ban_chat` при старте сервера. Они не меняют таблицы
существующих ролевых банов и не удаляют наказания. Нужны обычные права на миграции БД; новые ключи, переменные окружения
и начальные данные не требуются. При общей БД нескольких серверов немедленное обновление подключённых игроков
выполняется на сервере операции; другие серверы загружают актуальные ограничения при переподключении.

Команды PowerShell из корня проекта (Git и .NET SDK 10.0):

```powershell
git submodule update --init --recursive
dotnet restore SpaceStation14.slnx
dotnet build Content.Server/Content.Server.csproj -c Release --no-restore
dotnet build Content.Client/Content.Client.csproj -c Release --no-restore
dotnet test Content.Tests/Content.Tests.csproj -c Release --filter FullyQualifiedName~ChatBanDatabaseTest
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c Release --filter FullyQualifiedName~Tests._Exodus.Chat.ChatBanTest
```

Для локальной проверки запустите в двух терминалах:

```powershell
dotnet run --project Content.Server/Content.Server.csproj -c Release --no-build
```

```powershell
dotnet run --project Content.Client/Content.Client.csproj -c Release --no-build
```

В клиенте подключитесь к `localhost`. Для разработки можно заменить `Release` на `Debug` в командах сборки и запуска.
Если тип «Чаты» отсутствует, пересоберите и клиент, и сервер. Если операция завершается ошибкой, сначала проверьте
историю, чтобы не выдать повторный мут, затем журнал сервера. При ошибках миграции проверьте права на БД;
удалять БД для исправления ошибки не нужно. Команды администратора доступны только после получения соответствующих прав.

Логика находится в `Content.Server/Administration/Managers/BanManager.ChatBans.Exodus.cs`; команды — в
`Content.Server/_Exodus/Chat`; модель каналов — в `Content.Server.Database/_Exodus/Chat`; сетевые сообщения —
в `Content.Shared/_Exodus/Chat`. Дополнения к штатным окнам вынесены в `*.ChatBans.Exodus.cs` рядом с ними.
<!-- Exodus-end -->
