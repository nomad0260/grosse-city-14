game-ticker-restart-round = Перезапуск раунда...
game-ticker-start-round = Раунд начинается...
game-ticker-start-round-cannot-start-game-mode-fallback = Не удалось запустить режим { $failedGameMode }! Запускаем { $fallbackMode }...
game-ticker-start-round-cannot-start-game-mode-restart = Не удалось запустить режим { $failedGameMode }! Перезапуск раунда...
game-ticker-start-round-invalid-map = Выбранная карта { $map } не подходит для игрового режима { $mode }. Игровой режим может не функционировать как задумано...
game-ticker-unknown-role = Неизвестный
game-ticker-delay-start = Начало раунда было отложено на { $seconds } секунд.
game-ticker-pause-start = Начало раунда было приостановлено.
game-ticker-pause-start-resumed = Отсчёт начала раунда возобновлён.
game-ticker-player-join-game-message = Добро пожаловать в Город 14! Если вы играете впервые, обязательно нажмите ESC на клавиатуре и прочитайте правила игры, а также не бойтесь просить помощи в "Админ помощь".
game-ticker-get-info-text = Привет и добро пожаловать в [color=white]Grosse City 14![/color]
                            Текущий раунд: [color=white]#{ $roundId }[/color]
                            Текущее количество игроков: [color=white]{ $playerCount }[/color]
                            Текущая карта: [color=white]{ $mapName }[/color]
                            Текущий режим игры: [color=white]{ $gmTitle }[/color]
                            >[color=yellow]{ $desc }[/color]
game-ticker-get-info-preround-text = Привет и добро пожаловать в [color=white]Grosse City 14![/color]
    Текущий раунд: [color=white]#{ $roundId }[/color]
    Текущее количество игроков: [color=white]{ $playerCount }[/color] ([color=white]{ $readyCount }[/color] { $readyCount ->
    [one] готов
    *[other] готовы
})
    Текущая карта: [color=white]{ $mapName }[/color]
    Текущий режим игры: [color=white]{ $gmTitle }[/color]
    >[color=yellow]{ $desc }[/color]
game-ticker-no-map-selected = [color=red]Карта ещё не выбрана![/color]
game-ticker-player-no-jobs-available-when-joining = При попытке присоединиться к игре ни одной роли не было доступно.

# Displayed in chat to admins when a player joins
player-join-message = Игрок { $name } зашёл!
player-first-join-message = Игрок { $name } зашёл на сервер впервые.

# Displayed in chat to admins when a player leaves
player-leave-message = Игрок { $name } вышел!

latejoin-arrival-announcement = { $character } ({ $job }) { GENDER($entity) ->
    [male] прибыл
    [female] прибыла
    [epicene] прибыли
    *[neuter] прибыло
} в город!
latejoin-arrival-announcement-special = { $job } { $character } на палубе!
latejoin-arrival-sender = Города
latejoin-arrivals-direction = Вскоре прибудет поезд, который доставит вас в город.
latejoin-arrivals-direction-time = Поезд, который доставит вас в город, прибудет через { $time }.
latejoin-arrivals-dumped-from-shuttle = Таинственная сила не позволяет вам уехать на поезде прибытия.
latejoin-arrivals-teleport-to-spawn = Таинственная сила вытягивает вас с поезда прибытия. Удачной смены!

preset-not-enough-ready-players = Не удалось запустить пресет { $presetName }. Требуется { $minimumPlayers } игроков, но готовы только { $readyPlayersCount }.
preset-no-one-ready = Не удалось запустить режим { $presetName }. Нет готовых игроков.

game-run-level-PreRoundLobby = Предраундовое лобби
game-run-level-InRound = В раунде
game-run-level-PostRound = После раунда
