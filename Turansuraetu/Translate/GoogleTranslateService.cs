using System;
using System.IO;
using System.Windows;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Translation.V2;

namespace Turansuraetu.Translate
{
    public class GoogleTranslateService : ITranslateService
    {
        private readonly TranslationClient _client;

        public GoogleTranslateService()
        {
            if (!File.Exists("Secrets/GoogleAPI.json"))
            {
                MessageBox.Show("No Google API authentication found. Google translation will be unavailable.", "Turansuraetu - Google API", MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            _client = TranslationClient.Create(GoogleCredential.FromFile("Secrets/GoogleAPI.json"));
        }

        public void Translate(Language from, Language to, string str, ref Project.TranslationPair.MachineTranslations target, bool overwrite)
        {
            if (_client == null)
                return;

            if (!overwrite && !string.IsNullOrWhiteSpace(target.Google))
                return;

            target.Google = _client.TranslateText(str, GetLanguage(to), GetLanguage(from)).TranslatedText;
        }

        public bool IsActive(MainWindow window)
        {
            return window.DoGoogle.IsChecked;
        }

        public string GetLanguage(Language lang)
        {
            return lang switch
            {
                Language.English => LanguageCodes.English,
                Language.Japanese => LanguageCodes.Japanese,
                _ => throw new ArgumentOutOfRangeException(nameof(lang), lang, null)
            };
        }
    }
}
