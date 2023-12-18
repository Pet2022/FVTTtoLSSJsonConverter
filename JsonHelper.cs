using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace FVTTtoLSSCharConverter {

	// Newtonsoft/JsonFileUtils.cs
	public static class JsonFileUtils {
		private static readonly JsonSerializerSettings _options
			= new() { NullValueHandling = NullValueHandling.Ignore };

		public static void SimpleWrite(object obj, string fileName) {
			var jsonString = JsonConvert.SerializeObject(obj, _options);
			File.WriteAllText(fileName, jsonString);
		}
	}

	public static class JsonHelper {

		// Search key by value, return 1 string item
		public static string SearchTargetKeyByValue(dynamic jsonObject, string targetSearchKey, string searchValue, string defaultValue = "none") {
			string result = defaultValue;

			JToken token = ((JObject)jsonObject).Descendants()
				.Where(t => t.Type == JTokenType.Property && ((JProperty)t).Value.ToString() == searchValue)
				.Select(p => p.Parent).Descendants()
				.Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name == targetSearchKey)
				.Select(p => ((JProperty)p).Value)
				.FirstOrDefault();

			if (token != null) {
				result = token.ToString();
			}

			Utilities.AddLog("\n[=== FindTargetValueByKey ===]");
			Utilities.AddLog("FindTargetValueByKey targetSearchKey: " + targetSearchKey);
			Utilities.AddLog("FindTargetValueByKey searchKey: " + searchValue);
			Utilities.AddLog("FindTargetValueByKey result: " + result);

			return result;
		}

		// Search keys by value, return JToken Array
		public static JToken[] SearchTargetKeysByValue(dynamic jsonObject, string targetSearchKey, string searchValue) {
			JToken[] tokens = ((JObject)jsonObject).Descendants()
				.Where(t => t.Type == JTokenType.Property && ((JProperty)t).Value.ToString() == searchValue)
				.Select(p => p.Parent).Descendants()//First<JToken>()
				.Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name == targetSearchKey)
				.Select(p => ((JProperty)p).Value)
				.ToArray();

			Utilities.AddLog("\n[=== FindTargetValuesByKey ===]");
			Utilities.AddLog("FindTargetValuesByKey targetSearchKey: " + targetSearchKey);
			Utilities.AddLog("FindTargetValuesByKey searchKey: " + searchValue);
			Utilities.AddLog("FindTargetValuesByKey token.Lenght: " + tokens.Length);

			return tokens;
		}


		// Search key, return 1 string item
		public static string SearchTargetKeyByName(dynamic jsonObject, string targetSearchKey, string defaultValue = "none") {
			string result = defaultValue;

			JToken token = ((JObject)jsonObject).Descendants()
				.Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name == targetSearchKey)
				.Select(p => ((JProperty)p).Value)
				.FirstOrDefault();

			if (token != null) {
				result = token.ToString();
			}

			Utilities.AddLog("\n[=== FindTargetValueByKey ===]");
			Utilities.AddLog("FindTargetValueByKey targetSearchKey: " + targetSearchKey);
			Utilities.AddLog("FindTargetValueByKey result: " + result);

			return result;
		}

		// Search keys, return JToken Array
		public static JToken[] SearchTargetKeysByName(dynamic jsonObject, string targetSearchKey) {
			JToken[] tokens = ((JObject)jsonObject).Descendants()
				.Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name == targetSearchKey)
				.Select(p => ((JProperty)p).Value)
				.ToArray();

			Utilities.AddLog("\n[=== FindTargetValuesByKey ===]");
			Utilities.AddLog("FindTargetValuesByKey targetSearchKey: " + targetSearchKey);
			Utilities.AddLog("FindTargetValuesByKey token.Lenght: " + tokens.Length);

			return tokens;
		}

		public static string FVTT_GetSkillProf(dynamic fvttJsonObject, string skillProfKey) {
			Utilities.AddLog("\n[=== FVTT_GetSkillProf for Key: " + skillProfKey + " ===]");

			string result = fvttJsonObject.system.skills[skillProfKey].value;
			string result2 = SearchTargetKeyByValue(fvttJsonObject, "value", "system.skills." + skillProfKey + ".value");

			if (result2 != "none" && result.ToString() != result2) {
				return result2;
			}

			return result;
		}

		public static int FVTT_GetSkillBonus(dynamic fvttJsonObject, string skillProfKey) {
			Utilities.AddLog("\n[=== FVTT_GetSkillBonus for Key: " + skillProfKey + " ===]");

			string result = fvttJsonObject.system.skills[skillProfKey].bonuses.check;

			//string result2 = SearchTargetKeyByValue(fvttJsonObject, "value", "system.skills." + skillProfKey + ".bonus");

			//if (result2 != "none" && result.ToString() != result2) {
			//	return result2;
			//}

			int tryToInt = 0;
			int.TryParse(result, out tryToInt);

			if(tryToInt != 0){
				return tryToInt;	
			}else if (!string.IsNullOrEmpty(result)){
				result = result.Remove(result.IndexOf('+'), 1);
				int.TryParse(result, out tryToInt);
			}

			Utilities.AddLog("\n=== Try To Int bonus: " + skillProfKey + " ===" + tryToInt);

			return tryToInt;
		}

		// For debug purposes
		public static void MakeLSSTemplateFormatted(string filePath, string saveDir) {
			string text = File.ReadAllText(filePath);

			text = text.Replace("{", "{" + Environment.NewLine);
			text = text.Replace("}", Environment.NewLine + "}");
			text = text.Replace(",", "," + Environment.NewLine);

			using (StreamWriter sw = new StreamWriter(File.OpenWrite(AppContext.BaseDirectory + saveDir + "/lss_template_formatted.json"))) {
				sw.Write(text);
			}
		}

		public static dynamic GetJsonRepresentationFromFile(string filePath) {
			//Utilities.AddLog("===================================== READ JSON: " + filePath);
			string text = File.ReadAllText(filePath);

			return JValue.Parse(text);
		}
	}
}
