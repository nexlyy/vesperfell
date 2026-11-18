using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>
    /// The Russian translation. Keys are the English strings exactly as they are written in the code;
    /// patterns keep their {0}, {1} placeholders and their ^ colour codes.
    /// </summary>
    public static partial class LocRu
    {
        static Dictionary<string, string> d;
        static void A(string en, string ru) { d[en] = ru; }

        public static Dictionary<string, string> Build()
        {
            d = new Dictionary<string, string>(1400);
            Menus(); Hud(); Windows(); World(); Net(); ItemText();
            Classes(); Bestiary(); Places(); QuestText(); Signs(); Talk();
            return d;
        }

        // generated item names
        public static readonly Dictionary<string, string> Suffixes = new Dictionary<string, string>
        {
            { "of the Wolf", "Волка" }, { "of the Fox", "Лисы" }, { "of the Owl", "Совы" }, { "of the Bear", "Медведя" }, { "of the Vale", "Долины" },
            { "of Vespers", "Вечерни" }, { "of Embers", "Углей" }, { "of the Hart", "Оленя" }, { "of Thirst", "Жажды" },
        };

        public static readonly Dictionary<string, Loc.Adj> Prefixes = new Dictionary<string, Loc.Adj>
        {
            { "Worn", new Loc.Adj("Потёртый", "Потёртая", "Потёртое", "Потёртые") },
            { "Iron", new Loc.Adj("Грубый", "Грубая", "Грубое", "Грубые") },
            { "Plain", new Loc.Adj("Простой", "Простая", "Простое", "Простые") },
            { "Old", new Loc.Adj("Старый", "Старая", "Старое", "Старые") },
            { "Sturdy", new Loc.Adj("Крепкий", "Крепкая", "Крепкое", "Крепкие") },
            { "Keen", new Loc.Adj("Острый", "Острая", "Острое", "Острые") },
            { "Tempered", new Loc.Adj("Закалённый", "Закалённая", "Закалённое", "Закалённые") },
            { "Trusty", new Loc.Adj("Верный", "Верная", "Верное", "Верные") },
            { "Honed", new Loc.Adj("Отточенный", "Отточенная", "Отточенное", "Отточенные") },
            { "Wayfarer's", new Loc.Adj("Дорожный", "Дорожная", "Дорожное", "Дорожные") },
            { "Moonforged", new Loc.Adj("Лунный", "Лунная", "Лунное", "Лунные") },
            { "Gloamtouched", new Loc.Adj("Мглистый", "Мглистая", "Мглистое", "Мглистые") },
            { "Vigilant", new Loc.Adj("Недремлющий", "Недремлющая", "Недремлющее", "Недремлющие") },
            { "Warden's", new Loc.Adj("Дозорный", "Дозорная", "Дозорное", "Дозорные") },
            { "Bellwrought", new Loc.Adj("Колокольный", "Колокольная", "Колокольное", "Колокольные") },
            { "Dusk-bound", new Loc.Adj("Сумеречный", "Сумеречная", "Сумеречное", "Сумеречные") },
            { "Saint-blessed", new Loc.Adj("Освящённый", "Освящённая", "Освящённое", "Освящённые") },
            { "Kingsworn", new Loc.Adj("Королевский", "Королевская", "Королевское", "Королевские") },
            { "Emberheart", new Loc.Adj("Пламенный", "Пламенная", "Пламенное", "Пламенные") },
            { "Evensong", new Loc.Adj("Вечерний", "Вечерняя", "Вечернее", "Вечерние") },
        };

        // gender: 0 masculine, 1 feminine, 2 neuter, 3 plural
        public static readonly Dictionary<string, Loc.Noun> Nouns = new Dictionary<string, Loc.Noun>
        {
            { "Longsword", new Loc.Noun("Длинный меч", 0) }, { "Arming Sword", new Loc.Noun("Рыцарский меч", 0) }, { "Warblade", new Loc.Noun("Боевой клинок", 0) },
            { "Greataxe", new Loc.Noun("Секира", 1) }, { "Headsman's Axe", new Loc.Noun("Топор палача", 0) }, { "Cleaver", new Loc.Noun("Тесак", 0) },
            { "Staff", new Loc.Noun("Посох", 0) }, { "Ember Staff", new Loc.Noun("Тлеющий посох", 0) }, { "Brandstaff", new Loc.Noun("Огненный посох", 0) },
            { "Wand", new Loc.Noun("Жезл", 0) }, { "Cinder Wand", new Loc.Noun("Пепельный жезл", 0) }, { "Kindling Rod", new Loc.Noun("Запальный прут", 0) },
            { "Daggers", new Loc.Noun("Кинжалы", 3) }, { "Twin Fangs", new Loc.Noun("Парные клыки", 3) }, { "Stilettos", new Loc.Noun("Стилеты", 3) },
            { "Kris", new Loc.Noun("Крис", 0) }, { "Serpent Blade", new Loc.Noun("Змеиный клинок", 0) }, { "Flamberge Knife", new Loc.Noun("Пламенеющий нож", 0) },
            { "Longbow", new Loc.Noun("Длинный лук", 0) }, { "Warbow", new Loc.Noun("Боевой лук", 0) }, { "Yew Bow", new Loc.Noun("Тисовый лук", 0) },
            { "Shortbow", new Loc.Noun("Короткий лук", 0) }, { "Hunting Bow", new Loc.Noun("Охотничий лук", 0) }, { "Horn Bow", new Loc.Noun("Роговой лук", 0) },
            { "Hauberk", new Loc.Noun("Кольчуга", 1) }, { "Cuirass", new Loc.Noun("Кираса", 1) }, { "Warplate", new Loc.Noun("Латы", 3) },
            { "Vestment", new Loc.Noun("Облачение", 2) }, { "Robe", new Loc.Noun("Мантия", 1) }, { "Mantle", new Loc.Noun("Накидка", 1) },
            { "Leathers", new Loc.Noun("Кожаный доспех", 0) }, { "Shadowgarb", new Loc.Noun("Одеяние тени", 2) }, { "Harness", new Loc.Noun("Перевязь", 1) },
            { "Jerkin", new Loc.Noun("Куртка", 1) }, { "Huntcoat", new Loc.Noun("Охотничий кафтан", 0) }, { "Wildhide", new Loc.Noun("Звериная шкура", 1) },
            { "Ring", new Loc.Noun("Кольцо", 2) }, { "Signet", new Loc.Noun("Перстень", 0) }, { "Amulet", new Loc.Noun("Амулет", 0) }, { "Charm", new Loc.Noun("Оберег", 0) },
        };

        // menus
        static void Menus()
        {
            // loading
            A("Sharpening steel", "Точим сталь");
            A("Charting the vale", "Чертим карту долины");
            A("Descending into the undercroft", "Спускаемся в подземелье");
            A("Tuning the bell", "Настраиваем колокол");
            A("The dusk gathers", "Сгущаются сумерки");
            A("The abbey waits", "Аббатство ждёт");
            A("Ready", "Готово");
            // title
            A("T H E   S I L E N T   B E L L", "Б Е З М О Л В Н Ы Й   К О Л О К О Л");
            A("Continue", "Продолжить");
            A("New Game", "Новая игра");
            A("Join Game", "Присоединиться");
            A("Settings", "Настройки");
            A("Quit", "Выход");
            A("A dark-fantasy action RPG demo", "Демо экшен-RPG в жанре тёмного фэнтези");
            A("WASD move  -  mouse aim  -  F interact  -  F11 fullscreen", "WASD — движение  -  мышь — прицел  -  F — действие  -  F11 — весь экран");
            A("A new journey replaces the saved one at its first wayshrine.", "Новый путь заменит сохранённый у первого же святилища.");
            // joining
            A("Join a Friend's Game", "Игра с другом");
            A("Your name", "Ваше имя");
            A("Host's address", "Адрес хозяина");
            A("The host opens their game from the pause menu (Esc, then Host Co-op) and tells you the address shown there.",
              "Хозяин открывает игру в меню паузы (Esc, затем «Создать кооператив») и сообщает вам показанный там адрес.");
            A("Knocking at the door...", "Стучимся в дверь...");
            A("Back", "Назад");
            A("Connect", "Подключиться");
            // class selection
            A("Choose Your Calling", "Выберите призвание");
            A("Joining {0}'s vigil  -  level {1}  -  {2} in the party", "Вход в игру {0}  —  уровень {1}  —  в отряде: {2}");
            A("Abilities", "Умения");
            A("Lv {0}", "Ур. {0}");
            A("^dLearned at level {0}.^0", "^dИзучается на уровне {0}.^0");
            A("Begins with", "Снаряжение");
            A("Resource", "Ресурс");
            A("Dodges", "Перекаты");
            A("Health", "Здоровье");
            A("Power", "Мощь");
            A("Defence", "Защита");
            A("Speed", "Скорость");
            A("Range", "Дальность");
            A("Begin the Vigil", "Начать бдение");
            A("Join the Vigil", "Вступить в бдение");
            A("Entering...", "Входим...");
            // pause
            A("Paused", "Пауза");
            A("The Party", "Отряд");
            A("Resume", "Продолжить");
            A("Save Game", "Сохранить игру");
            A("^gProgress saved.^0 You will return at your last wayshrine.", "^gИгра сохранена.^0 Вы вернётесь к последнему святилищу.");
            A("Leave the Party", "Покинуть отряд");
            A("Host Co-op", "Создать кооператив");
            A("Stop Hosting", "Закрыть кооператив");
            A("^yThe party is disbanded.^0 The vigil is yours alone again.", "^yОтряд распущен.^0 Бдение снова только ваше.");
            A("Save and Exit to Title", "Сохранить и выйти");
            A("Quit to Desktop", "Выйти из игры");
            A("In the party: {0} of {1}. Your address:", "В отряде: {0} из {1}. Ваш адрес:");
            A("How friends join", "Как зайти друзьям");
            A("{0}  -  level {1}  -  {2}h {3}m", "{0}  -  уровень {1}  -  {2} ч {3} мин");
            A("The Vigil is Open", "Бдение открыто");
            A("Friends join from the title screen: ^yJoin Game^0, then one of these addresses.\n\n{0}\n\nOn the same home network, use the address that begins with 192.168 or 10. Over the internet, either forward TCP port {1} on your router, or set up a virtual network with your friends (Radmin VPN, ZeroTier, Tailscale) and use its address (Radmin VPN addresses begin with 26).\n\nIf Windows Firewall asks, allow Vesperfell.",
              "Друзья подключаются с главного экрана: ^yПрисоединиться^0, затем один из этих адресов.\n\n{0}\n\nВ одной домашней сети подойдёт адрес, начинающийся с 192.168 или 10. Через интернет: либо пробросьте TCP-порт {1} на роутере, либо создайте с друзьями виртуальную сеть (Radmin VPN, ZeroTier, Tailscale) и возьмите её адрес (адреса Radmin VPN начинаются с 26).\n\nЕсли брандмауэр Windows спросит — разрешите Vesperfell.");
            // settings
            A("Sound", "Звук");
            A("Master", "Общая");
            A("Music", "Музыка");
            A("Effects", "Эффекты");
            A("Ambience", "Окружение");
            A("Display", "Экран");
            A("Fullscreen", "Полный экран");
            A("Window size", "Размер окна");
            A("Game", "Игра");
            A("Screen shake", "Тряска экрана");
            A("Damage numbers", "Числа урона");
            A("Show frame rate", "Счётчик кадров");
            A("Language", "Язык");
            A("Co-op name", "Имя в сети");
            A("Controls", "Управление");
            A("Done", "Готово");
            A("Move", "Движение");
            A("Mouse", "Мышь");
            A("Aim", "Прицел");
            A("Left mouse", "ЛКМ");
            A("Basic attack (hold)", "Атака (удерживать)");
            A("Right mouse", "ПКМ");
            A("Class secondary", "Приём класса");
            A("Space", "Пробел");
            A("Dodge roll", "Перекат");
            A("Interact / talk", "Действие / разговор");
            A("Health / Spirit draught", "Настои: жизнь / дух");
            A("I  or  Tab", "I  или  Tab");
            A("Inventory", "Инвентарь");
            A("Character", "Персонаж");
            A("Skills", "Умения");
            A("Journal", "Журнал");
            A("Map", "Карта");
            A("Pause / close", "Пауза / закрыть");
            A("Toggle fullscreen", "Полный экран");
            A("Party chat (co-op)", "Чат отряда (кооп)");
            // reading, death, ending
            A("Close  [F]", "Закрыть  [F]");
            A("YOU HAVE FALLEN", "ВЫ ПАЛИ");
            A("The Gloam closes over you. Somewhere, a wayshrine remembers your name.", "Мгла смыкается над вами. Где-то святилище ещё помнит ваше имя.");
            A("Rise at the last wayshrine", "Восстать у святилища");
            A("Rise beside your companions", "Восстать рядом с отрядом");
            A("Exit to Title", "Выйти в меню");
            A("The Gloam will keep {0} of your gold.", "Мгла оставит себе {0} вашего золота.");
            A("The Bell Rings", "Колокол звонит");
            A("It rings out over the abbey roofs and the standing stones, over the Thornwood and the drowned fen, and wherever the sound goes the dusk draws back like a tide.\n\nFor the first time in thirty nights, there is an east.\n\n^yElder Maren will want to hear it from you.^0",
              "Звон плывёт над крышами аббатства и стоячими камнями, над Терновым лесом и затопленной топью — и всюду, куда он доходит, сумерки отступают, как отлив.\n\nВпервые за тридцать ночей у мира снова есть восток.\n\n^yСтарейшина Марен захочет услышать это от вас.^0");
            A("Level {0}   -   {1} foes slain   -   fallen {2} times   -   {3}h {4}m", "Уровень {0}   -   врагов убито: {1}   -   смертей: {2}   -   {3} ч {4} мин");
            A("Step into the dawn", "Шагнуть в рассвет");
        }

        // in-game display
        static void Hud()
        {
            A("LMB", "ЛКМ");
            A("RMB", "ПКМ");
            A("Experience", "Опыт");
            A("Maximum level reached.", "Достигнут максимальный уровень.");
            A("{0} / {1} to level {2}", "{0} / {1} до уровня {2}");
            A("Rest at a wayshrine or drink a draught (^y1^0) to recover.", "Отдохните у святилища или выпейте настой (^y1^0), чтобы восстановиться.");
            A("Dodge Roll  [Space]", "Перекат  [Пробел]");
            A("Tumble in the direction you are moving. You cannot be hit for a moment.", "Кувырок в сторону движения. На мгновение вас нельзя задеть.");
            A("L{0}", "У{0}");
            A("{0}s cooldown", "перезарядка {0} с");
            A("^rUnlocks at level {0}^0", "^rОткроется на уровне {0}^0");
            A("Health Draught  [1]", "Целебный настой  [1]");
            A("Spirit Draught  [2]", "Настой духа  [2]");
            A("Restores 45% of your health over a moment.", "Постепенно восстанавливает 45% здоровья.");
            A("Restores 50 Fury.", "Восстанавливает 50 Ярости.");
            A("Restores 60% of your {0}.", "Восстанавливает 60% запаса: {0}.");
            A("Say:", "Чат:");
            A("Press ^yM^0 for the full map.", "Нажмите ^yM^0, чтобы открыть карту.");
            A("THE SILENT PRIOR", "БЕЗМОЛВНЫЙ ПРИОР");
            A("keeper of the unrung bell", "хранитель молчащего колокола");
            A("LEVEL {0}", "УРОВЕНЬ {0}");
            A("Talk", "Говорить");
            A("New Quest", "Новое задание");
            A("Quest Complete", "Задание выполнено");
            // floating words
            A("Ambush!", "Засада!");
            A("Backstab", "Удар в спину");
            A("Perfect block!", "Идеальный блок!");
            A("Staggered!", "Оглушён!");
            A("+{0} xp", "+{0} оп.");
            A("Unlocks at level {0}", "Откроется на уровне {0}");
            A("Not enough {0}", "Не хватает: {0}");
            A("No room", "Нет места");
            A("No target", "Нет цели");
            A("No draughts", "Настоев нет");
            A("Already hale", "Здоровье полное");
            A("Already full", "Запас полон");
        }

        // the character window
        static void Windows()
        {
            A("Equipment", "Снаряжение");
            A("Weapon", "Оружие");
            A("Armour", "Броня");
            A("Amulet", "Амулет");
            A("Ring", "Кольцо");
            A("Click to unequip", "Щелчок: снять");
            A("{0} slot", "Слот: {0}");
            A("Empty.", "Пусто.");
            A("Damage", "Урон");
            A("Gold", "Золото");
            A("Health Draught", "Целебный настой");
            A("Spirit Draught", "Настой духа");
            A("Health Draught  ({0})", "Целебный настой  ({0})");
            A("Restores 45% of your health. Press ^y1^0 in the field, or click here.", "Восстанавливает 45% здоровья. В бою — клавиша ^y1^0, здесь — щелчок.");
            A("Spirit Draught  ({0})", "Настой духа  ({0})");
            A("Restores your {0}. Press ^y2^0 in the field, or click here.", "Восполняет запас: {0}. В бою — клавиша ^y2^0, здесь — щелчок.");
            A("Pack  {0} / {1}", "Сумка  {0} / {1}");
            A("Left-click: equip    Right-click: drop", "ЛКМ: надеть    ПКМ: выбросить");
            A("Right-click: drop", "ПКМ: выбросить");
            A("^rYour class cannot use this.^0", "^rВаш класс не может это использовать.^0");
            A("^rYou cannot use that.^0", "^rВы не можете это использовать.^0");
            A("^rYour pack is full.^0", "^rСумка полна.^0");
            A("Keepsakes", "Памятные вещи");
            A("Nothing yet.", "Пока ничего.");
            A("Left-click to equip or unequip.  Right-click to drop.", "ЛКМ — надеть или снять.  ПКМ — выбросить.");
            A("A green mark means stronger than what you wear.", "Зелёная метка — вещь сильнее надетой.");
            A("Level {0}", "Уровень {0}");
            A("Maximum level", "Максимальный уровень");
            A("{0} / {1} experience", "Опыт: {0} / {1}");
            A("Attributes", "Характеристики");
            A("Might", "Сила");
            A("Finesse", "Ловкость");
            A("Arcana", "Чары");
            A("Vigor", "Стойкость");
            A("Raw strength. +2% damage per point for Warriors, a little for everyone else, and half a point of Armour.",
              "Грубая сила. +2% урона за очко для Воинов, немного — для остальных, и пол-очка Брони.");
            A("Speed and precision. +2% damage per point for Rogues and Rangers. +0.5% critical chance and slightly faster attacks for all.",
              "Быстрота и точность. +2% урона за очко для Плутов и Следопытов. +0,5% шанса крита и чуть более быстрые атаки для всех.");
            A("Command of the unseen. +2% damage per point for Mages, more Mana, and shorter ability cooldowns for all.",
              "Власть над незримым. +2% урона за очко для Магов, больше Маны и более короткая перезарядка умений для всех.");
            A("Hardiness. +8 Health per point and faster natural recovery.", "Выносливость. +8 здоровья за очко и более быстрое естественное восстановление.");
            A("primary", "основная");
            A("Points to spend: {0}", "Свободных очков: {0}");
            A("Each level grants 3 points to spend here.", "Каждый уровень даёт 3 очка характеристик.");
            A("Combat", "Бой");
            A("When it reaches zero you fall, and wake at the last wayshrine you rested at.", "Когда оно иссякнет, вы падёте и очнётесь у последнего святилища, где отдыхали.");
            A("Damage per hit", "Урон за удар");
            A("Weapon damage multiplied by your attributes (x{0}). Abilities scale from this.", "Урон оружия, умноженный на характеристики (x{0}). От него считаются умения.");
            A("Attacks per second", "Атак в секунду");
            A("Attack speed", "Скорость атаки");
            A("Set by your weapon type and quickened by Finesse.", "Зависит от типа оружия и растёт с Ловкостью.");
            A("Damage per second", "Урон в секунду");
            A("Sustained damage", "Постоянный урон");
            A("Basic attacks only, including critical hits.", "Только основные атаки, с учётом критов.");
            A("Critical chance", "Шанс крита");
            A("Chance for any hit to deal x{0} damage.", "Шанс, что удар нанесёт x{0} урона.");
            A("Reduces every blow you take by the percentage shown.", "Снижает каждый полученный удар на указанный процент.");
            A("Movement", "Скорость");
            A("Movement speed", "Скорость движения");
            A("Paces per second, more or less.", "Шагов в секунду — более-менее.");
            A("Life on hit", "Вампиризм");
            A("Part of the damage you deal returns to you as health.", "Часть нанесённого урона возвращается вам здоровьем.");
            A("Cooldown rate", "Перезарядка");
            A("Cooldowns", "Перезарядка умений");
            A("Arcana shortens the wait between abilities.", "Чары сокращают ожидание между умениями.");
            A("Chronicle", "Хроника");
            A("Foes slain", "Врагов убито");
            A("Times fallen", "Смертей");
            A("Time in the vale", "Время в долине");
            A("{0}h {1}m", "{0} ч {1} мин");
            A("Dodge Roll", "Перекат");
            A("[Space]", "[Пробел]");
            A("{0} charges", "зарядов: {0}");
            A("{0} charge", "зарядов: {0}");
            A("{0}s", "{0} с");
            A("Passive - {0}", "Пассивно — {0}");
            A("Quests", "Задания");
            A("No quests yet.", "Заданий пока нет.");
            A("Main quest", "Главное задание");
            A("Side quest", "Побочное задание");
            A("Completed", "Выполнено");
            A("Objectives", "Цели");
            A("^dReward:^0 {0}", "^dНаграда:^0 {0}");
            A("Track this quest", "Отслеживать");
            A("Tracked on your compass.", "Отмечено на вашем компасе.");
            A("The Vale of Vesperfell", "Долина Весперфелл");
            A("You", "Вы");
            A("Wayshrine", "Святилище");
            A("Folk", "Люди");
            A("Objective", "Цель");
        }

        // the world: prompts, notices, things to read
        static void World()
        {
            A("^yF^0 to speak  -  ^yI^0 inventory  -  ^yJ^0 journal", "^yF^0 — говорить  -  ^yI^0 — инвентарь  -  ^yJ^0 — журнал");
            A("^rCould not save: {0}^0", "^rНе удалось сохранить: {0}^0");
            A("^yWelcome back, Warden.^0", "^yС возвращением, Страж.^0");
            A("The Gloam took its tithe: ^y{0} gold^0", "Мгла взяла свою десятину: ^y{0} зол.^0");
            A("Health Draught  ^d({0})^0", "Целебный настой  ^d({0})^0");
            A("Spirit Draught  ^d({0})^0", "Настой духа  ^d({0})^0");
            A("^yLevel {0}!^0  3 attribute points to spend (^yC^0)", "^yУровень {0}!^0  3 очка характеристик (^yC^0)");
            A("^cNew ability:^0 {0}  ^d[{1}]^0", "^cНовое умение:^0 {0}  ^d[{1}]^0");
            A("^pThe Prior calls the faithful to rise.^0", "^pПриор велит верным восстать.^0");
            A("^pThe silence deepens. He is desperate now.^0", "^pТишина сгущается. Он в отчаянии.^0");
            A("Wolves hunted: ^y{0}^0 / {1}", "Волков убито: ^y{0}^0 / {1}");
            A("Received ^{0}{1}^0", "Получено: ^{0}{1}^0");
            A("^rPack full^0 - {0} lies at your feet", "^rСумка полна^0 — {0} лежит у ваших ног");
            // prompts
            A("Open", "Открыть");
            A("Rest", "Отдохнуть");
            A("Read", "Читать");
            A("Take the lantern", "Взять фонарь");
            A("Gather", "Собрать");
            A("Touch the stone", "Коснуться камня");
            A("Examine the door", "Осмотреть дверь");
            A("Enter the Sunken Abbey", "Войти в Затонувшее аббатство");
            A("Climb to the surface", "Подняться на поверхность");
            A("Kindle the vigil-flame", "Зажечь стражевой огонь");
            A("The stone hums", "Камень гудит");
            A("Open the king's door", "Открыть королевскую дверь");
            A("Examine the bell", "Осмотреть колокол");
            A("Hang the Bell's Tongue", "Повесить язык колокола");
            A("Make a wish", "Загадать желание");
            // things to look at
            A("Sign", "Табличка");
            A("The well", "Колодец");
            A("You let a coin fall. It is a long time before the splash - and when it comes, it sounds almost like a small bell.\n\n^gYou feel a little braver.^0",
              "Вы роняете монету. Всплеска долго нет — а когда он раздаётся, то звучит почти как маленький колокол.\n\n^gВам становится чуть смелее.^0");
            A("Dark water, a long way down.", "Тёмная вода, глубоко внизу.");
            A("A waking stone", "Пробуждённый камень");
            A("The carved sign glows steadily. It is warm, like something breathing in its sleep.", "Высеченный знак ровно светится. Он тёплый — будто кто-то дышит во сне.");
            A("The king's door", "Королевская дверь");
            A("A slab of grey stone seals the mound. Three hollow signs are cut into the lintel - a ring, a crossing, and an arrow.\n\n^dSigns faintly lit: {0} of 3.^0",
              "Плита серого камня запечатывает курган. На притолоке вырезаны три пустых знака — кольцо, крест и стрела.\n\n^dЗнаков слабо светится: {0} из 3.^0");
            A("The abbey doors", "Врата аббатства");
            A("Grey stone has grown across the doorway like frost on a window. At its centre is a round hollow, the size of a man's palm, carved with the sign of the bell.\n\n^dSomething belongs here.^0",
              "Серый камень затянул проём, как иней — окно. В середине — круглая выемка величиной с ладонь, со знаком колокола.\n\n^dСюда что-то вкладывали.^0");
            A("^dOnly the host can lead the party through.^0", "^dПровести отряд дальше может только хозяин игры.^0");
            A("The Vesper Bell", "Вечерний колокол");
            A("It lies where it fell, taller than a man and green with age. A crack runs down its flank.\n\nInside, where the clapper should hang, there is only an iron hook.",
              "Он лежит там, где упал, — выше человеческого роста, зелёный от времени. По боку идёт трещина.\n\nВнутри, где должен висеть язык, — лишь железный крюк.");
            // notices
            A("^yWayshrine attuned:^0 {0}", "^yСвятилище пробуждено:^0 {0}");
            A("^yYou rest.^0 The Gloam stirs - foes return.", "^yВы отдыхаете.^0 Мгла шевелится — враги возвращаются.");
            A("^bMoonpetal^0  ^d({0})^0", "^bЛунный лепесток^0  ^d({0})^0");
            A("^cFar to the north-east, stone grinds against stone.^0", "^cДалеко на северо-востоке камень скрежещет о камень.^0");
            A("^cA rune wakes.^0  {0} of 3", "^cРуна пробудилась.^0  {0} из 3");
            A("^oA vigil-flame burns.^0  One more remains dark.", "^oСтражевой огонь горит.^0  Ещё один остаётся тёмным.");
            A("^pThe Abbey Sigil sinks into the stone. The doors breathe out.^0", "^pПечать аббатства уходит в камень. Врата выдыхают.^0");
            A("^oBoth vigil-flames burn. Somewhere in the dark, the Bellgate grinds open.^0", "^oОба стражевых огня горят. Где-то во тьме со скрежетом открываются Колокольные врата.^0");
            A("^cThe thornbrake gives way. Something glimmers beyond.^0", "^cТерновник поддаётся. За ним что-то мерцает.^0");
            A("^cThe wall crumbles - a sealed passage!^0", "^cСтена осыпается — замурованный проход!^0");
            A("^yJournal updated:^0 {0}", "^yЖурнал обновлён:^0 {0}");
            A("Received ^r2 Health Draughts^0", "Получено: ^r2 целебных настоя^0");
            A("Received ^y25 gold^0", "Получено: ^y25 золота^0");
            A("Received ^y150 gold^0", "Получено: ^y150 золота^0");
            A("Received ^y40 gold^0", "Получено: ^y40 золота^0");
            A("Received ^r3 Health^0 and ^b2 Spirit Draughts^0", "Получено: ^r3 целебных настоя^0 и ^b2 настоя духа^0");
            A("^pThe Silent Prior is unmade.^0", "^pБезмолвный приор развеян.^0");
            A("^cSir Caldus kneels, and is dust.^0", "^cСэр Калдус преклоняет колено — и рассыпается в прах.^0");
            A("^yWASD^0 to move  -  aim with the ^ymouse^0", "^yWASD^0 — движение  -  прицел — ^yмышью^0");
            A("^yLeft mouse^0: {0}   ^yRight mouse^0: {1}", "^yЛКМ^0: {0}   ^yПКМ^0: {1}");
            A("^yQ^0: {0}   ^ySpace^0: dodge roll   ^y1^0: health draught", "^yQ^0: {0}   ^yПробел^0: перекат   ^y1^0: целебный настой");
            A("Brother Halvard waits by the fire. Press ^yF^0 beside him.", "Брат Халвард ждёт у костра. Нажмите ^yF^0 рядом с ним.");
        }

        // co-op
        static void Net()
        {
            A("^rCould not open port {0}: {1}^0", "^rНе удалось открыть порт {0}: {1}^0");
            A("The host has closed the game.", "Хозяин закрыл игру.");
            A("That copy of the game is a different version from the host's.", "Версия вашей игры отличается от версии хозяина.");
            A("The party is full.", "В отряде нет мест.");
            A("^y{0}^0 has left the party.", "^y{0}^0 покидает отряд.");
            A("^g{0}^0 has joined the party.", "^g{0}^0 вступает в отряд.");
            A("Type the host's address first - for example 192.168.1.20", "Сначала введите адрес хозяина — например, 192.168.1.20");
            A("That address could not be found.", "Такой адрес не найден.");
            A("No answer from that address.", "Адрес не отвечает.");
            A("Nobody is hosting at that address.", "По этому адресу никто не ждёт гостей.");
            A("That address could not be reached.", "До этого адреса не удалось добраться.");
            A("The host did not answer.", "Хозяин не ответил.");
            A("The game lost step with the host.", "Игра рассинхронизировалась с хозяином.");
            A("The connection to the host was lost.", "Связь с хозяином потеряна.");
            A("^gYou have joined {0}'s vigil.^0", "^gВы в игре у {0}.^0");
            A("^yEnter^0 to speak to the party  -  ^yF^0 to interact  -  ^yI^0 inventory", "^yEnter^0 — чат отряда  -  ^yF^0 — действие  -  ^yI^0 — инвентарь");
            A("^rThis copy of the game builds the world differently from the host's. Things may be out of place.^0",
              "^rЭта копия игры строит мир иначе, чем у хозяина. Что-то может оказаться не на месте.^0");
            A("^pThe crypt draws the whole party in.^0", "^pКрипта втягивает внутрь весь отряд.^0");
        }

        // item tooltips and named items
        static void ItemText()
        {
            A("^dKeepsake^0", "^dПамятная вещь^0");
            A("Carried: {0}", "В наличии: {0}");
            A("^d{0} {1}  -  item level {2}^0", "^d{0} · {1}  -  ур. предмета {2}^0");
            A("Common", "Обычное");
            A("Fine", "Добротное");
            A("Rare", "Редкое");
            A("Relic", "Реликвия");
            A("Armor", "Броня");
            A("Longsword", "Длинный меч");
            A("Greataxe", "Секира");
            A("Staff", "Посох");
            A("Wand", "Жезл");
            A("Daggers", "Кинжалы");
            A("Kris", "Крис");
            A("Longbow", "Длинный лук");
            A("Shortbow", "Короткий лук");
            A("^w{0} - {1} damage^0", "^wУрон {0} - {1}^0");
            A("  ^g(+{0} dps)^0", "  ^g(+{0} ур/с)^0");
            A("  ^r({0} dps)^0", "  ^r({0} ур/с)^0");
            A("^d{0} attacks per second^0", "^dАтак в секунду: {0}^0");
            A("^dWorth {0} gold^0", "^dЦена: {0} зол.^0");
            // quest items
            A("Abbey Sigil", "Печать аббатства");
            A("A seal of grey stone. The abbey doors answer to it.", "Печать из серого камня. Врата аббатства отзываются на неё.");
            A("Moonpetal", "Лунный лепесток");
            A("Cold to the touch and faintly bright.", "Холодный на ощупь и слабо светится.");
            A("Tobin's Lantern", "Фонарь Тобина");
            A("Still warm. He kept it lit to the end.", "Ещё тёплый. Он не дал ему погаснуть до самого конца.");
            A("The Bell's Tongue", "Язык колокола");
            A("The clapper of the Vesper Bell, torn out by the Prior's own hand.", "Язык Вечернего колокола, вырванный рукой самого приора.");
            // starting gear
            A("Oathguard's Longsword", "Длинный меч клятвенного стража");
            A("Lamp-keeper's Staff", "Посох хранителя ламп");
            A("Pair of Dusk Knives", "Пара сумеречных ножей");
            A("Fenwarden's Longbow", "Длинный лук стража топей");
            A("It has seen you this far.", "Оно довело вас досюда.");
            A("Dented Hauberk", "Помятая кольчуга");
            A("Singed Vestment", "Опалённое облачение");
            A("Patched Leathers", "Латаный кожаный доспех");
            A("Travel-worn Jerkin", "Поношенная куртка");
            // relics
            A("Vespertine, the Evening Blade", "Веспертин, Вечерний клинок");
            A("Cindersong", "Песнь углей");
            A("The Quiet Pair", "Тихая пара");
            A("Duskwhisper", "Шёпот сумерек");
            A("It rings, very softly, when drawn.", "Вынутый из ножен, он едва слышно звенит.");
            A("The ember at its head has never gone out.", "Уголёк в его навершии ни разу не гас.");
            A("They have never once been heard.", "Их ещё никто ни разу не услышал.");
            A("Its string hums a note like a far-off bell.", "Его тетива гудит нотой далёкого колокола.");
            A("Orrin's Vigil", "Бдение Оррина");
            A("Shroud of the Last Lamp", "Покров последней лампы");
            A("Hushwalker's Garb", "Одеяние идущего в тишине");
            A("Fenwarden's Oathcoat", "Клятвенный кафтан стража топей");
            A("Laid away beneath the abbey against a darker day. This is that day.", "Спрятано под аббатством на чёрный день. Этот день настал.");
            A("Crown-Shard of the Barrow Kings", "Осколок короны курганных королей");
            A("A splinter of a crown no one living remembers.", "Осколок короны, которую не помнит никто из живых.");
            A("Sir Caldus' Signet", "Перстень сэра Калдуса");
            A("Cold iron. It tightens when the dead are near.", "Холодное железо. Сжимается, когда рядом мёртвые.");
            A("Lamplighter's Charm", "Оберег фонарщика");
            A("Wren tied the cord herself. 'So you don't get lost too.'", "Рен сама завязала шнурок. «Чтобы и тебя не потерять»." );
            A("Ysolde's Sprig-Ring", "Кольцо-веточка Изольды");
            A("A band of woven moonpetal stem that never wilts.", "Колечко из сплетённого стебля лунного лепестка. Оно не вянет.");
            A("Bram's Best Greataxe", "Лучшая секира Брама");
            A("Bram's Best Wand", "Лучший жезл Брама");
            A("Bram's Best Kris", "Лучший крис Брама");
            A("Bram's Best Shortbow", "Лучший короткий лук Брама");
            A("'Bring it back in one piece. Or don't - just bring yourself.'", "«Верни в целости. А не выйдет — вернись хоть сам»." );
        }
    }
}
