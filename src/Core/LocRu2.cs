namespace Vesperfell
{
    /// <summary>The Russian translation, part two: classes, the bestiary, places, quests, signs and every line of talk.</summary>
    public static partial class LocRu
    {
        // classes
        static void Classes()
        {
            A("Warrior", "Воин");
            A("Oathguard of the Bell", "Клятвенный страж Колокола");
            A("A wall of steel. Builds Fury by striking and blocking, then spends it on crushing blows.", "Стальная стена. Копит Ярость ударами и блоками, а затем тратит её на сокрушительные приёмы.");
            A("Steel and shield. Every blow builds Fury.", "Сталь и щит. Каждый удар копит Ярость.");
            A("The Oathguard once stood at the abbey doors while the Vesper Bell was rung. They are the last who remember the rite.", "Клятвенные стражи стояли у врат аббатства, пока звонил Вечерний колокол. Они последние, кто помнит обряд.");
            A("Fury", "Ярость");
            A("Bulwark: strikes and blocked blows build Fury. Heavy armour.", "Оплот: удары и отражённые атаки копят Ярость. Тяжёлая броня.");
            A("Cleave", "Рассечение");
            A("A three-swing combo. The third swing is wider, hits harder and knocks foes back. Each foe struck builds Fury.", "Серия из трёх взмахов. Третий шире, бьёт сильнее и отбрасывает врагов. Каждый задетый враг даёт Ярость.");
            A("Bulwark", "Оплот");
            A("Hold to raise your shield. Blocks 85% of damage from the front, stops projectiles and builds Fury.", "Удерживайте, чтобы поднять щит. Блокирует 85% урона спереди, останавливает снаряды и копит Ярость.");
            A("Shield Rush", "Натиск щитом");
            A("Charge forward, damaging and stunning every foe in your path. Builds Fury.", "Рывок вперёд: ранит и оглушает всех врагов на пути. Копит Ярость.");
            A("Whirlwind", "Вихрь");
            A("Spin with your weapon outstretched, striking all nearby foes twice.", "Вращение с вытянутым оружием: дважды бьёт всех врагов вокруг.");
            A("Vesper Oath", "Вечерняя клятва");
            A("Swear the old oath. For 6 seconds take half damage, deal 25% more and mend your wounds.", "Принесите древнюю клятву. 6 секунд вы получаете вдвое меньше урона, наносите на 25% больше и залечиваете раны.");

            A("Mage", "Маг");
            A("Cinderwright", "Хранитель углей");
            A("Fragile and devastating. Hurls embers from afar, freezes what comes close and blinks out of reach.", "Хрупок и смертоносен. Мечет угли издалека, замораживает тех, кто подошёл близко, и ускользает скачком.");
            A("Fragile but deadly. Fire, frost and storm.", "Хрупок, но смертоносен. Огонь, лёд и буря.");
            A("Cinderwrights kept the abbey's lamps burning with captured embers. Now they turn that fire on the dark.", "Хранители углей питали лампы аббатства пойманным огнём. Теперь они обращают это пламя против тьмы.");
            A("Mana", "Мана");
            A("Kindled: mana returns steadily. Spells light the dark around them.", "Тлеющий: мана восполняется сама. Заклинания освещают тьму вокруг.");
            A("Ember Bolt", "Огненная стрела");
            A("Fling a bolt of living ember. Costs no mana.", "Метает сгусток живого огня. Не тратит ману.");
            A("Blink", "Скачок");
            A("Vanish and reappear a short distance toward your cursor. Briefly untouchable.", "Исчезните и появитесь чуть ближе к курсору. Ненадолго неуязвимы.");
            A("Fireball", "Огненный шар");
            A("A roaring sphere that explodes on impact and leaves foes burning.", "Ревущий шар: взрывается при попадании и поджигает врагов.");
            A("Frost Nova", "Кольцо льда");
            A("A ring of killing frost. Freezes nearby foes solid, then leaves them slowed.", "Кольцо смертельного холода. Намертво сковывает ближних врагов, а потом замедляет их.");
            A("Stormcall", "Зов бури");
            A("Lightning strikes the foe nearest your cursor and leaps between up to four more.", "Молния бьёт врага, ближайшего к курсору, и перескакивает ещё на четверых.");

            A("Rogue", "Разбойник");
            A("Duskblade", "Сумеречный клинок");
            A("Quick, cruel and hard to pin down. Strikes from behind for bonus damage and vanishes when cornered.", "Быстр, жесток и неуловим. Со спины бьёт сильнее, а загнанный в угол — исчезает.");
            A("Quick and cruel. Strikes from behind, then vanishes.", "Быстр и беспощаден. Бьёт в спину и исчезает.");
            A("When the long dusk fell, the Duskblades stopped stealing from the living and started hunting the dead.", "Когда пали долгие сумерки, Сумеречные клинки перестали обворовывать живых и начали охотиться на мёртвых.");
            A("Energy", "Энергия");
            A("Backstab: +50% damage from behind. High critical chance. Two dodge charges.", "Удар в спину: +50% урона сзади. Высокий шанс крита. Два переката.");
            A("Twin Fangs", "Парные клыки");
            A("A flurry of quick stabs. Strikes from behind deal 50% more damage.", "Шквал быстрых уколов. Удары в спину наносят на 50% больше урона.");
            A("Thrown Knife", "Метательный нож");
            A("Flick a knife at range.", "Бросок ножа на расстояние.");
            A("Shadowstep", "Шаг тени");
            A("Dash through foes, cutting each one. Your next strike is a guaranteed critical hit.", "Рывок сквозь врагов с ударом по каждому. Следующий удар — гарантированно критический.");
            A("Veil", "Завеса");
            A("Melt into smoke for 4 seconds. Foes lose track of you; your next attack is a devastating ambush.", "Растворитесь в дыму на 4 секунды. Враги теряют вас из виду, а следующая атака станет сокрушительной засадой.");
            A("Fan of Knives", "Веер ножей");
            A("Hurl a ring of poisoned knives in every direction.", "Метните кольцо отравленных ножей во все стороны.");

            A("Ranger", "Следопыт");
            A("Fenwarden", "Болотный страж");
            A("Controls the field from a distance with bow, snare and falling arrows.", "Держит поле боя издалека: лук, силки и дождь стрел.");
            A("Bow, snare and falling arrows. Rules the open ground.", "Лук, силки и дождь стрел. Хозяин открытого поля.");
            A("Fenwardens walk the wild edge of the vale. They were the first to see the wolves change.", "Болотные стражи бродят по диким окраинам долины. Они первыми увидели, как изменились волки.");
            A("Focus", "Фокус");
            A("Steady hand: long reach and reliable critical hits.", "Твёрдая рука: большая дальность и надёжные критические удары.");
            A("Loose", "Выстрел");
            A("Draw and loose an arrow. Hold to keep firing.", "Натяните тетиву и выстрелите. Удерживайте, чтобы стрелять без остановки.");
            A("Piercing Shot", "Пронзающий выстрел");
            A("A heavy arrow that punches through every foe in a line.", "Тяжёлая стрела пробивает всех врагов на одной линии.");
            A("Snare", "Силок");
            A("Set a hidden trap at your cursor. The first foe to step in is wounded and rooted in place.", "Скрытая ловушка у курсора. Первый наступивший враг ранен и обездвижен.");
            A("Arrow Rain", "Град стрел");
            A("Call a volley onto an area. Arrows fall for a few seconds, wounding and slowing all within.", "Залп по области. Несколько секунд падают стрелы, раня и замедляя всех внутри.");
            A("Hunter's Mark", "Метка охотника");
            A("Mark the foe nearest your cursor - it takes 35% more damage - and send a seeking arrow after it.", "Пометьте врага, ближайшего к курсору (он получает на 35% больше урона), и пустите в него самонаводящуюся стрелу.");

            // weapon and armour kinds, also used whole by the item-name builder
            A("Longsword", "Длинный меч"); A("Arming Sword", "Рыцарский меч"); A("Warblade", "Боевой клинок");
            A("Greataxe", "Секира"); A("Headsman's Axe", "Топор палача"); A("Cleaver", "Тесак");
            A("Staff", "Посох"); A("Ember Staff", "Тлеющий посох"); A("Brandstaff", "Огненный посох");
            A("Wand", "Жезл"); A("Cinder Wand", "Пепельный жезл"); A("Kindling Rod", "Запальный прут");
            A("Daggers", "Кинжалы"); A("Stilettos", "Стилеты");
            A("Kris", "Крис"); A("Serpent Blade", "Змеиный клинок"); A("Flamberge Knife", "Пламенеющий нож");
            A("Longbow", "Длинный лук"); A("Warbow", "Боевой лук"); A("Yew Bow", "Тисовый лук");
            A("Shortbow", "Короткий лук"); A("Hunting Bow", "Охотничий лук"); A("Horn Bow", "Роговой лук");
            A("Hauberk", "Кольчуга"); A("Cuirass", "Кираса"); A("Warplate", "Латы");
            A("Vestment", "Облачение"); A("Robe", "Мантия"); A("Mantle", "Накидка");
            A("Leathers", "Кожаный доспех"); A("Shadowgarb", "Одеяние тени"); A("Harness", "Перевязь");
            A("Jerkin", "Куртка"); A("Huntcoat", "Охотничий кафтан"); A("Wildhide", "Звериная шкура");
            A("Ring", "Кольцо"); A("Signet", "Перстень"); A("Amulet", "Амулет"); A("Charm", "Оберег");
        }

        // the bestiary
        static void Bestiary()
        {
            A("Gloam Wolf", "Мглистый волк");
            A("Pack Mother", "Мать стаи");
            A("Mire Crawler", "Трясинный ползун");
            A("Hollow", "Пустой");
            A("Barrow Archer", "Курганный лучник");
            A("Gloam Wisp", "Мглистый огонёк");
            A("Acolyte of the Hush", "Послушник Тишины");
            A("Sir Caldus, the Barrow Knight", "Сэр Калдус, курганный рыцарь");
            A("The Silent Prior", "Безмолвный приор");
        }

        // places and people
        static void Places()
        {
            A("The Low Road", "Нижний тракт");
            A("where the pilgrims once walked", "где когда-то шли паломники");
            A("Emberwick", "Эмбервик");
            A("the last lit hearths of the vale", "последние горящие очаги долины");
            A("The Thornwood", "Терновый лес");
            A("something watches from the brakes", "из чащи кто-то следит");
            A("Mirefen", "Трясина");
            A("drowned ground, drowned names", "утопшая земля, утопшие имена");
            A("The Barrowfield", "Курганное поле");
            A("kings sleep lightly here", "здесь чутко спят короли");
            A("Abbey Rise", "Аббатский холм");
            A("the bell tower stands mute", "колокольня молчит");
            A("The Ashen Fields", "Пепельные поля");
            A("burned the night the bell fell silent", "сгорели в ночь, когда умолк колокол");
            A("The Sunken Abbey", "Затонувшее аббатство");
            A("undercroft of Saint Orrin", "подземелье святого Оррина");
            A("The Bell Crypt", "Колокольный склеп");
            A("the long dusk has lifted", "долгие сумерки рассеялись");
            A("Vesperfell", "Весперфелл");

            // wayshrines
            A("Warden's Rest", "Приют стража");
            A("The Old Watchtower", "Старая сторожевая башня");
            A("The Stone Circle", "Каменный круг");
            A("The Abbey Steps", "Ступени аббатства");
            A("The Narthex", "Притвор");

            // people
            A("Elder Maren", "Старейшина Марен");
            A("Brother Halvard", "Брат Халвард");
            A("Bram the Smith", "Кузнец Брам");
            A("Bram", "Брам");
            A("Ysolde", "Изольда");
            A("Odo", "Одо");
            A("Wren", "Рен");
            A("Villager", "Житель");
            A("Sister Ilse", "Сестра Ильза");
            A("Warden", "Страж");

            // loading
            A("Painting the dusk", "Рисуем сумерки");
            A("Raising walls and stones", "Возводим стены и камни");
            A("Planting the Thornwood", "Сажаем Терновый лес");
            A("Waking the dead", "Будим мёртвых");
            A("Descending", "Спускаемся");
            A("Growing the Thornwood", "Растим Терновый лес");
            A("Raising Emberwick", "Строим Эмбервик");
            A("Unearthing the Barrowfield", "Раскапываем Курганное поле");
            A("Lighting the undercroft", "Зажигаем огни в подземелье");
            A("Shaping the vale in three dimensions", "Придаём долине объём");

            A("Unknown save version", "Неизвестная версия сохранения");
            A("Vesperfell ran into a problem and has to close.", "В Весперфелле произошла ошибка, и игра будет закрыта.");
            A("Details were written to crash.log.", "Подробности записаны в crash.log.");
            A("Save is missing its class", "В сохранении не указан класс");

            // loading-screen lore
            A("Each evening the Vesper Bell was rung, and the Gloam drew back from the vale. Thirty nights ago, it fell silent.", "Каждый вечер звонил Вечерний колокол, и Мгла отступала из долины. Тридцать ночей назад он умолк.");
            A("Wayshrines still hold a spark of the bell's blessing. Rest at one to mend your wounds and mark your return.", "В святилищах ещё теплится искра благословения колокола. Отдохните у святилища, чтобы залечить раны и вернуться туда после гибели.");
            A("Hold your ground or give it: Space rolls you clear of a telegraphed blow.", "Стойте на месте или уступите: Пробел — перекат из-под удара, который виден заранее.");
            A("The dead of the Barrowfield were kings once. They do not care for visitors.", "Мертвецы Курганного поля когда-то были королями. Гостей они не жалуют.");
            A("Moonpetals only open in the dark. In Vesperfell, that is no longer a short season.", "Лунные лепестки раскрываются только в темноте. В Весперфелле эта пора больше не коротка.");
            A("A red shape on the ground is a blow about to land. Be somewhere else.", "Красная фигура на земле — это удар, который вот-вот обрушится. Окажитесь в другом месте.");
            A("Bram the smith will temper worn steel for coin. Ysolde sells draughts for those who bleed often.", "Кузнец Брам за монету закалит потёртую сталь. Изольда продаёт настои тем, кто часто истекает кровью.");
            A("Resting at a wayshrine stirs the Gloam: common foes will rise again.", "Отдых у святилища тревожит Мглу: обычные враги восстают снова.");
            A("They say the Prior loved silence more than he loved the saint.", "Говорят, приор любил тишину больше, чем святого.");
            A("Three stones remember the old kings. Wake all three and their hoard may open.", "Три камня помнят древних королей. Пробудите все три — и их клад, быть может, откроется.");
        }

        // quests
        static void QuestText()
        {
            A("The Silent Bell", "Безмолвный колокол");
            A("For four hundred years the Vesper Bell was rung at each day's end, and the Gloam kept to its side of the vale. Thirty nights ago the bell fell silent. The dusk has not ended since.", "Четыреста лет Вечерний колокол звонил на исходе каждого дня, и Мгла держалась своей стороны долины. Тридцать ночей назад колокол умолк. С тех пор сумерки не кончаются.");
            A("The dawn.", "Рассвет.");
            A("Speak with Brother Halvard beside the campfire.", "Поговорите с братом Халвардом у костра.");
            A("Follow the road north to Emberwick and find Elder Maren.", "Идите по дороге на север, в Эмбервик, и найдите старейшину Марен.");
            A("Take the Abbey Sigil from Sir Caldus at the King's Barrow, east of Emberwick.", "Заберите Печать аббатства у сэра Калдуса в Королевском кургане к востоку от Эмбервика.");
            A("Carry the Sigil north, through the graveyard, to the doors of the Sunken Abbey.", "Отнесите Печать на север, через кладбище, к вратам Затонувшего аббатства.");
            A("Kindle the two vigil-flames in the abbey's west and east wings. ({0}/{1})", "Зажгите два стражевых огня в западном и восточном крыле аббатства. ({0}/{1})");
            A("Pass the Bellgate and face the Silent Prior in the Bell Crypt.", "Пройдите Колокольные врата и сразитесь с Безмолвным приором в Колокольном склепе.");
            A("Hang the Bell's Tongue and ring the Vesper Bell.", "Повесьте язык на место и ударьте в Вечерний колокол.");
            A("Return to Elder Maren in Emberwick.", "Вернитесь к старейшине Марен в Эмбервик.");

            A("Thinning the Pack", "Проредить стаю");
            A("The wolves of the Thornwood have grown gaunt and pale-eyed, and bold enough to stand at the palisade after lamp-lighting. Bram wants their numbers cut.", "Волки Тернового леса отощали, глаза у них побелели, и они так осмелели, что стоят у частокола, когда зажигают фонари. Брам хочет, чтобы их стало меньше.");
            A("Bram's finest work, and coin.", "Лучшая работа Брама и монеты.");
            A("Hunt Gloam Wolves in the Thornwood, west of Emberwick. ({0}/{1})", "Охотьтесь на мглистых волков в Терновом лесу к западу от Эмбервика. ({0}/{1})");
            A("Return to Bram at the forge in Emberwick.", "Вернитесь к Браму в кузницу Эмбервика.");

            A("Moonpetal for the Fevered", "Лунный лепесток для больных");
            A("The dusk-fever is spreading through Emberwick. Moonpetal breaks it, but the flower now grows only in the Thornwood, glowing faintly among the roots.", "По Эмбервику ползёт сумеречная лихорадка. Лунный лепесток её сбивает, но теперь цветок растёт только в Терновом лесу и слабо светится среди корней.");
            A("Draughts, and a ring of living stem.", "Настои и кольцо из живого стебля.");
            A("Gather Moonpetals in the Thornwood. They glow in the dark. ({0}/{1})", "Соберите лунные лепестки в Терновом лесу. Они светятся в темноте. ({0}/{1})");
            A("Bring the Moonpetals to Ysolde in Emberwick.", "Принесите лунные лепестки Изольде в Эмбервик.");

            A("The Missing Lamplighter", "Пропавший фонарщик");
            A("Tobin the lamplighter went to tend the far lamps of the west road six nights ago. His daughter Wren is still waiting at the gate.", "Шесть ночей назад фонарщик Тобин ушёл зажигать дальние фонари на западной дороге. Его дочь Рен до сих пор ждёт у ворот.");
            A("A child's keepsake.", "Детская памятная вещица.");
            A("Search the deep Thornwood for Tobin: take the unlit path north from the fork in the west road.", "Ищите Тобина в глубине Тернового леса: от развилки на западной дороге идите на север по тёмной тропе.");
            A("Bring Tobin's lantern home to Wren in Emberwick.", "Отнесите фонарь Тобина домой, к Рен в Эмбервик.");

            A("What the Stones Keep", "Что хранят камни");
            A("An old carving", "Старая резьба");
            A("Three rune stones older than the abbey stand in the vale: at the old watchtower in the Thornwood, in the southern Barrowfield, and on an islet in Mirefen. The carvings say that waking all three will open the king's door.", "В долине стоят три рунных камня старше аббатства: у старой сторожевой башни в Терновом лесу, на юге Курганного поля и на островке в Трясине. Резьба гласит: пробуди все три — и королевская дверь откроется.");
            A("Whatever the barrow-kings buried.", "Всё, что зарыли курганные короли.");
            A("Wake the three rune stones: the watchtower, the Barrowfield and Mirefen. ({0}/{1})", "Пробудите три рунных камня: у башни, на Курганном поле и в Трясине. ({0}/{1})");
            A("Open the king's door, in the far north-east of the Barrowfield.", "Откройте королевскую дверь на дальнем северо-востоке Курганного поля.");
        }

        // signs and notes
        static void Signs()
        {
            A("Emberwick Notice Board", "Доска объявлений Эмбервика");
            A("^yBY ORDER OF ELDER MAREN^0\nNo one walks the roads after the lamps are lit. That is, no one walks the roads.\n\n" +
              "^yWANTED^0  Wolf pelts, any condition. See Bram at the forge.\n\n^yMISSING^0  Tobin the lamplighter. Went west to tend the Thornwood lamps. Ask for Wren.\n\n" +
              "^dSomeone has scratched underneath: the bell isn't broken. it was silenced.^0",
              "^yПО ПРИКАЗУ СТАРЕЙШИНЫ МАРЕН^0\nПосле того как зажгут фонари, по дорогам никто не ходит. То есть по дорогам вообще никто не ходит.\n\n" +
              "^yКУПЛЮ^0  Волчьи шкуры, в любом виде. Спросить Брама в кузнице.\n\n^yПРОПАЛ^0  Фонарщик Тобин. Ушёл на запад к фонарям Тернового леса. Спросить Рен.\n\n" +
              "^dКто-то нацарапал снизу: колокол не сломан. его заставили замолчать.^0");
            A("Waymarker", "Дорожный указатель");
            A("^wNORTH^0 - Emberwick, a short walk.\n\nThe other arms of the post have been snapped off. Someone has carved a bell into the wood, and then scratched it out.",
              "^wСЕВЕР^0 — Эмбервик, рукой подать.\n\nОстальные стрелки указателя обломаны. Кто-то вырезал на дереве колокол, а потом выскоблил его.");
            A("A soldier's marker", "Солдатский знак");
            A("HERE THE WATCH ENDED.\n\nWe lit the beacon three nights running. No answer came from the abbey. On the fourth night the wolves' eyes were the wrong colour.",
              "ЗДЕСЬ ЗАКОНЧИЛСЯ ДОЗОР.\n\nТри ночи подряд мы жгли сигнальный огонь. Аббатство не ответило. На четвёртую ночь глаза у волков стали не того цвета.");
            A("A mossy stone", "Замшелый камень");
            A("The carving is older than the abbey:\n\n^cWHAT THE BELL KEEPS OUT, THE STONES KEEP IN. WAKE US THREE AND THE KING'S DOOR OPENS.^0",
              "Резьба старше аббатства:\n\n^cЧТО КОЛОКОЛ ДЕРЖИТ СНАРУЖИ, КАМНИ ДЕРЖАТ ВНУТРИ. ПРОБУДИ НАС ТРОИХ — И КОРОЛЕВСКАЯ ДВЕРЬ ОТКРОЕТСЯ.^0");
            A("Tobin's satchel", "Сумка Тобина");
            A("A folded scrap inside, in a careful hand:\n\n^wWren - the west lamps are lit as far as the old tower. I can hear them in the trees now, the ones with too many legs. If I am late, do not come looking. Keep the house lamp burning. - T.^0",
              "Внутри сложенный клочок, исписанный аккуратной рукой:\n\n^wРен, западные фонари горят до самой старой башни. Теперь я слышу их в деревьях — тех, у кого слишком много ног. Если я задержусь, не ходи меня искать. Не дай погаснуть лампе в доме. — Т.^0");
            A("^wWEST^0 - the old watchtower.\n^wNORTH^0 - the deep wood. ^rNo lamps beyond this point.^0\n^wSOUTH^0 - the river ford and Mirefen.",
              "^wЗАПАД^0 — старая сторожевая башня.\n^wСЕВЕР^0 — глубокий лес. ^rДальше фонарей нет.^0\n^wЮГ^0 — речной брод и Трясина.");
            A("A hunter's blaze", "Охотничья зарубка");
            A("Three notches, then an arrow pointing south toward the river.", "Три зарубки, а под ними стрелка на юг, к реке.");
            A("The King-Stone", "Королевский камень");
            A("Worn lettering circles the base:\n\n^cSEVEN KINGS LAY DOWN THEIR CROWNS WHEN THE FIRST BELL RANG. THEY HAVE BEEN WAITING FOR IT TO STOP.^0",
              "Основание опоясывают стёртые буквы:\n\n^cСЕМЬ КОРОЛЕЙ СЛОЖИЛИ КОРОНЫ, КОГДА ВПЕРВЫЕ ЗАЗВОНИЛ КОЛОКОЛ. С ТЕХ ПОР ОНИ ЖДУТ, КОГДА ОН СМОЛКНЕТ.^0");
            A("^wEAST^0 - the King's Barrow. ^rDo not disturb.^0\n^wSOUTH^0 - Garrow's farm.\n^wNORTH-EAST^0 - the old hoard-mound. Sealed since before the abbey.",
              "^wВОСТОК^0 — Королевский курган. ^rНе тревожить.^0\n^wЮГ^0 — ферма Гарроу.\n^wСЕВЕРО-ВОСТОК^0 — старый курган с кладом. Запечатан ещё до аббатства.");
            A("Founder's plaque", "Табличка основателя");
            A("^wSAINT ORRIN^0 hung the Vesper Bell in this place and set seven wardens to ring it at each day's end.\n\n^d'So long as it sounds, the Gloam shall not cross the vale.'^0\n\nBelow, a newer and cruder inscription: ^rHE WHO LOVES SILENCE NEED NOT FEAR IT.^0",
              "^wСВЯТОЙ ОРРИН^0 повесил здесь Вечерний колокол и поставил семерых стражей звонить в него на исходе каждого дня.\n\n^d«Покуда он звучит, Мгла не перейдёт долину».^0\n\nНиже — надпись новее и грубее: ^rКТО ЛЮБИТ ТИШИНУ, ТОМУ НЕЧЕГО ЕЁ БОЯТЬСЯ.^0");
            A("A fresh grave", "Свежая могила");
            A("The earth is newly turned. A warden's tabard is folded on top, and a single white flower.\n\n^wBROTHER ANSEL. HE RANG IT ALONE FOR NINE NIGHTS.^0",
              "Земля недавно вскопана. Сверху сложена накидка стража и лежит один белый цветок.\n\n^wБРАТ АНСЕЛЬ. ДЕВЯТЬ НОЧЕЙ ОН ЗВОНИЛ ОДИН.^0");
            A("A leaning post", "Покосившийся столб");
            A("^wMIREFEN^0\n\nThe paint has run. Underneath, older letters: ^dmind the lights. they are not lamps.^0",
              "^wТРЯСИНА^0\n\nКраска потекла. Под ней проступают старые буквы: ^dберегись огоньков. это не фонари.^0");
            A("Garrow's Farm", "Ферма Гарроу");
            A("The board is charred at the edges.\n\nNailed beneath it, a child's drawing: a house, four figures, and above them a great black circle with no clapper.",
              "Края доски обуглены.\n\nПод ней прибит детский рисунок: дом, четыре фигурки, а над ними огромный чёрный круг без языка.");
            A("A pilgrim's remains", "Останки паломника");
            A("A hand still clutches a slip of vellum:\n\n^wTwo vigil-flames, west and east, kept the Bellgate. The Prior put them out himself. I watched him do it. He was smiling.^0",
              "Рука всё ещё сжимает клочок пергамента:\n\n^wДва стражевых огня, на западе и на востоке, хранили Колокольные врата. Приор сам их погасил. Я видел это. Он улыбался.^0");
            A("The Prior's ledger", "Книга приора");
            A("The last pages are written in a hand that grows steadily smaller.\n\n^w...the bell is a wound in the evening. Each stroke tears the quiet open again. I have prayed on it. Saint Orrin does not answer, but the dusk does.^0\n\n" +
              "^wI have taken its tongue. I will keep it close, where it cannot speak.^0",
              "Последние страницы исписаны почерком, который становится всё мельче.\n\n^w...колокол — рана на теле вечера. Каждый удар заново разрывает тишину. Я молился об этом. Святой Оррин не отвечает, но отвечают сумерки.^0\n\n" +
              "^wЯ забрал его язык. Я буду держать его при себе, там, где он не сможет говорить.^0");
        }

        // talk
        static void Talk()
        {
            A("Farewell.", "Прощайте.");
            A("Something else.", "О другом.");

            // Brother Halvard
            A("So. You came. I had begun to think the road had taken you as well.\n\nThirty nights ago the Vesper Bell fell silent, and the dusk has not ended since. The Gloam is in the vale now. You will have seen the wolves' eyes.",
              "Итак, вы пришли. Я уже начал думать, что дорога забрала и вас.\n\nТридцать ночей назад Вечерний колокол умолк, и с тех пор сумерки не кончаются. Мгла уже в долине. Вы, должно быть, видели глаза волков.");
            A("What must I do?", "Что мне делать?");
            A("What is the Vesper Bell?", "Что такое Вечерний колокол?");
            A("Listen.\n\n...Four hundred years the Order rang it, and I never once just stood still and listened. Thank you, Warden. Go and be warm somewhere.",
              "Слушайте.\n\n...Четыреста лет Орден звонил в него, и я ни разу просто не остановился послушать. Спасибо, Страж. Ступайте, погрейтесь где-нибудь.");
            A("Tell me about the bell.", "Расскажите о колоколе.");
            A("Still breathing? Good. This fire is the only thing left on the road that knows what hour it is.",
              "Ещё дышите? Хорошо. Этот костёр — единственное на дороге, что ещё знает, который час.");
            A("What is the Gloam?", "Что такое Мгла?");
            A("Any advice for the road?", "Дадите совет в дорогу?");
            A("Go north to Emberwick and find Elder Maren. She kept the abbey's keys for thirty years, until the Prior turned her out.\n\nTake these draughts. I am too old to need them, and you are about to need them badly.",
              "Идите на север, в Эмбервик, и найдите старейшину Марен. Тридцать лет она хранила ключи аббатства, пока приор её не выгнал.\n\nВозьмите эти настои. Мне они уже ни к чему, а вам скоро очень понадобятся.");
            A("Saint Orrin hung it four hundred years ago. Rung at each day's end, it told the dark where the vale stopped.\n\nSeven wardens to ring it. I was the seventh. The Prior dismissed us one by one - gently, always gently - and not one of us thought to ask him why.",
              "Святой Оррин повесил его четыреста лет назад. Он звонил на исходе каждого дня и указывал тьме, где кончается долина.\n\nЗвонить в него полагалось семерым стражам. Я был седьмым. Приор отпустил нас одного за другим — мягко, всегда мягко, — и никто из нас не догадался спросить зачем.");
            A("It is not fog and it is not night. It is what was here before the bell - older than the barrow-kings.\n\nIt gets into things. Beasts first. Then the dead. Then, I think, anyone who listens to it for too long.",
              "Это не туман и не ночь. Это то, что было здесь до колокола, — оно древнее курганных королей.\n\nОна проникает во всё. Сперва в зверей. Потом в мёртвых. А потом, думаю, в любого, кто слишком долго к ней прислушивается.");
            A("Your shield is half your sword. Raise it (right mouse) just as a blow lands and the brute who swung will stagger. Every strike feeds your Fury - spend it.",
              "Щит — половина вашего меча. Поднимите его (правая кнопка мыши) в миг удара, и тот, кто замахнулся, пошатнётся. Каждый удар питает вашу Ярость — тратьте её.");
            A("Keep your distance and keep moving. Blink (right mouse) when they close in, and remember that frost holds a foe still long enough to burn it.",
              "Держитесь на расстоянии и не стойте на месте. Когда враги подступят, делайте скачок (правая кнопка мыши). И помните: лёд держит врага достаточно долго, чтобы его сжечь.");
            A("Never fight from the front if you can help it. Step through them (Q) and cut from behind - and when it turns bad, you have two rolls where the rest of us have one.",
              "Никогда не деритесь в лоб, если можно иначе. Пройдите сквозь них (Q) и бейте в спину, а если станет худо — у вас два переката там, где у остальных один.");
            A("Let them come to you across open ground. Lay a snare (Q) on the path they will take, and loose the piercing shot (right mouse) when they line up.",
              "Пусть идут к вам по открытому месту. Поставьте силок (Q) у них на пути, а когда выстроятся в линию — пускайте пронзающий выстрел (правая кнопка мыши).");
            A("Rest at the wayshrines - they still hold a little of the bell's blessing. And when the ground turns red beneath you, be somewhere else.",
              "Отдыхайте у святилищ — в них ещё теплится благословение колокола. А когда земля под вами краснеет, уходите оттуда.");

            // Elder Maren
            A("Look at the sky, Warden. I had forgotten it could be that colour.\n\nStay as long as you like. There is still a great deal of dusk left in the corners of this vale, and you seem to be good at corners.",
              "Посмотрите на небо, Страж. Я и забыла, что оно бывает такого цвета.\n\nОставайтесь сколько захотите. По углам этой долины ещё полно сумерек, а углы вам, похоже, по плечу.");
            A("I heard it. We all heard it. Odo dropped a full tray and nobody laughed.\n\nYou rang the Vesper Bell.",
              "Я слышала. Мы все слышали. Одо уронил полный поднос, и никто не засмеялся.\n\nВы ударили в Вечерний колокол.");
            A("It is done. The Prior is gone.", "Всё кончено. Приора больше нет.");
            A("Halvard sent you? Then the Order has one blade left - or whatever it is you carry.\n\nI am Maren. I held the abbey's keys for thirty years, until Prior Aldous took them from me with a smile and a blessing.",
              "Вас прислал Халвард? Значит, у Ордена остался ещё один клинок — или что там у вас в руках.\n\nЯ Марен. Тридцать лет я хранила ключи аббатства, пока приор Альдус не забрал их у меня — с улыбкой и благословением.");
            A("How do I get into the abbey?", "Как попасть в аббатство?");
            A("Who is the Prior?", "Кто такой приор?");
            A("East, past the standing stones, to the King's Barrow. Sir Caldus was a hard man when he was alive.",
              "На восток, мимо стоячих камней, к Королевскому кургану. Сэр Калдус и при жизни был человеком суровым.");
            A("Tell me about Sir Caldus.", "Расскажите о сэре Калдусе.");
            A("You have it. I can feel it from here - cold, like a coin left on a windowsill.\n\nNorth, through the graveyard. Press it to the stone and the doors will know you.",
              "Она у вас. Я чувствую её отсюда — холодную, как монета, забытая на подоконнике.\n\nНа север, через кладбище. Прижмите её к камню, и врата вас узнают.");
            A("What waits inside?", "Что ждёт внутри?");
            A("Still breathing. Good. Do not let me keep you - but do not hurry so much that you forget to rest at the shrines.",
              "Ещё дышите. Хорошо. Не буду вас задерживать — но не спешите так, чтобы забыть отдохнуть у святилищ.");
            A("What waits inside the abbey?", "Что ждёт в аббатстве?");
            A("The doors are sealed with a sigil-stone, and he has the only one I ever held. But there was a second. It was buried with Sir Caldus, the abbey's first champion, in the King's Barrow out east.\n\nHe will not have stayed buried. Nothing does, now. Here - coin for the smith and the herbalist. Spend it before you go.",
              "Врата запечатаны камнем-печатью, и та, что была у меня, теперь у него. Но была и вторая. Её похоронили вместе с сэром Калдусом, первым защитником аббатства, в Королевском кургане на востоке.\n\nВряд ли он так и лежит в могиле. Теперь никто не лежит. Вот, возьмите монет для кузнеца и травницы. Потратьте их перед дорогой.");
            A("He charges like a bull and, like a bull, he is poor at stopping. Let him find a wall.\n\nWhen he raises that great sword over his head, do not be near him when it comes down.",
              "Он бросается вперёд, как бык, и, как бык, плохо умеет останавливаться. Пусть найдёт стену.\n\nА когда он поднимет над головой свой огромный меч — не стойте рядом, когда тот опустится.");
            A("Aldous. A gentle man. He used to say the bell hurt him - that every stroke went through him like a nail. We thought it was only age.\n\nOn the last night he sent the ringers away and climbed the tower alone. In the morning there was no morning.",
              "Альдус. Кроткий человек. Он говорил, что колокол причиняет ему боль — что каждый удар проходит сквозь него, как гвоздь. Мы думали, это просто старость.\n\nВ последнюю ночь он отослал звонарей и поднялся на башню один. А наутро утро не наступило.");
            A("The undercroft. Two vigil-flames kept the Bellgate, one in the west wing and one in the east - he will have put them out. Light them both and the way to the crypt will open.\n\nAnd if you meet Sister Ilse... be kind. She stayed when the rest of us ran.",
              "Подземелье. Колокольные врата хранили два стражевых огня, в западном крыле и в восточном, — он наверняка их погасил. Зажгите оба, и путь в склеп откроется.\n\nА если встретите сестру Ильзу... будьте с ней добры. Она осталась, когда мы все бежали.");
            A("Then he has his silence at last, poor man, and we have our evenings back.\n\nThis was laid by for the Order's champion. There has not been one in my lifetime. There is now.",
              "Значит, он наконец обрёл свою тишину, бедняга, а мы вернули себе вечера.\n\nЭто берегли для защитника Ордена. На моём веку такого не было. Теперь есть.");

            // Bram
            A("Need any work done?", "Есть для меня работа?");
            A("About those wolves...", "Насчёт волков...");
            A("The pack is thinned.", "Стая поредела.");
            A("Would you look at that. I can see what I'm hitting. Bram - and I owe you an ale, Warden.",
              "Вы только гляньте. Я вижу, по чему бью. Я Брам, и с меня эль, Страж.");
            A("Mind the sparks. Bram. I'd shake your hand, but it would take the skin off.",
              "Осторожно, искры. Я Брам. Пожал бы вам руку, да кожу сдеру.");
            A("Temper my weapon to +{0}  ({1} gold)", "Закалить оружие до +{0}  ({1} зол.)");
            A("Temper my armour to +{0}  ({1} gold)", "Закалить броню до +{0}  ({1} зол.)");
            A("Sell all Common and Fine gear in my pack  ({0} for {1} gold)", "Продать всё обычное и добротное  ({0} шт. за {1} зол.)");
            A("Wolves. They come up to the palisade at lamp-lighting now and just... stand there. Looking in. Their eyes are the wrong colour.\n\nThin the pack - six should do it - and you can have the best thing I've made this year.",
              "Волки. Теперь они подходят к частоколу, когда зажигают фонари, и просто... стоят. Смотрят внутрь. Глаза у них не того цвета.\n\nПроредите стаю — шести хватит, — и получите лучшее, что я выковал за год.");
            A("I'll hunt them.", "Я на них поохочусь.");
            A("Not now.", "Не сейчас.");
            A("West gate, follow the lamps. You'll hear them before you see them.", "За западные ворота и дальше вдоль фонарей. Вы услышите их раньше, чем увидите.");
            A("{0} of {1}. Keep at it. And mind the big one - the mother. She's cleverer than the rest.", "{0} из {1}. Продолжайте. И берегитесь большой — матери. Она умнее остальных.");
            A("Six? I slept last night. First time in a month. Here - I meant it for the Elder's nephew, but he ran off south, and you didn't.",
              "Шесть? Я этой ночью спал. Впервые за месяц. Держите — я делал это для племянника старейшины, но он сбежал на юг, а вы — нет.");
            A("That's {0} gold of work, friend, and you've {1}. Steel doesn't temper itself.", "Работа стоит {0} зол., друг, а у вас {1}. Сталь сама не закалится.");
            A("^y{0}^0 tempered", "Закалка: ^y{0}^0");
            A("There. Sharper than my wife's opinion of me.", "Готово. Острее, чем мнение моей жены обо мне.");
            A("There. That'll turn a blade that would have opened you yesterday.", "Готово. Теперь это остановит клинок, который вчера бы вас распорол.");
            A("Sold {0} items for ^y{1} gold^0", "Продано предметов: {0} за ^y{1} зол.^0");
            A("I'll melt the lot down. {0} gold, and that's generous.", "Переплавлю всё. {0} зол., и это щедро.");
            A("You've nothing I'd buy.", "У вас нет ничего, что я бы купил.");

            // Ysolde
            A("You look worried.", "У вас встревоженный вид.");
            A("About the moonpetals...", "Насчёт лунных лепестков...");
            A("I have the moonpetals.", "Лунные лепестки у меня.");
            A("Sunlight on the herb beds. They won't know what to do with themselves. Neither do I.", "Солнце на грядках с травами. Они не знают, куда себя деть. Я тоже.");
            A("Careful where you step - that's feverfew. I'm Ysolde. If you're bleeding, I have draughts. If you're not, give it an hour.",
              "Осторожнее, это пиретрум. Я Изольда. Если вы истекаете кровью, у меня есть настои. Если нет — подождите часок.");
            A("Buy a Health Draught  ({0} gold)", "Купить целебный настой  ({0} зол.)");
            A("Buy a Spirit Draught  ({0} gold)", "Купить настой духа  ({0} зол.)");
            A("Half the village has the dusk-fever. Moonpetal breaks it, but it only grows in the Thornwood now, and I can't send anyone there.\n\nFive blooms would see us through. They glow - you can't miss them in the dark, and it's always dark.",
              "Полдеревни слегло с сумеречной лихорадкой. Лунный лепесток её сбивает, но теперь он растёт только в Терновом лесу, а туда я никого послать не могу.\n\nПяти цветков нам хватит. Они светятся — в темноте не пропустите, а темно теперь всегда.");
            A("I'll gather them.", "Я их соберу.");
            A("You have them already? Then hand them over, quick.", "Они уже у вас? Тогда давайте скорее.");
            A("Bless you. Take the stem as well as the bloom, if you can.", "Благослови вас. Если сможете, берите цветок вместе со стеблем.");
            A("Here.", "Вот.");
            A("{0} of {1}. They like the roots of the big oaks, and the wet ground near the pond.", "{0} из {1}. Они любят корни больших дубов и сырую землю у пруда.");
            A("Oh, these are lovely - look how bright. That's the Fuller children and old Hask sorted by tomorrow.\n\nI wove this from the first stem I ever picked. It never wilted. I think it wants to be worn.",
              "Ох, какие чудесные — смотрите, как светятся. К завтрашнему дню дети Фуллеров и старый Хаск будут на ногах.\n\nЭто я сплела из самого первого стебля, что сорвала. Он так и не завял. Думаю, его пора носить.");
            A("That's {0} gold, love. I can't brew on kindness.", "Это {0} зол., дорогуша. На доброте зелья не сваришь.");
            A("There you are. Health Draughts: ^r{0}^0\n\n^dYou have {1} gold.^0", "Держите. Целебных настоев: ^r{0}^0\n\n^dУ вас {1} зол.^0");
            A("There you are. Spirit Draughts: ^b{0}^0\n\n^dYou have {1} gold.^0", "Держите. Настоев духа: ^b{0}^0\n\n^dУ вас {1} зол.^0");
            A("Another Health Draught  ({0} gold)", "Ещё целебный настой  ({0} зол.)");
            A("Another Spirit Draught  ({0} gold)", "Ещё настой духа  ({0} зол.)");

            // Odo
            A("Drinks are free until I remember I have a business to run. Odo. You'll be the one who rang it, then.",
              "Выпивка бесплатно, пока я не вспомню, что у меня тут вообще-то дело. Я Одо. Так это вы в него позвонили.");
            A("Welcome to the Last Candle. Odo. We're out of nearly everything, but the fire's real.",
              "Добро пожаловать в «Последнюю свечу». Я Одо. У нас кончилось почти всё, но огонь настоящий.");
            A("Heard anything worth hearing?", "Что интересного слышно?");
            A("A bed for the night  (5 gold)", "Постель на ночь  (5 зол.)");
            A("My grandfather laid stone at the abbey. Swore there was a vault off the ossuary the monks bricked up in a single night. 'You could hear it was hollow,' he said. 'Knock hard enough and it would answer.'",
              "Мой дед клал камень в аббатстве. Клялся, что рядом с оссуарием есть ниша, которую монахи замуровали за одну ночь. «Слышно было, что там пусто, — говорил он. — Постучи посильнее, и она ответит».");
            A("There are three old stones in this vale with signs cut in them - one by the watchtower in the Thornwood, one down the south end of the Barrowfield, one out in Mirefen on a bit of dry ground. Older than the abbey. They say if all three wake, the kings' hoard opens.",
              "В долине есть три старых камня с вырезанными знаками: один у сторожевой башни в Терновом лесу, другой на южном краю Курганного поля, третий в Трясине, на сухом пятачке. Они старше аббатства. Говорят, если пробудить все три, откроется клад королей.");
            A("North of the fork on the west road there's a thornbrake nobody's cut in years. Tobin used to say he saw something shining behind it. A good blade would make short work of thorns.",
              "К северу от развилки на западной дороге есть терновник, который годами никто не рубил. Тобин говорил, что видел за ним что-то блестящее. Хороший клинок быстро справится с колючками.");
            A("Sir Caldus? My nan used to frighten us with him. 'Charges like a bull,' she'd say, 'and can't stop any better.' Put a wall behind you and step aside.",
              "Сэр Калдус? Бабка нас им пугала. «Несётся, как бык, — говорила, — и тормозит не лучше». Встаньте спиной к стене и в последний миг отойдите.");
            A("The Prior never liked noise. Used to wince at my shutters. Funny thing to say about a churchman, but I don't think he ever forgave the bell for being loud.",
              "Приор никогда не любил шума. Морщился, когда я хлопал ставнями. Странно такое говорить о служителе церкви, но, по-моему, он так и не простил колоколу, что тот громкий.");
            A("Anything else?", "Что-нибудь ещё?");
            A("Five gold. I'd let you have it for nothing, but then they'd all want it for nothing.", "Пять золотых. Я бы пустил вас даром, но тогда даром захотят все.");
            A("^gYou sleep. For the first time in thirty nights, you dream of morning.^0\n\nUp already? The stew's on. It's mostly turnip.",
              "^gВы спите. Впервые за тридцать ночей вам снится утро.^0\n\nУже встали? Похлёбка на огне. В основном из репы.");

            // Wren
            A("I light it every night now. Somebody has to do the lamps.", "Теперь я зажигаю его каждую ночь. Кто-то же должен следить за фонарями.");
            A("Did you... is that...", "Это... это же...");
            A("I found this in the deep wood. I'm sorry.", "Это было в глубине леса. Мне очень жаль.");
            A("Did you find him? He has a lantern with a brass cap. He never goes anywhere without it.", "Вы его нашли? У него фонарь с медной крышкой. Он без него никуда не ходит.");
            A("Where did he go, again?", "Куда он пошёл, напомни?");
            A("Are you a warden? A real one?\n\nMy da's Tobin. He lights the lamps on the west road. He went out six nights ago to do the far ones, and he hasn't come back yet.",
              "Вы страж? Настоящий?\n\nМой папа — Тобин. Он зажигает фонари на западной дороге. Шесть ночей назад он ушёл к дальним и ещё не вернулся.");
            A("I'll look for him.", "Я его поищу.");
            A("I'm sorry. I can't.", "Прости. Не могу.");
            A("West road, to the fork with the signpost. Then north, where the lamps stop. He always said the deep wood lamps mattered most, because nobody else would do them.\n\nI'm not allowed past the gate. So I wait here.",
              "По западной дороге до развилки с указателем. Потом на север, туда, где кончаются фонари. Папа всегда говорил, что фонари в глубоком лесу важнее всего, потому что больше их никто не зажжёт.\n\nМне нельзя за ворота. Вот я и жду здесь.");
            A("(She holds it for a long time.)\n\nIt's still warm. He kept it lit so I'd know which way was home.\n\n...I made this for him, for when he came back. You have it. So you don't get lost too.",
              "(Она долго держит его в руках.)\n\nОн ещё тёплый. Папа не давал ему гаснуть, чтобы я знала, где дом.\n\n...Я сделала это для него — к его возвращению. Возьмите. Чтобы вы тоже не потерялись.");

            // Sister Ilse
            A("(She has turned to face the stairs, and the light coming down them.)\n\nOh. Oh, there it is. I had forgotten the sound. Thank you. I think I can go up now.",
              "(Она повернулась к лестнице, к свету, что льётся по ступеням.)\n\nО. Вот он. Я забыла этот звук. Спасибо. Кажется, теперь я могу подняться.");
            A("(The figure does not turn.)\n\nYou are warm. It has been some time since anything warm came down those stairs. I am - I was - Sister Ilse. I rang the Vesper with the others, until he sent us away. I did not go far enough.",
              "(Фигура не оборачивается.)\n\nОт вас веет теплом. Давно уже ничто тёплое не спускалось по этим ступеням. Я — я была — сестра Ильза. Я звонила к вечерне вместе с остальными, пока он не отослал нас. Я ушла недостаточно далеко.");
            A("What happened here?", "Что здесь случилось?");
            A("How do I reach the Prior?", "Как добраться до приора?");
            A("Is anything hidden in this place?", "Здесь что-нибудь спрятано?");
            A("Prior Aldous took the bell's tongue and sealed the doors, and one by one we stopped.\n\nHe is below, in the crypt, keeping the silence. He believes he is sparing us the noise. He was always kind.",
              "Приор Альдус забрал язык колокола и запечатал двери, и мы один за другим умолкли.\n\nОн внизу, в склепе, стережёт тишину. Он верит, что избавляет нас от шума. Он всегда был добр.");
            A("The Bellgate bars the chancel. Two vigil-flames open it: one in the scriptorium, west of the nave, and one in the ossuary to the east. He put them out himself.\n\nYou may relight them. I cannot hold fire any more.",
              "Колокольные врата преграждают путь к алтарю. Их открывают два стражевых огня: один в скриптории, к западу от нефа, другой в оссуарии на востоке. Он сам их погасил.\n\nВы можете зажечь их снова. Я больше не могу удержать огонь.");
            A("In the ossuary, the north wall. We laid Saint Orrin's own vestments behind it when the Gloam first stirred, and bricked it shut in a single night.\n\nThe mortar never set properly. Strike it.",
              "В оссуарии, у северной стены. Когда Мгла впервые шевельнулась, мы спрятали за ней облачение самого святого Оррина и замуровали её за одну ночь.\n\nРаствор так и не схватился как следует. Ударьте по ней.");

            // villagers
            A("I keep setting the table for supper. It's always supper-time now, isn't it?", "Я всё накрываю стол к ужину. Теперь ведь всегда время ужина, правда?");
            A("My hens have stopped laying. They don't know when morning is. Nor do I.", "Мои куры перестали нестись. Они не знают, когда утро. Я тоже.");
            A("Don't go west after... well. Don't go west.", "Не ходите на запад после... в общем. Не ходите на запад.");
            A("The Elder says help is coming up the Low Road. I suppose that's you. You're shorter than I hoped.", "Старейшина говорит, что по Нижнему тракту идёт помощь. Наверное, это вы. Я думала, вы повыше.");
            A("I heard something scratching at the palisade last night. Slowly. All the way along.", "Прошлой ночью что-то скреблось о частокол. Медленно. Вдоль всего забора.");
            A("Bram says the wolves just stand and watch. I said, watch what? He didn't answer.", "Брам говорит, волки просто стоят и смотрят. Я спросила: на что смотрят? Он не ответил.");
            A("I'd forgotten shadows could point somewhere.", "Я и забыла, что тени могут куда-то указывать.");
            A("The hens are laying again. All at once. It's chaos.", "Куры снова несутся. Все разом. Сущий хаос.");
            A("My mother wept when she saw the east. Then she told me to air the bedding.", "Мама заплакала, когда увидела восток. А потом велела проветрить постель.");
            A("They'll tell stories about you, Warden. I've already started one.", "О вас будут рассказывать истории, Страж. Я уже начала одну.");

            // 3D view
            A("Graphics", "Графика");
            A("after restart", "после перезапуска");
            A("Wheel", "Колесо");
            A("Zoom (3D view)", "Приближение (3D)");
            A("Zoom (overhead)", "Масштаб (сверху)");
            A("Camera", "Камера");
            A("First person", "Из глаз");
            A("Overhead", "Сверху");
            A("Mouse speed", "Скорость мыши");
            A("Invert mouse", "Инверсия мыши");
            A("Look and aim", "Обзор и прицел");
            A("Arrows", "Стрелки");
            A("Turn", "Поворот");
            A("^yWASD^0 to move  -  look with the ^ymouse^0", "^yWASD^0 — движение  -  обзор — ^yмышью^0");
            A("WASD move  -  mouse look  -  F interact  -  F11 fullscreen", "WASD — движение  -  мышь — обзор  -  F — действие  -  F11 — весь экран");
            A("Raising the vale", "Долина обретает объём");
            A("2D view: OpenGL 3.3 is not available", "2D-вид: OpenGL 3.3 недоступен");
        }
    }
}
