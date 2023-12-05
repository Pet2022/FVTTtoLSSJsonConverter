using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FVTTtoLSSCharConverter {
	class Program {
		private static string appVersion = "v1.0.0";

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
			string dndSystemVersion = fvttObject._stats.systemVersion.ToString();
			Utilities.AddLog("DND System Version: " + dndSystemVersion);

			// Disable debug outputs
			Utilities.doSilent = true;

			Character fvttCharacter = new Character();

			JToken[] characterClassesEnNames = JsonHelper.SearchTargetKeysByValue(fvttObject, "source", "class");
			JToken[] characterClassesRuNames = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "class");
			JToken[] characterClassesLevels = JsonHelper.SearchTargetKeysByValue(fvttObject, "levels", "class");
			JToken[] characterClassesHitPoints = JsonHelper.SearchTargetKeysByValue(fvttObject, "value", "HitPoints");
			JToken[] characterClassesHitDice = JsonHelper.SearchTargetKeysByName(fvttObject, "hitDice");
			JToken[] characterClassesSpellcasting = JsonHelper.SearchTargetKeysByValue(fvttObject, "spellcasting", "class");
			JToken[] characterClassesSaves;
			if (dndSystemVersion != "2.4.0") {
				characterClassesSaves = JsonHelper.SearchTargetKeysByName(fvttObject, "saves");
			} else {
				characterClassesSaves = JsonHelper.SearchTargetKeysByValue(fvttObject, "grants", "Trait");
			}

			bool isBase = false;
			CharacterClassData cData;
			for (int i = 0; i < characterClassesRuNames.Length; i++) {
				//Utilities.AddLog("==========================================Character Classes HitPoints:\n" + characterClassesHitPoints[i]);
				if (characterClassesHitPoints[i]["1"].ToString() == "max"){
					isBase = true;
				}else{
					isBase = false;	
				}

				fvttCharacter.CreateNewClass(isBase, characterClassesEnNames[i].ToString(), characterClassesLevels[i].ToString());
				cData = fvttCharacter.classes[characterClassesEnNames[i].ToString()];

				cData.className = characterClassesRuNames[i].ToString();
				cData.SetHitDice(characterClassesHitDice[i].ToString());
				fvttCharacter.conMod = Utilities.CalcModificator(fvttObject.system.abilities.con.value.ToString());

				int hpIntValue = 0;
				for (int h = 1; h <= characterClassesHitPoints[i].Count<JToken>(); h++) {
					//Utilities.AddLog("HP VALUE:" + characterClassesHitPoints[i][h.ToString()]);

					if (characterClassesHitPoints[i][h.ToString()].ToString() == "max") {
						characterClassesHitPoints[i][h.ToString()] = cData.hitDiceValue.ToString();
					}

					if (characterClassesHitPoints[i][h.ToString()].ToString() == "avg") {
						characterClassesHitPoints[i][h.ToString()] = (cData.hitDiceValue/2 + 1).ToString();
					}

					int.TryParse(characterClassesHitPoints[i][h.ToString()].ToString(), out hpIntValue);
					cData.lvlsHP.Add(hpIntValue);
				}

				foreach (var saveToken in characterClassesSaves[i]) {
					Utilities.AddLog("Saves TOKEN:" + saveToken);

					if (dndSystemVersion != "2.4.0") {
						cData.saves.Add(saveToken.ToString());
					} else {
						cData.saves.Add(saveToken.ToString().Substring(6));
					}
				}

				cData.spellcastCharacteristic = characterClassesSpellcasting[i]["ability"].ToString();
			}
			//Utilities.AddLog("==========================================\n");
			for (int i = 0; i < fvttCharacter.classes.Count; i++) {
				cData = fvttCharacter.classes[characterClassesEnNames[i].ToString()];
				//Utilities.AddLog("cData.className: " + cData.className);
				//Utilities.AddLog("cData.spellcastCharacteristic: " + cData.spellcastCharacteristic);

				if (!string.IsNullOrEmpty(cData.spellcastCharacteristic)){
					cData.spellSave = 8 + fvttCharacter.GetProficiencyBonus() + Utilities.CalcModificator(fvttObject.system.abilities[cData.spellcastCharacteristic].value.ToString());
					cData.spellAttackBonus = fvttCharacter.GetProficiencyBonus() + Utilities.CalcModificator(fvttObject.system.abilities[cData.spellcastCharacteristic].value.ToString());
				}
				//Utilities.AddLog("==========================================Character Data:\n" + JValue.FromObject(fvttCharacter.classes[characterClassesEnNames[i].ToString()]));
			}

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
			string race = JsonHelper.SearchTargetKeyByValue(fvttObject, "value", "system.details.race");
			if (string.IsNullOrEmpty(race)) {
				race = fvttObject.system.details.race;
			}
			newLSSObject.info.race.value = race;

			// Background
			string background = JsonHelper.SearchTargetKeyByValue(fvttObject, "name", "background");
			if (string.IsNullOrEmpty(background)) {
				background = fvttObject.system.details.background;
			}
			newLSSObject.info.background.value = background;

			newLSSObject.info.alignment.value = fvttObject.system.details.alignment;
			newLSSObject.info.charClass.value = fvttCharacter.GetClassesString();
			newLSSObject.info.level.value = fvttCharacter.GetTotalLevel();
			newLSSObject.info.experience.value = fvttObject.system.details.xp.value;

			// LSS Stats
			newLSSObject.stats.str.score = fvttObject.system.abilities.str.value;
			newLSSObject.stats.dex.score = fvttObject.system.abilities.dex.value;
			newLSSObject.stats.con.score = fvttObject.system.abilities.con.value;
			newLSSObject.stats["int"].score = fvttObject.system.abilities["int"].value;
			newLSSObject.stats.wis.score = fvttObject.system.abilities.wis.value;
			newLSSObject.stats.cha.score = fvttObject.system.abilities.cha.value;

			//Utilities.doSilent = true;
			// LSS Skills
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

			List<CharacterClassData> casterClasses = fvttCharacter.GetCastClasses();
			//newLSSObject = AddLineToNotes(newLSSObject, "notes-1", "\n");
			foreach (var cData3 in casterClasses) {
				newLSSObject = AddLineToNotes(newLSSObject, "notes-1", cData3.GetCasterClassString());
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
			}

			// LSS Vitality
			newLSSObject.vitality["hp-current"].value = fvttObject.system.attributes.hp.value;
			newLSSObject.vitality["hp-max"].value = fvttCharacter.GetMaxHP();

			newLSSObject.vitality["hit-die"].value = fvttCharacter.GetHpDiceString();
			//Utilities.AddLog("hit-die: " + newLSSObject.vitality["hit-die"].value);

			Dictionary<string, int> hpDicesMulti = fvttCharacter.GetHpDiceMulti();

			newLSSObject = AddLineToNotes(newLSSObject, "notes-4", "Кости хитов:");
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

			newLSSObject.vitality.speed.value = fvttObject.system.attributes.movement.walk; // fly, climb, swim, burrow. Add this to some textbox
			newLSSObject.vitality.ac.value = "";
			newLSSObject.vitality.initiative.value = Utilities.CalcModificator(fvttObject.system.abilities.dex.value.ToString());

			newLSSObject.vitality.deathFails = fvttObject.system.attributes.death.failure;
			newLSSObject.vitality.deathSuccesses = fvttObject.system.attributes.death.success;

			// LSS Saves
			foreach (var cData2 in fvttCharacter.classes.Values) {
				if(cData2.baseClass){ 
					foreach(string save in cData2.saves){
						Utilities.AddLog(save);
						newLSSObject.saves[save].isProf = true;
					}
				}else{
					continue;	
				}
			}
			Utilities.AddLog(newLSSObject.saves.ToString());

			// LSS casterClass
			CharacterClassData casterData = fvttCharacter.FindMainCasterClass(true);
			if(casterData != null){
				newLSSObject.casterClass.value = casterData.className;
				//newLSSObject.casterClass.value = casterData.classNameOriginal;

				newLSSObject.spellsInfo["base"].code = casterData.spellcastCharacteristic;
				newLSSObject.spellsInfo.save.value = casterData.spellSave;
				newLSSObject.spellsInfo.mod.value = casterData.spellAttackBonus;

				Utilities.AddLog("Main Caster Class: " + newLSSObject.casterClass.value);
				Utilities.AddLog("Spellcast Characteristic: " + newLSSObject.spellsInfo["base"].value);
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

			JToken[] characterItemsWeapons = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "weapon");
			JToken[] characterItemsEquipment = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "equipment");
			JToken[] characterItemsBackpack = JsonHelper.SearchTargetKeysByValue(fvttObject, "name", "backpack");
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

					string description = "\n";
					
					if (tmpJson.system != null) {
						description += StripHTML(tmpJson.system.description.value.ToString());
					}else if(!string.IsNullOrEmpty(tmpJson.description.ToString())) {
						description += StripHTML(tmpJson.description.ToString());
					}

					if(tmpJson.system != null && !string.IsNullOrEmpty(tmpJson.system.uses.max.ToString())){
						featName = "<b>" + featName + ":</b> " + GetUsesString(tmpJson.system.uses);
						newLSSObject = AddLineToNotes(newLSSObject, "traits", featName);
					}else{
						newLSSObject = AddLineToNotes(newLSSObject, "features", featName);
						//newLSSObject = AddLineToNotes(newLSSObject, "features", featName + description + "\n");
					}
				}
			}

			Utilities.AddLog("==========================================Character Spells:\n");
			foreach (var spell in characterItemsSpells) {
				tmpJson = (dynamic)spell.Parent.Parent;
				//Utilities.AddLog(spell.ToString());

				string itemName = SimplifyName(spell.ToString());
				bool isPrepared = false;

				try{
					Utilities.AddLog("prepared: " + tmpJson.system.preparation.prepared.ToString());
				
					if (!string.IsNullOrEmpty(tmpJson.system.preparation.prepared.ToString())){
						isPrepared = tmpJson.system.preparation.prepared;
					}
				}catch(Exception ex){}

				if(isPrepared){
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
					}

					Utilities.AddLog(resultLine);

					string spellLevel = tmpJson.system.level.ToString();
					newLSSObject = AddLineToNotes(newLSSObject, "spells-level-" + spellLevel, itemName + spellSaveChar);
				}
			}

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

			if(result.Contains(" (")){
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