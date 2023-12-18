using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FVTTtoLSSCharConverter {
	class Program {
		private static string appVersion = "v1.0.2";

		private static string sourceJSONsFolder = "SourceJSONs";
		private static string outputJSONsFolder = "OutputJSONs";
		private static string emptyJSONsFolder = "TemplateJSONs";

		private static string empty_lss_name = "empty_lss.json";
		private static string empty_fvtt_name = "empty_fvtt.json";

		private static dynamic empty_lss_object;
		private static dynamic empty_fvtt_object;

		private static SortedList<string, dynamic> fvttCharacters = new SortedList<string, dynamic>();
		private static List<dynamic> lssCharacters = new List<dynamic>();

		private static dynamic newLSSObject;

		private static int dnd240Version = 240000;

		static void Main(string[] args) {
			Utilities.AddLog("App Version: " + appVersion);

			empty_lss_object = JsonHelper.GetJsonRepresentationFromFile(AppContext.BaseDirectory + emptyJSONsFolder + "/" + empty_lss_name);
			Utilities.AddLog("LSS Template Found: " + (empty_lss_object != null));

			empty_fvtt_object = JsonHelper.GetJsonRepresentationFromFile(AppContext.BaseDirectory + emptyJSONsFolder + "/" + empty_fvtt_name);
			Utilities.AddLog("FVTT Template Found: " + (empty_fvtt_object != null));

			// For debug purposes
			//JsonHelper.MakeLSSTemplateFormatted(AppContext.BaseDirectory + emptyJSONsFolder + "/" + empty_lss_name, outputJSONsFolder);

			GetAllSourceJsons();

			Utilities.AddLog("\n[===== Convert " + fvttCharacters.Count + " Foundry Characters to LSS =====]");
			for (int i = 0; i < fvttCharacters.Count; i++) {
				Utilities.AddLog("\n " + (i + 1) + " =====================================");
				Utilities.AddLog("Source FVTT character name: " + fvttCharacters.Values[i].name);

				FVTTtoLSSConvert(fvttCharacters.Keys[i], fvttCharacters.Values[i]);
			}

			//Utilities.AddLog("\n[===== Convert " + lssCharacters.Count + " LSS Characters to Foundry =====]");
			//for (int j = 0; j < lssCharacters.Count; j++) {
			//	Utilities.AddLog("\n " + (j + 1) + " =====================================");
			//	Utilities.AddLog("Source FVTT character name: " + lssCharacters[j].name.value);

			//	LSStoFVTTConvert(lssCharacters[j]);
			//}

			Utilities.AddLog("\n[===== Convert SUCCESS =====]");
			Utilities.AddLog("\nPress any key to close this window...");
			Console.ReadKey();
		}

		private static void GetAllSourceJsons() {
			foreach (string path in Directory.GetFiles(AppContext.BaseDirectory + sourceJSONsFolder, "*.json")) {
				if (path.Contains("vtt")) {
					fvttCharacters.Add(Path.GetFileNameWithoutExtension(path), JsonHelper.GetJsonRepresentationFromFile(path));
				} else {
					lssCharacters.Add(JsonHelper.GetJsonRepresentationFromFile(path));
				}
			}
		}

		private static void FVTTtoLSSConvert(string uniqueName, dynamic fvttObject) {
			// Disable debug outputs
			Utilities.doSilent = true;

			Utilities.AddLog("DND System Version: " + fvttObject._stats.systemVersion.ToString(), true);

			int dndSystemVersion = ConvertVersionNumber(fvttObject._stats.systemVersion.ToString());

			// Find class, subclass and features imported from plutonium
			JToken[] plutoniumItems = JsonHelper.SearchTargetKeysByName(fvttObject, "plutonium");
			Utilities.AddLog("Plutonium items found count: " + plutoniumItems.Length, false);

			bool plutoniumUsed = false;
			foreach (var item in plutoniumItems) {
				dynamic plutoniumItem = (dynamic)item;
				if (plutoniumItem.page != null && !string.IsNullOrEmpty(plutoniumItem.page.ToString())) {
					//Utilities.AddLog("Plutonium item with reference: " + plutoniumItem.page.ToString());
					plutoniumUsed = true;
				}
			}

			Utilities.AddLog("Plutonium used: " + plutoniumUsed, true);

			// Find all class items with hitDice values
			JToken[] hitDiceTokens = JsonHelper.SearchTargetKeysByName(fvttObject, "hitDice");

			//Utilities.AddLog("hitDiceTokens[0]: " + hitDiceTokens[0].ToString());
			//Utilities.AddLog("hitDiceTokens[0].Root.SelectToken(\"items\").[0]: " + hitDiceTokens[0].Root.SelectToken("items")[0].ToString());

			// Foreach hitDice token get class token
			JToken[] classTokens = new JToken[hitDiceTokens.Length];
			for(int i = 0; i < hitDiceTokens.Length; i++){
				classTokens[i] = hitDiceTokens[i].Parent.Parent.Parent.Parent;
				//Utilities.AddLog("classTokens " + i + "\n" + classTokens[i], true);
			}

			//Utilities.doSilent = true;

			Character fvttCharacter = new Character();
			string baseClassName = JsonHelper.SearchTargetKeyByValue(fvttObject, "name", fvttObject.system.details.originalClass.ToString());
			bool isBase = false;
			CharacterClassData cData;
			dynamic tmpClass;
			JToken classSaves = null;
			JToken classHitPoints = null;
			JToken[] classHitPointsTmp;
			int hpIntValue = 0;
			string saveString = "";

			for (int i = 0; i < classTokens.Length; i++) {
				tmpClass = (dynamic)classTokens[i];
				//Utilities.AddLog("Class " + i + " Token:\n" + tmpClass, true);

				string identifier = tmpClass.system.identifier;
				string className = Localization.LocalizeClass(identifier);
				string classLevels = tmpClass.system.levels;
				string hitDice = tmpClass.system.hitDice;
				string spellcastingAbility = tmpClass.system.spellcasting.ability;

				JToken[] subclasses = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "subclass");
				string subclassName = "none";

				foreach(JToken scToken in subclasses){
					//Utilities.AddLog("scToken.Parent: " + scToken.Parent.Parent, true);
					string subclassIdentifier = ((dynamic)scToken.Parent.Parent).system.classIdentifier;

					if(subclassIdentifier == identifier) {
						subclassName = scToken.ToString();
					}
				}

				classHitPointsTmp = JsonHelper.SearchTargetKeysByValue(tmpClass, "value", "HitPoints");
				if(classHitPointsTmp.Length > 0){
					classHitPoints = classHitPointsTmp[0];
				}
				
				if (dndSystemVersion < dnd240Version) {
					classSaves = JsonHelper.SearchTargetKeysByName(tmpClass, "saves")[0];
				} else {
					classSaves = JsonHelper.SearchTargetKeysByValue(tmpClass, "grants", "Trait")[0];
				}

				Utilities.AddLog("========================= Class " + i, true);
				Utilities.AddLog("Name: " + className, true);
				Utilities.AddLog("Identifier: " + identifier, true);
				Utilities.AddLog("Levels: " + classLevels, true);
				Utilities.AddLog("Hit Dice: " + hitDice, true);
				Utilities.AddLog("Spellcasting Ability: " + spellcastingAbility, true);
				Utilities.AddLog("Hit Points: " + classHitPoints, true);
				Utilities.AddLog("Saves: " + classSaves, true);
				Utilities.AddLog("Subclass: " + subclassName, true);

				isBase = className == baseClassName;

				fvttCharacter.CreateNewClass(isBase, identifier, classLevels);
				cData = fvttCharacter.classes[identifier];

				cData.className = className;
				cData.subclassName = subclassName;
				cData.SetHitDice(hitDice);
				fvttCharacter.conMod = Utilities.CalcModificator(fvttObject.system.abilities.con.value.ToString());

				if(classHitPoints != null){
					for (int h = 1; h <= classHitPoints.Count<JToken>(); h++) {
						//Utilities.AddLog("HP VALUE:" + classHitPoints[h.ToString()]);

						if (classHitPoints[h.ToString()].ToString() == "max") {
							classHitPoints[h.ToString()] = cData.hitDiceValue.ToString();
						}

						if (classHitPoints[h.ToString()].ToString() == "avg") {
							classHitPoints[h.ToString()] = (cData.hitDiceValue / 2 + 1).ToString();
						}

						int.TryParse(classHitPoints[h.ToString()].ToString(), out hpIntValue);
						cData.lvlsHP.Add(hpIntValue);
					}
				}
				
				foreach (JToken saveToken in classSaves) {
					if (dndSystemVersion < dnd240Version) {
						saveString = saveToken.ToString();
					} else {
						saveString = saveToken.ToString().Substring(6);
					}

					cData.saves.Add(saveString);

					Utilities.AddLog("Save TOKEN: " + saveString);
				}

				if(!string.IsNullOrEmpty(spellcastingAbility)){
					cData.spellcastCharacteristic = spellcastingAbility;
				}

				if (cData.spellcastCharacteristic != "none") {
					cData.spellSave = 8 + fvttCharacter.GetProficiencyBonus() + Utilities.CalcModificator(fvttObject.system.abilities[cData.spellcastCharacteristic].value.ToString());
					cData.spellAttackBonus = fvttCharacter.GetProficiencyBonus() + Utilities.CalcModificator(fvttObject.system.abilities[cData.spellcastCharacteristic].value.ToString());
				}
				Utilities.AddLog("=====Prepared character Data:\n" + JValue.FromObject(fvttCharacter.classes[identifier]));
			}

			//Utilities.doSilent = true;

			// LSS Common
			newLSSObject = (dynamic) ((JObject) empty_lss_object).DeepClone();
			newLSSObject.name.value = fvttObject.name;
			newLSSObject.hiddenName = uniqueName;
			newLSSObject.inspiration = fvttObject.system.attributes.inspiration;
			newLSSObject.exhaustion = fvttObject.system.attributes.exhaustion;

			// LSS Avatar
			if (((string)fvttObject.img).Contains("http")) {
				newLSSObject.avatar.jpeg = fvttObject.img;
				newLSSObject.avatar.webp = fvttObject.img;
			} else {
				newLSSObject.avatar.jpeg = null;
				newLSSObject.avatar.webp = null;
			}

			// LSS Info
			// Race

			// Common
			string commonRace = fvttObject.system.details.race;

			// Plutonium && dnd < 2.4.0
			string oldRace = JsonHelper.SearchTargetKeyByValue(fvttObject, "value", "system.details.race");

			// dnd >= 2.4.0
			string newRace = JsonHelper.SearchTargetKeyByValue(fvttObject, "name", "race");

			string race = "";
			if(oldRace != "none") {
				race = oldRace;
			}else if(newRace != "none"){
				race = newRace;	
			}else{
				race = commonRace;
			}

			newLSSObject.info.race.value = race;

			// Background
			string background = JsonHelper.SearchTargetKeyByValue(fvttObject, "name", "background");
			if (background == "none") {
				background = fvttObject.system.details.background;
			}
			newLSSObject.info.background.value = background;

			newLSSObject.info.alignment.value = fvttObject.system.details.alignment;
			newLSSObject.info.charClass.value = fvttCharacter.GetClassesString();
			newLSSObject.info.level.value = fvttCharacter.GetTotalLevel();
			newLSSObject.info.experience.value = fvttObject.system.details.xp.value;

			// LSS Stats
			newLSSObject.stats.str.score = FindMaximumStatChange(fvttObject, "str");
			newLSSObject.stats.dex.score = FindMaximumStatChange(fvttObject, "dex");
			newLSSObject.stats.con.score = FindMaximumStatChange(fvttObject, "con");
			newLSSObject.stats["int"].score = FindMaximumStatChange(fvttObject, "int");
			newLSSObject.stats.wis.score = FindMaximumStatChange(fvttObject, "wis");
			newLSSObject.stats.cha.score = FindMaximumStatChange(fvttObject, "cha");


			//Utilities.doSilent = true;
			// LSS Skills proficiency
			newLSSObject.skills.acrobatics.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "acr");
			newLSSObject.skills.investigation.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "inv");
			newLSSObject.skills.athletics.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "ath");
			newLSSObject.skills.perception.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "prc");
			newLSSObject.skills.survival.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "sur");
			newLSSObject.skills.performance.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "prf");
			newLSSObject.skills.intimidation.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "itm");
			newLSSObject.skills.history.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "his");
			newLSSObject.skills["sleight of hand"].isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "slt");
			newLSSObject.skills.arcana.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "arc");
			newLSSObject.skills.medicine.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "med");
			newLSSObject.skills.deception.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "dec");
			newLSSObject.skills.nature.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "nat");
			newLSSObject.skills.insight.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "ins");
			newLSSObject.skills.religion.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "rel");
			newLSSObject.skills.stealth.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "ste");
			newLSSObject.skills.persuasion.isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "per");
			newLSSObject.skills["animal handling"].isProf = JsonHelper.FVTT_GetSkillProf(fvttObject, "ani");

			// LSS Skills bonus
			newLSSObject.skills.acrobatics.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "acr");
			newLSSObject.skills.investigation.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "inv");
			newLSSObject.skills.athletics.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "ath");
			newLSSObject.skills.perception.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "prc");
			newLSSObject.skills.survival.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "sur");
			newLSSObject.skills.performance.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "prf");
			newLSSObject.skills.intimidation.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "itm");
			newLSSObject.skills.history.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "his");
			newLSSObject.skills["sleight of hand"].bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "slt");
			newLSSObject.skills.arcana.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "arc");
			newLSSObject.skills.medicine.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "med");
			newLSSObject.skills.deception.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "dec");
			newLSSObject.skills.nature.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "nat");
			newLSSObject.skills.insight.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "ins");
			newLSSObject.skills.religion.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "rel");
			newLSSObject.skills.stealth.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "ste");
			newLSSObject.skills.persuasion.bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "per");
			newLSSObject.skills["animal handling"].bonus = JsonHelper.FVTT_GetSkillBonus(fvttObject, "ani");

			// LSS Coins
			newLSSObject.coins.pp.value = fvttObject.system.currency.pp;
			newLSSObject.coins.gp.value = fvttObject.system.currency.gp;
			newLSSObject.coins.ep.value = fvttObject.system.currency.ep;
			newLSSObject.coins.sp.value = fvttObject.system.currency.sp;
			newLSSObject.coins.cp.value = fvttObject.system.currency.cp;

			// LSS Text
			newLSSObject.text.ideals.size = 12;
			newLSSObject.text.bonds.size = 12;
			newLSSObject.text.flaws.size = 12;
			newLSSObject.text.personality.size = 12;

			newLSSObject.text.ideals.value.data = StripHTML(fvttObject.system.details.ideal.ToString());
			newLSSObject.text.bonds.value.data = StripHTML(fvttObject.system.details.bond.ToString());
			newLSSObject.text.flaws.value.data = StripHTML(fvttObject.system.details.flaw.ToString());
			newLSSObject.text.personality.value.data = StripHTML(fvttObject.system.details.trait.ToString());

			// LSS Notes
			newLSSObject.text["notes-1"].size = 12;
			newLSSObject.text["notes-2"].size = 12;
			newLSSObject.text["notes-3"].size = 12;
			newLSSObject.text["notes-4"].size = 14;
			newLSSObject.text["notes-5"].size = 14;
			newLSSObject.text["notes-6"].size = 14;

			newLSSObject.text["notes-1"].value.data = "";
			newLSSObject.text["notes-2"].value.data = "";
			newLSSObject.text["notes-3"].value.data = "";
			newLSSObject.text["notes-4"].value.data = "";
			newLSSObject.text["notes-5"].value.data = "";
			newLSSObject.text["notes-6"].value.data = "";

			// LSS Appearance
			newLSSObject.text.background.size = 12;
			newLSSObject.text.background.customLabel = "Внешность";
			newLSSObject.text.background.value.data = fvttObject.system.details.appearance.ToString();//StripHTML(fvttObject.system.details.appearance.ToString());

			// LSS Background
			newLSSObject.text["notes-3"].customLabel = "ПРЕДЫСТОРИЯ ПЕРСОНАЖА";
			newLSSObject.text["notes-3"].value.data = fvttObject.system.details.biography.value.ToString();//StripHTML(fvttObject.system.details.biography.value.ToString());

			List<CharacterClassData> casterClasses = fvttCharacter.GetAllClassesList();//.GetCastClassesList();
			
			foreach (var cData3 in casterClasses) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-1", (cData3.HasSpellAbility) ? cData3.GetCasterClassString() : cData3.GetClassString());
				if(cData3.HasSubclass){
					newLSSObject = AddLineToNotes(newLSSObject, "notes-1", "<b>Архетип:</b> " + cData3.subclassName);
				}

				if (cData3.IsWarlock) {
					newLSSObject = AddLineToNotes(newLSSObject, "notes-1", cData3.GetWarlockCastDataString());
				}
			}

			// LSS SubInfo, if tidy5e charlist used!
			if (fvttObject.flags["tidy5e-sheet"] != null) {
				newLSSObject.subInfo.age.value = fvttObject.flags["tidy5e-sheet"].age;
				newLSSObject.subInfo.height.value = fvttObject.flags["tidy5e-sheet"].height;
				newLSSObject.subInfo.weight.value = fvttObject.flags["tidy5e-sheet"].weight;
				newLSSObject.subInfo.eyes.value = fvttObject.flags["tidy5e-sheet"].eyes;
				newLSSObject.subInfo.skin.value = fvttObject.flags["tidy5e-sheet"].skin;
				newLSSObject.subInfo.hair.value = fvttObject.flags["tidy5e-sheet"].hair;

				if (fvttObject.flags["tidy5e-sheet"].notes != null) {
					newLSSObject = AddLineToNotes(newLSSObject, "notes-1", fvttObject.flags["tidy5e-sheet"].notes.value.ToString());
				}

				if(fvttObject.flags["tidy5e-sheet"].notes3.value != null){
					newLSSObject.text.quests.size = 12;
					newLSSObject = AddLineToNotes(newLSSObject, "quests", fvttObject.flags["tidy5e-sheet"].notes3.value.ToString(), TextType.Normal, TextSpace.Before);
				}
				if (fvttObject.flags["tidy5e-sheet"].notes1.value != null) {
					newLSSObject.text.allies.size = 12;
					newLSSObject = AddLineToNotes(newLSSObject, "allies", fvttObject.flags["tidy5e-sheet"].notes1.value.ToString());
				}
				if (fvttObject.flags["tidy5e-sheet"].notes2.value != null) {
					newLSSObject = AddLineToNotes(newLSSObject, "allies", fvttObject.flags["tidy5e-sheet"].notes2.value.ToString(), TextType.Normal, TextSpace.Before);
				}
				if (fvttObject.flags["tidy5e-sheet"].notes4.value != null) {
					newLSSObject = AddLineToNotes(newLSSObject, "notes-6", fvttObject.flags["tidy5e-sheet"].notes4.value.ToString());
				}
			}else{
				newLSSObject.subInfo.age.value = "";
				newLSSObject.subInfo.height.value = "";
				newLSSObject.subInfo.weight.value = "";
				newLSSObject.subInfo.eyes.value = "";
				newLSSObject.subInfo.skin.value = "";
				newLSSObject.subInfo.hair.value = "";
			}

			// LSS Vitality
			newLSSObject.vitality["hp-current"].value = fvttObject.system.attributes.hp.value;

			int calculatedMaxHP = fvttCharacter.GetMaxHP();
			int fixedMaxHP = (fvttObject.system.attributes.hp.max != null) ? fvttObject.system.attributes.hp.max : fvttObject.system.attributes.hp.value;
			newLSSObject.vitality["hp-max"].value = (calculatedMaxHP > fixedMaxHP) ? calculatedMaxHP : fixedMaxHP;
			Utilities.AddLog("Calculated Max HP: " + calculatedMaxHP + " | Fixed Max HP: " + fixedMaxHP, true);

			newLSSObject.vitality["hit-die"].value = fvttCharacter.GetHpDiceString();
			//Utilities.AddLog("hit-die: " + newLSSObject.vitality["hit-die"].value);

			Dictionary<string, int> hpDicesMulti = fvttCharacter.GetHpDiceMulti();
			newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "<b>Кости хитов:</b>");
			foreach (var hpDice in hpDicesMulti) {
				newLSSObject.vitality["hp-dice-multi"][hpDice.Key].max = hpDice.Value;
				newLSSObject.vitality["hp-dice-multi"][hpDice.Key].current = hpDice.Value;

				if(hpDice.Value != 0){
					newLSSObject = AddLineToNotes(newLSSObject, "notes-4", hpDice.Key + ": _________\\" + hpDice.Value);
				}

				//Utilities.AddLog(hpDice.Key + " | " + hpDice.Value);
			}

			newLSSObject.vitality["hp-dice-current"].value = hpDicesMulti.Count;
			newLSSObject.vitality["hp-max-bonus"].value = 0;


			JToken[] playerRace = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "race");

			// Movement
			dynamic movementSource = fvttObject.system.attributes.movement;

			if (playerRace.Length > 0) {
				//Utilities.AddLog("Player Race:  " + playerRace[0].Parent.Parent, true);

				if (dndSystemVersion >= dnd240Version) {
					movementSource = ((dynamic)playerRace[0].Parent.Parent).system.movement;
				}
			}

			newLSSObject.vitality.speed.value = movementSource.walk;

			newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Скорость перемещения:", TextType.Bold, TextSpace.Before);
			if (movementSource.walk != null) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Хотьба: " + movementSource.walk);
			}
			if (movementSource.fly != null) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Полёт: " + movementSource.fly);
			}
			if (movementSource.swim != null) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Плавание: " + movementSource.swim);
			}
			if (movementSource.burrow != null) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Копание: " + movementSource.burrow);
			}

			// Senses
			string darkvision = "";
			string blindsight = "";
			string tremorsense = "";
			string truesight = "";

			dynamic sensesSource = fvttObject.system.attributes.senses;

			if (playerRace.Length > 0) {
				//Utilities.AddLog("Player Race:  " + playerRace[0].Parent.Parent, true);

				if (dndSystemVersion >= dnd240Version) {
					sensesSource = ((dynamic)playerRace[0].Parent.Parent).system.senses;
				}
			}

			if (sensesSource.darkvision != null) {
				darkvision = sensesSource.darkvision;
			}
			if (sensesSource.blindsight != null) {
				blindsight = sensesSource.blindsight;
			}
			if (sensesSource.tremorsense != null) {
				tremorsense = sensesSource.tremorsense;
			}
			if (sensesSource.truesight != null) {
				truesight = sensesSource.truesight;
			}

			if(!string.IsNullOrEmpty(darkvision) || !string.IsNullOrEmpty(blindsight) || !string.IsNullOrEmpty(tremorsense) || !string.IsNullOrEmpty(truesight)) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Способности:", TextType.Bold, TextSpace.Before);
			}

			if (!string.IsNullOrEmpty(darkvision)) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Тёмное зрение: " + darkvision);
			}
			if (!string.IsNullOrEmpty(blindsight)) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Слепое зрение: " + blindsight);
			}
			if (!string.IsNullOrEmpty(tremorsense)) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Чувство вибрации: " + tremorsense);
			}
			if (!string.IsNullOrEmpty(truesight)) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Истинное зрение: " + truesight);
			}

			newLSSObject.vitality.ac.value = "";
			newLSSObject.vitality.initiative.value = Utilities.CalcModificator(fvttObject.system.abilities.dex.value.ToString());

			newLSSObject.vitality.deathFails = fvttObject.system.attributes.death.failure;
			newLSSObject.vitality.deathSuccesses = fvttObject.system.attributes.death.success;

			// LSS Saves
			foreach (var cData2 in fvttCharacter.classes.Values) {
				if(cData2.baseClass){ 
					foreach(string save in cData2.saves){
						Utilities.AddLog("Add Save from base blass: " + save);
						newLSSObject.saves[save].isProf = true;
					}
				}else{
					continue;	
				}
			}
			//Utilities.AddLog(newLSSObject.saves.ToString());

			// LSS casterClass
			CharacterClassData casterData = fvttCharacter.FindMainCasterClass(true);
			if(casterData != null){
				newLSSObject.casterClass.value = casterData.className;
				//newLSSObject.casterClass.value = casterData.classNameOriginal;

				newLSSObject.spellsInfo["base"].code = casterData.spellcastCharacteristic;
				newLSSObject.spellsInfo.save.value = casterData.spellSave;
				newLSSObject.spellsInfo.mod.value = casterData.spellAttackBonus;

				Utilities.AddLog("Main Caster Class: " + newLSSObject.casterClass.value);
				Utilities.AddLog("Spellcast Characteristic: " + newLSSObject.spellsInfo["base"].code);
				Utilities.AddLog("spellsInfo.save: " + newLSSObject.spellsInfo.save.value);
				Utilities.AddLog("spellsInfo.mod: " + newLSSObject.spellsInfo.mod.value);
			}

			// LSS SpellSlots
			newLSSObject.spells["slots-1"].value = fvttObject.system.spells.spell1.value;
			newLSSObject.spells["slots-2"].value = fvttObject.system.spells.spell2.value;
			newLSSObject.spells["slots-3"].value = fvttObject.system.spells.spell3.value;
			newLSSObject.spells["slots-4"].value = fvttObject.system.spells.spell4.value;
			newLSSObject.spells["slots-5"].value = fvttObject.system.spells.spell5.value;
			newLSSObject.spells["slots-6"].value = fvttObject.system.spells.spell6.value;
			newLSSObject.spells["slots-7"].value = fvttObject.system.spells.spell7.value;
			newLSSObject.spells["slots-8"].value = fvttObject.system.spells.spell8.value;
			newLSSObject.spells["slots-9"].value = fvttObject.system.spells.spell9.value;

			foreach (var cData3 in casterClasses) {
				if(cData3.IsWarlock){
					int currentSpellSlotsCount = 0;
					int.TryParse(newLSSObject.spells["slots-" + cData3.GetWarlockSpellSlotsLvl()].value.ToString(), out currentSpellSlotsCount);
					newLSSObject.spells["slots-" + cData3.GetWarlockSpellSlotsLvl()].value = currentSpellSlotsCount + cData3.GetWarlockSpellSlotsCount();

					break;
				}
			}

			newLSSObject.text["spells-level-0"].value.data = "";
			newLSSObject.text["spells-level-1"].value.data = "";
			newLSSObject.text["spells-level-2"].value.data = "";
			newLSSObject.text["spells-level-3"].value.data = "";
			newLSSObject.text["spells-level-4"].value.data = "";
			newLSSObject.text["spells-level-5"].value.data = "";
			newLSSObject.text["spells-level-6"].value.data = "";
			newLSSObject.text["spells-level-7"].value.data = "";
			newLSSObject.text["spells-level-8"].value.data = "";
			newLSSObject.text["spells-level-9"].value.data = "";

			// LSS Fill items, feats, spells
			newLSSObject.text["attacks"].size = 12;
			newLSSObject.text["equipment"].size = 12;
			newLSSObject.text["features"].size = 12;
			newLSSObject.text["items"].size = 12;
			newLSSObject.text["traits"].size = 12;
			newLSSObject.text["prof"].size = 12;

			newLSSObject.text["attacks"].value.data = "";
			newLSSObject.text["equipment"].value.data = "";
			newLSSObject.text["features"].value.data = "";
			newLSSObject.text["items"].value.data = "";
			newLSSObject.text["items"].customLabel = "Предметы";
			newLSSObject.text["traits"].value.data = "";
			newLSSObject.text["prof"].value.data = "";

			Utilities.AddLog("====================================\n");
			//Utilities.AddLog(((JToken)fvttObject.items).ToString());

			//JToken[] test = ((JArray)fvttObject.items).ToArray<JToken>();
			//Utilities.AddLog(((JToken)fvttObject.items).ToString());

			JToken[] characterItemsWeapons = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "weapon");
			JToken[] characterItemsEquipment = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "equipment");
			JToken[] characterItemsBackpack = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "backpack");
			JToken[] characterItemsConsumable = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "consumable");
			JToken[] characterItemsLoot = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "loot");
			JToken[] characterItemsTool = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "tool");
			JToken[] characterItemsFeats = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "feat");
			JToken[] characterItemsSpells = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "spell");

			dynamic tmpJson;
			Utilities.AddLog("\n==========================================Character Weapons:");
			foreach (var weapon in characterItemsWeapons) {
				//Utilities.AddLog(item.ToString());

				tmpJson = (dynamic)weapon.Parent.Parent;
				//Utilities.AddLog(tmpJson.ToString());

				string itemName = SimplifyName(weapon.ToString());
				string weaponDmgFormula = "";
				string weaponDmgType = "";
				JToken weaponDmgData = null;

				try {
					weaponDmgData = tmpJson.system.damage.parts[0];
					weaponDmgType = Localization.LocalizeDamageType(weaponDmgData.Values<string>().Last().ToString());
				} catch (Exception ex) { }

				if (weaponDmgData != null){
					//Utilities.AddLog(weaponDmgData.Values<string>().First().ToString());
					weaponDmgFormula = weaponDmgData.Values<string>().First().ToString();
					weaponDmgFormula = weaponDmgFormula.Replace(" ", "");
					weaponDmgFormula = weaponDmgFormula.Remove(weaponDmgFormula.Length - 5);
					weaponDmgFormula += "+" + GetWeaponAttackBonus(tmpJson, fvttObject);
				}

				string resultLine = itemName;
				if(!string.IsNullOrEmpty(weaponDmgFormula)){ 
					resultLine += " <b>" + weaponDmgFormula + "</b>";
				}
				if (weaponDmgFormula.Contains('(')) {
					weaponDmgFormula = "";
				}
				if (!string.IsNullOrEmpty(weaponDmgType)) {
					resultLine += " [" + weaponDmgType + "]";
				}

				Utilities.AddLog(resultLine);

				newLSSObject = AddLineToNotes(newLSSObject, "attacks", resultLine);
				newLSSObject = AddLineToNotes(newLSSObject, "equipment", itemName + GetItemQuantity(tmpJson));
			}

			Utilities.AddLog("==========================================Character Equipment:\n");
			foreach (var item in characterItemsEquipment) {
				//Utilities.AddLog(item.ToString());
				tmpJson = (dynamic)item.Parent.Parent;

				// exclude effects
				if (tmpJson["type"] == null) continue;

				string itemName = SimplifyName(item.ToString());
				newLSSObject = AddLineToNotes(newLSSObject, "equipment", itemName + GetItemQuantity(tmpJson));
			}

			Utilities.AddLog("==========================================Character Backpack:\n");
			foreach (var item in characterItemsBackpack) {
				//Utilities.AddLog(item.ToString());
				tmpJson = (dynamic)item.Parent.Parent;

				string itemName = SimplifyName(item.ToString());
				newLSSObject = AddLineToNotes(newLSSObject, "items", itemName + GetItemQuantity(tmpJson));
			}

			Utilities.AddLog("==========================================Character Consumable:\n");
			foreach (var item in characterItemsConsumable) {
				//Utilities.AddLog(item.ToString());
				tmpJson = (dynamic)item.Parent.Parent;

				string itemName = SimplifyName(item.ToString());
				newLSSObject = AddLineToNotes(newLSSObject, "items", itemName + GetItemQuantity(tmpJson));
			}

			Utilities.AddLog("==========================================Character Tool:\n");
			foreach (var item in characterItemsTool) {
				//Utilities.AddLog(item.ToString());
				tmpJson = (dynamic)item.Parent.Parent;

				string itemName = SimplifyName(item.ToString());
				newLSSObject = AddLineToNotes(newLSSObject, "items", itemName + GetItemQuantity(tmpJson));
			}

			Utilities.AddLog("==========================================Character Loot:\n");
			foreach (var item in characterItemsLoot) {
				//Utilities.AddLog(item.ToString());
				tmpJson = (dynamic)item.Parent.Parent;

				string itemName = SimplifyName(item.ToString());
				newLSSObject = AddLineToNotes(newLSSObject, "items", itemName + GetItemQuantity(tmpJson));
			}

			Utilities.AddLog("==========================================Character Feats:\n");
			foreach (var feat in characterItemsFeats) {
				if(feat != null){
					//Utilities.AddLog(feat.ToString());
					tmpJson = (dynamic)feat.Parent.Parent;
					
					// exclude effects
					if (tmpJson["type"] == null) continue;

					//Utilities.AddLog(tmpJson.ToString());
					string featName = SimplifyName(feat.ToString());

					//string description = "\n";
					
					//if (tmpJson.system != null) {
					//	description += StripHTML(tmpJson.system.description.value.ToString());
					//}else if(!string.IsNullOrEmpty(tmpJson.system.description.ToString())) {
					//	description += StripHTML(tmpJson.system.description.ToString());
					//}

					if(tmpJson.system != null && !string.IsNullOrEmpty(tmpJson.system.uses.max.ToString())){
						featName = "<b>" + featName + ":</b> " + GetUsesString(tmpJson.system.uses);
						newLSSObject = AddLineToNotes(newLSSObject, "traits", featName);
					}else{
						newLSSObject = AddLineToNotes(newLSSObject, "features", featName);
						//newLSSObject = AddLineToNotes(newLSSObject, "features", featName + description + "\n");
					}
				}
			}

			//Utilities.doSilent = false;

			Utilities.AddLog("==========================================Character Spells:\n");
			foreach (var spell in characterItemsSpells) {
				tmpJson = (dynamic)spell.Parent.Parent;
				//Utilities.AddLog(spell.ToString());

				string itemName = SimplifyName(spell.ToString());
				bool isPrepared = false;
				bool isPact = (tmpJson.system.preparation.mode.ToString() == "pact");

				isPrepared = ((bool)tmpJson.system.preparation.prepared|| tmpJson.system.preparation.mode.ToString() == "pact");

				//Utilities.AddLog(itemName + " is pact: " + isPact);
				//Utilities.AddLog("prepared: " + isPrepared);

				if (isPrepared){
					string resultLine = "";
					string spellSaveChar = "";

					if (!string.IsNullOrEmpty(tmpJson.system.save.ability.ToString())) {
						spellSaveChar = " | Спас: " + Localization.LocalizeCharacteristic(tmpJson.system.save.ability.ToString());
					}

					if (((JArray)tmpJson.system.damage.parts).Count > 0){
						JToken spellDmgData = tmpJson.system.damage.parts[0];

						string spellDmgFormula = spellDmgData.Values<string>().First().ToString();
						string spellDmgType = Localization.LocalizeDamageType(spellDmgData.Values<string>().Last().ToString());

						if(spellDmgFormula.Contains('@')) {
							spellDmgFormula = spellDmgFormula.Replace(" ", "");
							spellDmgFormula = spellDmgFormula.Remove(spellDmgFormula.Length - 5);
							spellDmgFormula += "+" + GetSpellModBonus(newLSSObject.spellsInfo["base"].code.ToString(), fvttObject);
						}

						spellDmgFormula = "<b>" + spellDmgFormula + "</b>";

						if (spellDmgFormula.Contains('(')){
							spellDmgFormula = "";
						}

						resultLine = itemName + " " + spellDmgFormula + " [" + spellDmgType + "]";

						newLSSObject = AddLineToNotes(newLSSObject, "attacks", resultLine);
					} else{
						resultLine = itemName + spellSaveChar;

						if (isPact) {
							resultLine = "[Пакт] " + resultLine;
						}
					}

					Utilities.AddLog(resultLine);

					newLSSObject = AddLineToNotes(newLSSObject, "spells-level-" + tmpJson.system.level.ToString(), resultLine);
				}
			}

			//Utilities.doSilent = true;

			// LSS Proficiencies
			Utilities.AddLog("\n==========================================Character Proficiencies:");
			string tmpProfString = "";

			// WEAPON
			Utilities.AddLog("\n==================Weapon Proficiencies:");
			tmpProfString = GetProficienciesString(ProficiencieType.Weapons, JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.traits.weaponProf.value"), fvttObject.system.traits.weaponProf.value);
			if (!string.IsNullOrEmpty(tmpProfString)) {
				newLSSObject = AddLineToNotes(newLSSObject, "prof", "<b>Оружие:</b> " + tmpProfString, TextType.Normal);
			}

			// ARMOR
			Utilities.AddLog("\n==================Armor Proficiencies:");
			tmpProfString = GetProficienciesString(ProficiencieType.Armor, JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.traits.armorProf.value"), fvttObject.system.traits.armorProf.value);
			if (string.IsNullOrEmpty(tmpProfString)){
				tmpProfString = "Нет";
			}
			newLSSObject = AddLineToNotes(newLSSObject, "prof", "<b>Броня:</b> " + tmpProfString, TextType.Normal, TextSpace.Before);

			// TOOLS
			Utilities.AddLog("\n==================Tool Proficiencies:");
			JObject tools = (JObject)fvttObject.system.tools;
			tmpProfString = GetProficienciesString(ProficiencieType.Tools, JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.traits.toolProf.value"), tools.Properties().Select(p => p.Name).ToList());
			if (string.IsNullOrEmpty(tmpProfString)) {
				tmpProfString = "Нет";
			}
			newLSSObject = AddLineToNotes(newLSSObject, "prof", "<b>Инструменты:</b> " + tmpProfString, TextType.Normal, TextSpace.Before);

			// LANGUAGES
			Utilities.AddLog("\n==================Languages Proficiencies:");
			tmpProfString = GetProficienciesString(ProficiencieType.Languages, JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.traits.languages.value"), fvttObject.system.traits.languages.value);
			if (!string.IsNullOrEmpty(tmpProfString)) {
				newLSSObject = AddLineToNotes(newLSSObject, "prof", "<b>Языки:</b> " + tmpProfString, TextType.Normal, TextSpace.Before);
			}

			// IMMUNITIES
			Utilities.AddLog("\n==================Damage Immunities:");
			tmpProfString = GetProficienciesString(ProficiencieType.Immunities, JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.traits.di.value"), fvttObject.system.traits.di.value);
			if (!string.IsNullOrEmpty(tmpProfString)) {
				newLSSObject = AddLineToNotes(newLSSObject, "prof", "<b>Иммунитет:</b> " + tmpProfString, TextType.Normal, TextSpace.Before);
			}

			// Resistances
			Utilities.AddLog("\n==================Damage Resistances:");
			tmpProfString = GetProficienciesString(ProficiencieType.Resistances, JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.traits.dr.value"), fvttObject.system.traits.dr.value);
			if (!string.IsNullOrEmpty(tmpProfString)) {
				newLSSObject = AddLineToNotes(newLSSObject, "prof", "<b>Сопротивление:</b> " + tmpProfString, TextType.Normal, TextSpace.Before);
			}

			// Vulnerabilities
			Utilities.AddLog("\n==================Damage Vulnerabilities:");
			tmpProfString = GetProficienciesString(ProficiencieType.Vulnerabilities, JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.traits.dv.value"), fvttObject.system.traits.dv.value);
			if (!string.IsNullOrEmpty(tmpProfString)) {
				newLSSObject = AddLineToNotes(newLSSObject, "prof", "<b>Уязвимость:</b> " + tmpProfString, TextType.Normal, TextSpace.Before);
			}

			// Statuses IMMUNITIES
			Utilities.AddLog("\n==================Damage Vulnerabilities:");
			tmpProfString = GetProficienciesString(ProficiencieType.StatusImmunities, JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.traits.ci.value"), fvttObject.system.traits.ci.value);
			if (!string.IsNullOrEmpty(tmpProfString)) {
				newLSSObject = AddLineToNotes(newLSSObject, "prof", "<b>Иммунитет к состоянию:</b> " + tmpProfString, TextType.Normal, TextSpace.Before);
			}


			// Enable debug outputs
			Utilities.doSilent = false;

			// ========================================
			// Save converted json
			JsonFileUtils.SimpleWrite(newLSSObject, AppContext.BaseDirectory + outputJSONsFolder + "/actor_" + newLSSObject.name.value + "_lss_converted.json");
			//JsonHelper.MakeLSSTemplateFormatted(AppContext.BaseDirectory + emptyJSONsFolder + "/" + empty_lss_name, outputJSONsFolder);
		}

		public static int ConvertVersionNumber(string version){
			string[] versionArrayString = version.Split('.');
			int[] versionArrayInt = new int[versionArrayString.Length];

			int resultInt = 0;

			for(int i = 0; i < versionArrayString.Length; i++){
				int.TryParse(versionArrayString[i], out versionArrayInt[i]);
				resultInt += (int)(versionArrayInt[i] * 100000/ Math.Pow(10, i));
			}

			return resultInt;
		}

		public static string GetUsesString(dynamic usesToken){
			string result = "";

			string value = usesToken.max.ToString();
			if (value.Contains('@')) {
				value = usesToken.value.ToString();
			}

			result = GetUsesCount(value) + " за " + Localization.LocalizeMisc(usesToken.per.ToString());

			return result;
		}

		public static string GetUsesCount(string maxValue){
			if(maxValue == "@prof"){
				return newLSSObject.proficiency.ToString();
			}

			return maxValue;
		}

		private static List<JToken> tmpList = new List<JToken>();
		private static dynamic tmpToken;
		public static string GetProficienciesString(ProficiencieType profType, JToken[] itemsArray1, dynamic itemsArray2) {
			tmpList.Clear();

			foreach (var prof in itemsArray1) {
				// exclude Absorb Elements effect
				if(profType == ProficiencieType.Immunities || profType == ProficiencieType.Resistances || profType == ProficiencieType.Vulnerabilities){
					tmpToken = (dynamic)prof.Parent.Parent.Parent.Parent.Parent;
					if (tmpToken["type"] == null) continue;
				}

				if (tmpList.Contains(prof)) continue;
				tmpList.Add(prof);
			}

			foreach (var prof in itemsArray2) {
				if (tmpList.Contains(prof))	continue;
				tmpList.Add(prof);
			}

			string resultStr = "";
			foreach (JToken token in tmpList) {
				resultStr += Localization.LocalizeProficiencieByType(profType, token.ToString());
				resultStr += (tmpList.IndexOf(token) < tmpList.Count - 1) ? ", " : "";
			}
			resultStr.ToLower();

			return resultStr;
		}

		public static string StripHTML(string input) {
			return Regex.Replace(input, "<.*?>", String.Empty);
		}

		// BUG!!!
		public static int FindMaximumStatChange(dynamic fvttObject, string key) {
			JToken[] changesFound = JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "system.abilities." + key + ".value");

			int fvttValue = 0;
			int.TryParse(fvttObject.system.abilities[key].value.ToString(), out fvttValue);

			int result = 0;
			int tmpVal = 0;
			if (changesFound.Length > 0) {
				foreach (var item in changesFound) {
					int.TryParse(item.ToString(), out tmpVal);

					if (result < tmpVal)
						result = tmpVal;
				}
			}

			Utilities.AddLog("\n==================STAT COMPARSION:");
			Utilities.AddLog("fvttValue: " + fvttValue);
			Utilities.AddLog("result: " + result);

			if (fvttValue > result){
				return fvttValue;
			}

			return result;
		}

		public static string GetSpellModBonus(string characteristic, dynamic fvttObject) {
			int charMod = Utilities.CalcModificator(fvttObject.system.abilities[characteristic].value.ToString());

			return charMod.ToString();
		}

		public static string GetWeaponAttackBonus(dynamic jsonWeapon, dynamic fvttObject){
			int strMod = Utilities.CalcModificator(fvttObject.system.abilities["str"].value.ToString());
			int dexMod = Utilities.CalcModificator(fvttObject.system.abilities["dex"].value.ToString());

			// Have attack bonus?
			int attackBonus = 0;
			string weaponBonus = jsonWeapon.system.attackBonus.ToString();
			if (!string.IsNullOrEmpty(weaponBonus)) {
				int.TryParse(weaponBonus, out attackBonus);
			}

			// Have Reach property?
			bool haveReach = jsonWeapon.system.properties.rch;

			// Have Finesse property?
			bool isFinesse = jsonWeapon.system.properties.fin;
			if (isFinesse && strMod < dexMod) {
				return (dexMod + attackBonus).ToString();
			}

			// Is Range Weapon?			
			int minRangeValue = 0;
			int.TryParse(jsonWeapon.system.range.value.ToString(), out minRangeValue);

			if(minRangeValue > 5 && !haveReach) {
				return (dexMod + attackBonus).ToString();
			}

			return (strMod + attackBonus).ToString();
		}

		public static string SimplifyName(string inputStr) {
			string result = inputStr;

			if (result.IndexOf('/') != -1) {
				result = result.Remove(result.IndexOf('/'));
				result = result.TrimEnd();
			}

			if(result.Contains("(")){
				result = result.Remove(result.IndexOf('('));
				result = result.TrimEnd();
			}

			if (result.Contains("(")) {
				result = result.Remove(result.IndexOf('('));
				result = result.TrimEnd();
			}

			return result;
		}

		public static string GetItemQuantity(dynamic jsonItem){
			string itemQuantity = "1";

			try {itemQuantity = jsonItem.system.quantity.ToString();}
			catch (Exception ex) {}

			if (!string.IsNullOrEmpty(itemQuantity) && itemQuantity != "1") {
				itemQuantity = " x" + itemQuantity;
			} else {
				itemQuantity = "";
			}

			return itemQuantity;
		}

		private static dynamic AddLineToNotes(dynamic lssObject, string notesKey, string message, TextType textType = TextType.Normal, TextSpace addSpace = TextSpace.None) {
			Utilities.AddLog("AddLineToNotes " + notesKey + ": " + message);

			string notesText = lssObject.text[notesKey].value.data.ToString();

			if(addSpace == TextSpace.Before){
				notesText += "<p></p>";
			}

			string resultMessage = message;

			if (textType == TextType.Bold) {
				resultMessage = "<b>" + message + "</b>";
			}else if(textType == TextType.Italic){
				resultMessage = "<i>" + message + "</i>";
			}

			notesText += "<p>" + resultMessage + "</p>";

			if (addSpace == TextSpace.After) {
				notesText += "<p></p>";
			}

			lssObject.text[notesKey].value.data = notesText;

			return lssObject;
		}

		private static void LSStoFVTTConvert(dynamic lssObject) {
			dynamic newFVTTObject = empty_fvtt_object;
			newFVTTObject.name = lssObject.name.value;

			JsonFileUtils.SimpleWrite(newFVTTObject, AppContext.BaseDirectory + outputJSONsFolder + "/actor_" + newFVTTObject.name + "_fvtt_converted.json");
		}
	}

	public enum ProficiencieType {
		Armor,
		Weapons,
		Tools,
		Languages,
		Immunities,
		Resistances,
		Vulnerabilities,
		StatusImmunities
	}

	public enum TextType {
		Normal,
		Bold,
		Italic
	}

	public enum TextSpace {
		None,
		Before,
		After
	}
}