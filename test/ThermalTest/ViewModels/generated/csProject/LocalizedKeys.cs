using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ToolFrameworkPackage;

namespace HPSystemsTools
{
    public partial class HP3LSThermalTest
    {
        private LocalizedContent? invariant;
        public LocalizedContent Invariant
        {
            get
            {
                if (invariant == null)
                {
                    invariant = new LocalizedContent();
                }
                return invariant;
            }
        }

        private LocalizedContent? localized;
        public LocalizedContent Localized
        {
            get
            {
                if (localized == null)
                {
                    localized = new LocalizedContent(Localize);
                }
                return localized;
            }
        }

		public override string GetLocalizedString(string key)
        {
            foreach (var property in Localized.GetType().GetProperties())
            {
                if (property.Name.ToLowerInvariant().Equals(key.ToLowerInvariant())) return property?.GetValue(Localized)?.ToString() ?? key;
            }
            return key;
        }

		public override string GetInvariantString(string key)
        {
            foreach (var property in Invariant.GetType().GetProperties())
            {
                if (property.Name.ToLowerInvariant().Equals(key.ToLowerInvariant())) return property?.GetValue(Invariant)?.ToString() ?? key;
            }
            return key;
        }

		/// <summary>
        /// The format for the culture name based on RFC 4646 is languagecode2-country/regioncode2
        /// </summary>
		public readonly static Dictionary<string, KeyValuePair<string, string>> SupportedLanguages = new () {
		        { "ar", new KeyValuePair<string, string> ("ar", "العربية") },

        { "bg", new KeyValuePair<string, string> ("bg", "български") },

        { "cs", new KeyValuePair<string, string> ("cs", "Čeština") },

        { "da", new KeyValuePair<string, string> ("da", "Dansk") },

        { "de", new KeyValuePair<string, string> ("de", "Deutsch") },

        { "el", new KeyValuePair<string, string> ("el", "ελληνικά") },

        { "en", new KeyValuePair<string, string> ("en", "English") },

        { "es", new KeyValuePair<string, string> ("es", "Español") },

        { "gl-ES", new KeyValuePair<string, string> ("gl-ES", "Castelán galego") },

        { "eu-ES", new KeyValuePair<string, string> ("eu-ES", "Euskara gaztelania") },

        { "ca-ES", new KeyValuePair<string, string> ("ca-ES", "Català castellà") },

        { "et", new KeyValuePair<string, string> ("et", "esti keel") },

        { "fi", new KeyValuePair<string, string> ("fi", "Suomi") },

        { "fr", new KeyValuePair<string, string> ("fr", "Français") },

        { "he", new KeyValuePair<string, string> ("he", "עברית") },

        { "hr", new KeyValuePair<string, string> ("hr", "Hrvatski") },

        { "hu", new KeyValuePair<string, string> ("hu", "Magyar") },

        { "id", new KeyValuePair<string, string> ("id", "Bahasa Indo") },

        { "it", new KeyValuePair<string, string> ("it", "Italiano") },

        { "ja", new KeyValuePair<string, string> ("ja", "日本語") },

        { "ko", new KeyValuePair<string, string> ("ko", "한국어") },

        { "lt", new KeyValuePair<string, string> ("lt", "Lietuvių kalba") },

        { "lv", new KeyValuePair<string, string> ("lv", "Latviešu valoda") },

        { "nl", new KeyValuePair<string, string> ("nl", "Nederlands") },

        { "no", new KeyValuePair<string, string> ("no", "Norsk") },

        { "nb-NO", new KeyValuePair<string, string> ("no", "Norsk") },

        { "nn-NO", new KeyValuePair<string, string> ("no", "Norsk") },

        { "pl", new KeyValuePair<string, string> ("pl", "Polski") },

        { "pt-BR", new KeyValuePair<string, string> ("pt-BR", "Português (Brasil)") },

        { "pt-PT", new KeyValuePair<string, string> ("pt-PT", "Português (Portugal)") },

        { "ro", new KeyValuePair<string, string> ("ro", "Română") },

        { "ru", new KeyValuePair<string, string> ("ru", "Русский") },

        { "sk", new KeyValuePair<string, string> ("sk", "Slovenčina") },

        { "sl", new KeyValuePair<string, string> ("sl", "Slovenščina") },

        { "sr", new KeyValuePair<string, string> ("sr", "Srpski") },

        { "sv", new KeyValuePair<string, string> ("sv", "Svenska") },

        { "th", new KeyValuePair<string, string> ("th", "ภาษาไทย") },

        { "tr", new KeyValuePair<string, string> ("tr", "Türkçe") },

        { "uk", new KeyValuePair<string, string> ("uk", "Українська") },

        { "zh-TW", new KeyValuePair<string, string> ("zh-TW", "繁體中文") },

        { "zh-HK", new KeyValuePair<string, string> ("zh-TW", "繁體中文") },

        { "zh-Hans-HK", new KeyValuePair<string, string> ("zh-TW", "繁體中文") },

        { "zh-MO", new KeyValuePair<string, string> ("zh-TW", "繁體中文") },

        { "zh-CHT", new KeyValuePair<string, string> ("zh-TW", "繁體中文") },

        { "zh-CN", new KeyValuePair<string, string> ("zh-CN", "简体中文") },

        { "chinese(simplified", new KeyValuePair<string, string> ("zh-CN", "简体中文") },

        { "zh-SG", new KeyValuePair<string, string> ("zh-CN", "简体中文") },

        { "zh-CHS", new KeyValuePair<string, string> ("zh-CN", "简体中文") },

		};

		private static string? defaultLanguage;

		private static string GetDefaultLanguage(string twoLetterCulture, string englishName, string culture) {

            KeyValuePair<string, string> kv = new ("en", SupportedLanguages["en"].Value);

			foreach (var sl in SupportedLanguages)
            {
                // Ignore two letter ISO keys when testing english string override
                if (sl.Key.Length < 3) continue;

				if (englishName.StartsWith(sl.Key)) 
				{
					return sl.Value.Value;
				}
			}

            if (SupportedLanguages.ContainsKey(culture)) kv = SupportedLanguages[culture];
            else if (SupportedLanguages.ContainsKey(twoLetterCulture)) kv = SupportedLanguages[twoLetterCulture];

            return kv.Value;
		  
        }

		private static string GetDefaultLanguage() {
			if (string.IsNullOrEmpty(defaultLanguage))
			{
				string twoLetterCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
				string englishName = CultureInfo.CurrentCulture.EnglishName.ToLowerInvariant().Replace(" ","");
				string cultureName = CultureInfo.CurrentCulture.Name;
				defaultLanguage = GetDefaultLanguage(twoLetterCulture, englishName, cultureName);
			}
			return defaultLanguage!;
        }

		private static string GetSupportedLanguageCultureName(ILocalizeContext language)
        {
			var languageCultureName = "en";

			foreach (var pair in SupportedLanguages.Values)
            {
				if (pair.Value == language.Language) 
				{
					languageCultureName = pair.Key;
					break;
				}
			}
            //var languageCultureName = SupportedLanguages.Where(l => l.Value.Value == language.Language).Select(l => l.Value.Key).FirstOrDefault() ?? "en";
            return languageCultureName;
        }
        public class LocalizedContent
        {
            private class InvariantContext: ILocalizeContext
            {
                public bool IsRTL => false;

                public string Language
                {
                    get => "English";
                    set { Language = value; }
                }

                event PropertyChangedEventHandler? ILocalizeContext.LanguageChanged
                {
                    add
                    {
                        throw new NotImplementedException();
                    }

                    remove
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            private ILocalizeContext Localize { get; set; }
            public LocalizedContent (ILocalizeContext localize)
            {
                Localize = localize;
            }

            public LocalizedContent ()
            {
                Localize = new InvariantContext();
            }

            private readonly Dictionary<string, Dictionary<string, string>> content = new ()
            {
                      { "English", new Dictionary<string, string> {
                    { "Cancel", "Cancel" },
{ "Continue", "Continue" },
{ "HideDescription", "Hide Description" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Next" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Show Description" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "العربية", new Dictionary<string, string> {
                    { "Cancel", "إلغاء" },
{ "Continue", "متابعة" },
{ "HideDescription", "إخفاء الوصف" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "التالي" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "عرض الوصف" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "български", new Dictionary<string, string> {
                    { "Cancel", "Отказ" },
{ "Continue", "Продължи" },
{ "HideDescription", "Скриване на описанието" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Напред" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Показване на описанието" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Čeština", new Dictionary<string, string> {
                    { "Cancel", "Storno" },
{ "Continue", "Pokračovat" },
{ "HideDescription", "Skrýt popis" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Další" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Zobrazit popis" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Dansk", new Dictionary<string, string> {
                    { "Cancel", "Annullér" },
{ "Continue", "Fortsæt" },
{ "HideDescription", "Skjul beskrivelse" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Næste" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Vis beskrivelse" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Deutsch", new Dictionary<string, string> {
                    { "Cancel", "Abbrechen" },
{ "Continue", "Weiter" },
{ "HideDescription", "Beschreibung ausblenden" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Weiter" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Beschreibung anzeigen" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "ελληνικά", new Dictionary<string, string> {
                    { "Cancel", "Άκυρο" },
{ "Continue", "Συνέχεια" },
{ "HideDescription", "Απόκρυψη περιγραφής" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Επόμενο" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Εμφάνιση περιγραφής" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Español", new Dictionary<string, string> {
                    { "Cancel", "Cancelar" },
{ "Continue", "Continuar" },
{ "HideDescription", "Ocultar la descripción" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Siguiente" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Mostrar la descripción" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Castelán galego", new Dictionary<string, string> {
                    { "Cancel", "Cancelar" },
{ "Continue", "Continuar" },
{ "HideDescription", "Ocultar la descripción" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Siguiente" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Mostrar la descripción" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Euskara gaztelania", new Dictionary<string, string> {
                    { "Cancel", "Cancelar" },
{ "Continue", "Continuar" },
{ "HideDescription", "Ocultar la descripción" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Siguiente" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Mostrar la descripción" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Català castellà", new Dictionary<string, string> {
                    { "Cancel", "Cancelar" },
{ "Continue", "Continuar" },
{ "HideDescription", "Ocultar la descripción" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Siguiente" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Mostrar la descripción" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "esti keel", new Dictionary<string, string> {
                    { "Cancel", "Tühista" },
{ "Continue", "Jätka" },
{ "HideDescription", "Peida kirjeldus" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Edasi" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Kuva kirjeldus" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Suomi", new Dictionary<string, string> {
                    { "Cancel", "Peruuta" },
{ "Continue", "Jatka" },
{ "HideDescription", "Piilota kuvaus" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Seuraava" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Näytä kuvaus" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Français", new Dictionary<string, string> {
                    { "Cancel", "Annuler" },
{ "Continue", "Continuer" },
{ "HideDescription", "Masquer la description" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Suivant" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Afficher la description" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "עברית", new Dictionary<string, string> {
                    { "Cancel", "ביטול" },
{ "Continue", "המשך" },
{ "HideDescription", "הסתר תיאור" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "הבא" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "הצג תיאור" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Hrvatski", new Dictionary<string, string> {
                    { "Cancel", "Odustani" },
{ "Continue", "Nastavak" },
{ "HideDescription", "Sakrij opis" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Dalje" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Pokaži opis" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Magyar", new Dictionary<string, string> {
                    { "Cancel", "Mégse" },
{ "Continue", "Folytatás" },
{ "HideDescription", "Leírás elrejtése" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Tovább" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Leírás megjelenítése" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Bahasa Indo", new Dictionary<string, string> {
                    { "Cancel", "Batal" },
{ "Continue", "Lanjutkan" },
{ "HideDescription", "Sembunyikan Keterangan" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Berikutnya" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Tampilkan Keterangan" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Italiano", new Dictionary<string, string> {
                    { "Cancel", "Annulla" },
{ "Continue", "Continua" },
{ "HideDescription", "Nascondi descrizione" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Avanti" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Mostra descrizione" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "日本語", new Dictionary<string, string> {
                    { "Cancel", "キャンセル" },
{ "Continue", "続行" },
{ "HideDescription", "説明を表示しない" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "次へ" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "説明を表示する" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "한국어", new Dictionary<string, string> {
                    { "Cancel", "취소" },
{ "Continue", "계속" },
{ "HideDescription", "설명 숨기기" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "다음" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "간략한 설명" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Lietuvių kalba", new Dictionary<string, string> {
                    { "Cancel", "Atšaukti" },
{ "Continue", "Tęsti" },
{ "HideDescription", "Slėpti aprašymą" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Toliau" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Rodyti aprašą" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Latviešu valoda", new Dictionary<string, string> {
                    { "Cancel", "Atcelt" },
{ "Continue", "Turpināt" },
{ "HideDescription", "Slēpt aprakstu" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Tālāk" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Parādīt aprakstu" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Nederlands", new Dictionary<string, string> {
                    { "Cancel", "Annuleren" },
{ "Continue", "Doorgaan" },
{ "HideDescription", "Beschrijving verbergen" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Volgende" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Beschrijving weergeven" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Norsk", new Dictionary<string, string> {
                    { "Cancel", "Avbryt" },
{ "Continue", "Fortsett" },
{ "HideDescription", "Skjul beskrivelse" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Neste" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Vis beskrivelse" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Polski", new Dictionary<string, string> {
                    { "Cancel", "Anuluj" },
{ "Continue", "Kontynuuj" },
{ "HideDescription", "Ukryj opis" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Dalej" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Pokaż opis" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Português (Brasil)", new Dictionary<string, string> {
                    { "Cancel", "Cancelar" },
{ "Continue", "Continuar" },
{ "HideDescription", "Ocultar descrição" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Avançar" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Mostrar descrição" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Português (Portugal)", new Dictionary<string, string> {
                    { "Cancel", "Cancelar" },
{ "Continue", "Continuar" },
{ "HideDescription", "Ocultar descrição" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Seguinte" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Mostrar descrição" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Română", new Dictionary<string, string> {
                    { "Cancel", "Anulare" },
{ "Continue", "Continuare" },
{ "HideDescription", "Ascundere descriere" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Următor" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Afişare descriere" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Русский", new Dictionary<string, string> {
                    { "Cancel", "Отмена" },
{ "Continue", "Продолжить" },
{ "HideDescription", "Скрыть описание" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Далее" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Отобразить описание" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Slovenčina", new Dictionary<string, string> {
                    { "Cancel", "Zrušiť" },
{ "Continue", "Pokračovať" },
{ "HideDescription", "Skryť popis" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Ďalej" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Zobraziť popis" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Slovenščina", new Dictionary<string, string> {
                    { "Cancel", "Prekliči" },
{ "Continue", "Nadaljuj" },
{ "HideDescription", "Skrij opis" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Naprej" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Pokaži opis" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Srpski", new Dictionary<string, string> {
                    { "Cancel", "Otkaži" },
{ "Continue", "Nastavi" },
{ "HideDescription", "Sakrij opis" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Dalje" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Prikaži opis" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Svenska", new Dictionary<string, string> {
                    { "Cancel", "Avbryt" },
{ "Continue", "Fortsätt" },
{ "HideDescription", "Dölj beskrivning" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Nästa" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Visa beskrivning" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "ภาษาไทย", new Dictionary<string, string> {
                    { "Cancel", "ยกเลิก" },
{ "Continue", "ดำเนินการต่อ" },
{ "HideDescription", "ซ่อนคำอธิบาย" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "ถัดไป" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "แสดงคำอธิบาย" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Türkçe", new Dictionary<string, string> {
                    { "Cancel", "İptal" },
{ "Continue", "Devam" },
{ "HideDescription", "Açıklamayı Gizle" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "İleri" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Açıklamayı Göster" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "Українська", new Dictionary<string, string> {
                    { "Cancel", "Скасувати" },
{ "Continue", "Продовжити" },
{ "HideDescription", "Приховати опис" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "Далі" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "Відобразити опис" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "繁體中文", new Dictionary<string, string> {
                    { "Cancel", "取消" },
{ "Continue", "繼續" },
{ "HideDescription", "隱藏說明" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "下一步" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "顯示說明" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },        { "简体中文", new Dictionary<string, string> {
                    { "Cancel", "取消" },
{ "Continue", "继续" },
{ "HideDescription", "隐藏说明" },
{ "HideReadme", "Hide Readme" },
{ "Instructions", "This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.\r\nFor each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).\r\nIf the temperature goes above this limit, the system is considered to be running hot.\r\nThe test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.\r\nIf the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit." },
{ "MonitoringPeriod", "Monitoring Period" },
{ "Next", "下一步" },
{ "ProcessorLoad", "Processor Load" },
{ "ProcessorMaximumLoad", "Processor maximum load" },
{ "ShowDescription", "显示说明" },
{ "ShowReadme", "Show Readme" },
{ "TemperatureThresholdDTS", "Temperature threshold DTS" },
{ "ToolName", "CPU Thermal Test" },

		} },
            };

			            /// <summary>
            /// Cancel
            /// </summary>
            public string Cancel { get { return content[Localize.Language]["Cancel"]; } }


            /// <summary>
            /// Continue
            /// </summary>
            public string Continue { get { return content[Localize.Language]["Continue"]; } }


            /// <summary>
            /// Hide Description
            /// </summary>
            public string HideDescription { get { return content[Localize.Language]["HideDescription"]; } }


            /// <summary>
            /// Hide Readme
            /// </summary>
            public string HideReadme { get { return content[Localize.Language]["HideReadme"]; } }


            /// <summary>
            /// This test continuously monitors the processor’s temperature zones to ensure the system can manage heat effectively.            /// For each reading, it compares the current temperature to the maximum safe value (known as T-junction or DTS).            /// If the temperature goes above this limit, the system is considered to be running hot.            /// The test will fail if the temperature stays too high for a long time, especially when the processor is not working hard.            /// If the maximum safe temperature (DTS) is unknown, the test uses 100°C as the default limit.
            /// </summary>
            public string Instructions { get { return content[Localize.Language]["Instructions"]; } }


            /// <summary>
            /// Monitoring Period
            /// </summary>
            public string MonitoringPeriod { get { return content[Localize.Language]["MonitoringPeriod"]; } }


            /// <summary>
            /// Next
            /// </summary>
            public string Next { get { return content[Localize.Language]["Next"]; } }


            /// <summary>
            /// Processor Load
            /// </summary>
            public string ProcessorLoad { get { return content[Localize.Language]["ProcessorLoad"]; } }


            /// <summary>
            /// Processor maximum load
            /// </summary>
            public string ProcessorMaximumLoad { get { return content[Localize.Language]["ProcessorMaximumLoad"]; } }


            /// <summary>
            /// Show Description
            /// </summary>
            public string ShowDescription { get { return content[Localize.Language]["ShowDescription"]; } }


            /// <summary>
            /// Show Readme
            /// </summary>
            public string ShowReadme { get { return content[Localize.Language]["ShowReadme"]; } }


            /// <summary>
            /// Temperature threshold DTS
            /// </summary>
            public string TemperatureThresholdDTS { get { return content[Localize.Language]["TemperatureThresholdDTS"]; } }


            /// <summary>
            /// CPU Thermal Test
            /// </summary>
            public string ToolName { get { return content[Localize.Language]["ToolName"]; } }


        }
    }
}

