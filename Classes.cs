using System;
using System.Collections.Generic;

namespace FVTTtoLSSCharConverter {
	public class Character {
		public SortedList<string, CharacterClassData> classes = new SortedList<string, CharacterClassData>();
		public int conMod = 1;
		public bool isDwarf = false;
		public bool isDragonSorc = false;
		public bool hasToughFeat = false;
		public bool hasEpicGift = false;

		public string GetClassesString() {
			string result = "";

			for (int i = 0; i < classes.Count; i++) {
				result += classes.Values[i].className + " " + classes.Values[i].classLevel + ((i < classes.Count - 1) ? " \\ " : "");
			}

			Utilities.AddLog("LSS Classes String: " + result);

			return result;
		}

		public int GetTotalLevel() {
			int result = 0;

			for (int i = 0; i < classes.Count; i++) {
				result += classes.Values[i].classLevel;
			}

			Utilities.AddLog("LSS Total Character Level: " + result);

			return result;
		}

		public int GetProficiencyBonus() {
			return 1 + (int)Math.Ceiling(0.25f * GetTotalLevel());
		}

		/// <summary>
		/// useAutoPick = true: Find maximum caster level in multiclass
		/// useAutoPick = false: Wait for user input
		/// </summary>
		/// <param name="useAutoPick"></param>
		/// <returns></returns>
		public CharacterClassData FindMainCasterClass(bool useAutoPick = false) {
			string mainCasterClassName = "";
			int tmpLvl = 0;
			//string baseClassCasterCharacteristic = "";
			bool needToChoose = false;

			if (!useAutoPick) {
				Console.Write("=====List of multiclass caster classes=====\n");
			}

			for (int i = 0; i < classes.Count; i++) {
				if (useAutoPick) {
					// ToDo: find max caster class with same with base class spellcast characteristic
					//if (classes.Values[i].baseClass && classes.Values[i].HasSpellAbility) {
					//	baseClassCasterCharacteristic = classes.Values[i].spellcastCharacteristic;
					//}
					if (classes.Values[i].HasSpellAbility && tmpLvl < classes.Values[i].classLevel) {
						tmpLvl = classes.Values[i].classLevel;

						mainCasterClassName = classes.Keys[i];
					}
				} else if (classes.Values[i].HasSpellAbility) {
					Console.Write(i + ". " + classes.Values[i].classNameOriginal + "\n");
					needToChoose = true;
				}
			}

			if (!useAutoPick) {
				if (needToChoose) {
					Utilities.AddLog("Choose main caster class, type index: ");
					int n = Convert.ToInt32(Console.ReadLine());
					mainCasterClassName = classes.Keys[n];
					Utilities.AddLog("You pick " + classes.Values[n].classNameOriginal + " for your main caster class.");
				} else {
					Utilities.AddLog("No caster classes found!");
				}
			} else {
				Utilities.AddLog("FindMainCasterClass Name: " + mainCasterClassName);
			}

			return string.IsNullOrEmpty(mainCasterClassName) ? null : classes[mainCasterClassName];
		}

		public void CreateNewClass(bool isBase, string enName, string lvl) {
			classes.Add(enName, new CharacterClassData(isBase, enName, lvl));
		}

		public int GetMaxHP() {
			int result = 0;

			foreach (CharacterClassData cData in classes.Values) {
				foreach (int hp in cData.lvlsHP) {
					result += hp;
				}
			}

			if (conMod < 1)
				conMod = 1;

			result += conMod * GetTotalLevel();

			if (isDwarf) {
				result += GetTotalLevel();
			}

			if (isDragonSorc) {
				result += GetTotalLevel();
			}

			if (hasToughFeat) {
				result += 2 * GetTotalLevel();
			}

			if (hasEpicGift) {
				result += 40;
			}

			Utilities.AddLog("Character max hp: " + result);

			return result;
		}

		public string GetHpDiceString() {
			string result = "multiclass";

			if (classes.Count == 1) {
				result = classes.Values[0].hitDice;
			}

			return result;
		}

		public Dictionary<string, int> GetHpDiceMulti() {
			Dictionary<string, int> result = new Dictionary<string, int>();

			result.Add("d6", 0);
			result.Add("d8", 0);
			result.Add("d10", 0);
			result.Add("d12", 0);

			foreach (CharacterClassData cData in classes.Values) {
				result[cData.hitDice] += cData.classLevel;
			}

			return result;
		}

		public List<CharacterClassData> GetCastClassesList() {
			List<CharacterClassData> result = new List<CharacterClassData>();

			foreach (CharacterClassData cData in classes.Values) {
				if (cData.HasSpellAbility) {
					result.Add(cData);
				}
			}

			return result;
		}

		public List<CharacterClassData> GetAllClassesList() {
			List<CharacterClassData> result = new List<CharacterClassData>();

			foreach (CharacterClassData cData in classes.Values) {
				result.Add(cData);
			}

			return result;
		}
	}

	public class CharacterClassData {
		public bool baseClass = false;
		public int classLevel = 1;
		public string classNameOriginal;
		public string className;
		public string subclassName;
		public string hitDice = "d6";
		public int hitDiceValue = 6;
		public List<int> lvlsHP = new List<int>();
		public List<string> saves = new List<string>();

		public bool HasSpellAbility {
			get { return (spellcastCharacteristic != "none"); }
		}
		public string spellcastCharacteristic = "none";
		public int spellSave = 0;
		public int spellAttackBonus = 0;

		public CharacterClassData() {

		}

		public CharacterClassData(bool isBase, string originalName, string lvl) {
			baseClass = isBase;
			classNameOriginal = originalName;

			int parsedClassLvl = 1;
			int.TryParse(lvl, out parsedClassLvl);

			classLevel = parsedClassLvl;
		}

		public void SetHitDice(string diceStr) {
			hitDice = diceStr;
			int diceValue = 6;
			int.TryParse(diceStr.Substring(1), out diceValue);
			hitDiceValue = diceValue;

			Utilities.AddLog("hitDice: " + hitDice + " | diceValue: " + hitDiceValue);
		}

		public string GetCasterClassString() {
			return className + " " + classLevel + " | Хар-ка: " + Localization.LocalizeCharacteristic(spellcastCharacteristic) + " | Спас: " + spellSave + " | Бонус: " + spellAttackBonus;
		}

		public string GetClassString() {
			return className + " " + classLevel;
		}
	}

	public static class Localization {

		private static Dictionary<string, string> _classes = new Dictionary<string, string>(){
			{ "bard", "Бард" },
			{ "barbarian", "Варвар" },
			{ "cleric", "Жрец" },
			{ "druid", "Друид" },
			{ "fighter", "Воин" },
			{ "monk", "Монах" },
			{ "paladin", "Паладин" },
			{ "ranger", "Следопыт" },
			{ "rogue", "Плут" },
			{ "sorcerer", "Чародей" },
			{ "warlock", "Колдун" },
			{ "wizard", "Волшебник" },
			{ "artificer", "Изобретатель" },
		};

		private static Dictionary<string, string> _characteristics = new Dictionary<string, string>(){
			{ "str", "СИЛ" },
			{ "dex", "ЛВК" },
			{ "con", "ТЕЛ" },
			{ "int", "ИНТ" },
			{ "wis", "МДР" },
			{ "cha", "ХАР" },
		};

		//acid, bludgeoning, cold, fire, force, lightning, necrotic, piercing, poison, psychic, radiant, slashing, thunder
		//silver, adamant, spell, nonmagic, magic, healing, temphp
		private static Dictionary<string, string> _damageTypes = new Dictionary<string, string>(){
			{ "acid", "Кислота" },
			{ "cold", "Холод" },
			{ "fire", "Огонь" },
			{ "force", "Сила" },
			{ "lightning", "Молния" },
			{ "necrotic", "Некротический" },
			{ "poison", "Яд" },
			{ "radiant", "Лучистый" },
			{ "thunder", "Гром" },
			{ "psychic", "Психический" },
			{ "bludgeoning", "Дробящий" },
			{ "piercing", "Колющий" },
			{ "slashing", "Рубящий" },
			{ "healing", "Лечение" },
			{ "temphp", "Врем. ХП" },
			{ "silver", "Серебр. физ. урон" },
			{ "adamant", "Адам. физ. урон" },
			{ "spell", "Урон от закл." },
			{ "nonmagic", "Немаг. урон" },
			{ "magic", "Маг. урон" },
		};

		//unconscious, diseased, incapacitated, deafened, grappled, frightened, invisible, charmed, restrained, petrified, poisoned, paralyzed, prone, blinded, exhaustion, stunned
		private static Dictionary<string, string> _statuses = new Dictionary<string, string>(){
			{ "unconscious", "Без сознания" },
			{ "diseased", "Болезнь" },
			{ "incapacitated", "Выход из строя" },
			{ "deafened", "Глухота" },
			{ "grappled", "Захват" },
			{ "frightened", "Испуг" },
			{ "invisible", "Невидимость" },
			{ "charmed", "Обворожение" },
			{ "restrained", "Обездвиженность" },
			{ "petrified", "Окаменение" },
			{ "poisoned", "Отравление" },
			{ "paralyzed", "Паралич" },
			{ "prone", "Распластанность" },
			{ "blinded", "Слепота" },
			{ "exhaustion", "Утомление" },
			{ "stunned", "Шок" },
		};

		private static Dictionary<string, string> _armorFrof = new Dictionary<string, string>(){
			{ "lgt", "Лёгкий доспех" },
			{ "med", "Средний доспех" },
			{ "hvy", "Тяжёлый доспех" },
			{ "shl", "Щит" },

			{ "padded", "Стёганый" },
			{ "leather", "Кожаный" },
			{ "studded", "Проклёпанный кожаный" },

			{ "hide", "Шкурный" },
			{ "chainshirt", "Кольчужная рубаха" },
			{ "scalemail", "Чешуйчатый" },
			{ "breastplate", "Кираса" },
			{ "halfplate", "Полулаты" },

			{ "ringmail", "Колечный" },
			{ "chainmail", "Кольчуга" },
			{ "splint", "Наборный" },
			{ "plate", "Латы" },

			{ "shield", "Щит" },
		};

		private static Dictionary<string, string> _weaponFrof = new Dictionary<string, string>(){
			{ "sim", "Простое оружие" },
			{ "quarterstaff", "Боевой посох" },
			{ "mace", "Булава" },
			{ "club", "Дубинка" },
			{ "dagger", "Кинжал" },
			{ "spear", "Копьё" },
			{ "lighthammer", "Лёгкий молот" },
			{ "javelin", "Метательное копьё" },
			{ "greatclub", "Палица" },
			{ "handaxe", "Ручной топор" },
			{ "sickle", "Серп" },

			{ "lightcrossbow", "Лёгкий арбалет" },
			{ "dart", "Дротик" },
			{ "shortbow", "Короткий лук" },
			{ "sling", "Праща" },

			{ "mar", "Воинское оружие" },
			{ "halberd", "Алебарда" },
			{ "warpick", "Боевая кирка" },
			{ "warhammer", "Боевой молот" },
			{ "battleaxe", "Боевой топор" },
			{ "glaive", "Глефа" },
			{ "greatsword", "Двуручный меч" },
			{ "lance", "Длинное копьё" },
			{ "longsword", "Длинный меч" },
			{ "whip", "Кнут" },
			{ "shortsword", "Короткий меч" },
			{ "maul", "Молот" },
			{ "morningstar", "Моргенштерн" },
			{ "pike", "Пика" },
			{ "rapier", "Рапира" },
			{ "greataxe", "Секира" },
			{ "scimitar", "Скимитар" },
			{ "trident", "Трезубец" },
			{ "flail", "Цеп" },

			{ "handcrossbow", "Ручной арбалет" },
			{ "heavycrossbow", "Тяжёлый арбалет" },
			{ "longbow", "Длинный лук" },
			{ "blowgun", "Духовая трубка" },
			{ "net", "Сеть" },
		};

		private static Dictionary<string, string> _toolProf = new Dictionary<string, string>(){
			{ "thief", "Воровские инструменты" },
			{ "navg", "Инструменты навигатора" },
			{ "disg", "Набор для грима" },
			{ "forg", "Набор для фальсификации" },
			{ "pois", "Инструменты отравителя" },
			{ "herb", "Набор травника" },

			{ "card", "Карты" },
			{ "dice", "Кости" },
			{ "chess", "Драконьи шахматы" },
			// ??? Ставка трех драконов

			{ "alchemist", "Инструменты алхимика" },
			{ "potter", "Инструменты гончара" },
			{ "calligrapher", "Инструменты каллиграфа" },
			{ "mason", "Инструменты каменщика" },
			{ "cartographer", "Инструменты картографа" },
			{ "leatherworker", "Инструменты кожевника" },
			{ "smith", "Инструменты кузнеца" },
			{ "tinker", "Инструменты ремонтника" },
			{ "brewer", "Инструменты пивовара" },
			{ "carpenter", "Инструменты плотника" },
			{ "cook", "Инструменты повара" },
			{ "woodcarver", "Инструменты резчика по дереву" },
			{ "cobbler", "Инструменты сапожника" },
			{ "glassblower", "Инструменты стеклодува" },
			{ "weaver", "Инструменты ткача" },
			{ "painter", "Инструменты художника" },
			{ "jeweler", "Инструменты ювелира" },

			{ "drum", "Барабан" },
			{ "viol", "Виола" },
			{ "bagpipes", "Волынка" },
			{ "horn", "Рожок" },
			{ "lyre", "Лира" },
			{ "lute", "Лютня" },
			{ "panflute", "Флейта" },
			{ "flute", "Флейта" },
			{ "dulcimer", "Цимбалы" },
			{ "shawm", "Шалмей" },
			// ??? Свирель
			
			{ "water", "Водный транспорт" },
			{ "air", "Воздушный транспорт" },
			{ "space", "Космический транспорт" },
			{ "land", "Наземный транспорт" },
		};

		private static Dictionary<string, string> _languageFrof = new Dictionary<string, string>(){
			{ "giant", "Великаний" },
			{ "common", "Общий" },
			{ "gnomish", "Гномий" },
			{ "goblin", "Гоблинский" },
			{ "dwarvish", "Дварфский" },
			{ "orc", "Орочий" },
			{ "halfling", "Полуросликов" },
			{ "elvish", "Эльфийский" },

			{ "primordial", "Первичный" },
			{ "auran", "Ауран" },
			{ "ignan", "Игнан" },
			{ "aquan", "Акван" },
			{ "terran", "Терран" },

			{ "aarakocra", "Язык Ааракокр" },
			{ "abyssal", "Бездны" },
			{ "celestial", "Небесный" },
			{ "deep", "Глубинная Речь" },
			{ "draconic", "Драконий" },
			{ "gith", "Гитский" },
			{ "gnoll", "Гноллий" },
			{ "infernal", "Инфернальный" },
			{ "sylvan", "Сильван" },
			{ "undercommon", "Подземный" },
			{ "cant", "Воровской жаргон" },
			{ "druidic", "Друидический" },
		};

		private static Dictionary<string, string> _misc = new Dictionary<string, string>(){
			{ "sr", "короткий отдых" },
			{ "lr", "долгий отдых" },
			{ "day", "день" },
		};

		public static string LocalizeMisc(string key) {
			Utilities.AddLog("LocalizeMisc: " + key);

			if (string.IsNullOrEmpty(key)) {
				return "";
			}

			return _misc[key];
		}

		public static string LocalizeClass(string key) {
			Utilities.AddLog("LocalizeClass: " + key);

			if (string.IsNullOrEmpty(key)) {
				return "";
			}

			// If need to translate
			if(_classes.ContainsKey(key)){
				return _classes[key];
			}

			return key;
		}

		public static string LocalizeCharacteristic(string key) {
			Utilities.AddLog("LocalizeCharacteristic: " + key);

			if (string.IsNullOrEmpty(key)) {
				return "";
			}

			return _characteristics[key];
		}

		public static string LocalizeDamageType(string key) {
			Utilities.AddLog("LocalizeDamageType: " + key);

			if(string.IsNullOrEmpty(key)){
				return "";	
			}

			return _damageTypes[key];
		}

		public static string LocalizeArmorProficiencie(string key) {
			Utilities.AddLog("LocalizeArmorProficiencie: " + key);

			if (string.IsNullOrEmpty(key)) {
				return "";
			}

			return _armorFrof[key];
		}

		public static string LocalizeWeaponProficiencie(string key) {
			Utilities.AddLog("LocalizeWeaponProficiencie: " + key);

			if (string.IsNullOrEmpty(key)) {
				return "";
			}

			return _weaponFrof[key];
		}

		public static string LocalizeToolProficiencie(string key) {
			Utilities.AddLog("LocalizeToolProficiencie: " + key);

			if (string.IsNullOrEmpty(key)) {
				return "";
			}

			return _toolProf[key];
		}

		public static string LocalizeLanguageProficiencie(string key) {
			Utilities.AddLog("LocalizeLanguageProficiencie: " + key);

			if (string.IsNullOrEmpty(key)) {
				return "";
			}

			return _languageFrof[key];
		}

		public static string LocalizeStatus(string key) {
			Utilities.AddLog("LocalizeStatus: " + key);

			if (string.IsNullOrEmpty(key)) {
				return "";
			}

			return _statuses[key];
		}

		public static string LocalizeProficiencieByType(ProficiencieType profType, string key) {
			string result = "";

			switch(profType){
				case ProficiencieType.Armor:
					return LocalizeArmorProficiencie(key);
				case ProficiencieType.Weapons:
					return LocalizeWeaponProficiencie(key);
				case ProficiencieType.Tools:
					return LocalizeToolProficiencie(key);
				case ProficiencieType.Languages:
					return LocalizeLanguageProficiencie(key);
				case ProficiencieType.Immunities:
					return LocalizeDamageType(key);
				case ProficiencieType.Resistances:
					return LocalizeDamageType(key);
				case ProficiencieType.Vulnerabilities:
					return LocalizeDamageType(key);
				case ProficiencieType.StatusImmunities:
					return LocalizeStatus(key);
			}

			return result;
		}
	}

	public static class Utilities{
		public static bool doSilent = false;

		public static void AddLog(string message, bool ignoreSilent = false) {
			if (!doSilent || ignoreSilent) {
				Console.WriteLine(message);
			}
		}

		public static int CalcModificator(string value) {
			int valueInt = 10;
			int.TryParse(value, out valueInt);

			//Модификатор характеристики: (Характеристика - 10) / 2. Округление в меньшую сторону.
			return (int)Math.Floor((double)((valueInt - 10) / 2));
		}
	}
}