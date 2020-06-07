using System.IO;
using System.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Turansuraetu.Translate.API;

namespace Turansuraetu.Translate
{
    public class BingTranslateService : ITranslateService
    {
        private MicrosoftCognitiveTranslate _api;

        public BingTranslateService()
        {
            if (!File.Exists("Secrets/BingAPI.json"))
            {
                MessageBox.Show("No Bing API authentication found. Bing translation will be unavailable.", "Turansuraetu - Bing API", MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            ApiKeyData keyData = JsonConvert.DeserializeObject<ApiKeyData>(File.ReadAllText("Secrets/BingAPI.json"));

            _api = new MicrosoftCognitiveTranslate(keyData.key, keyData.region);
        }

        public void Translate(Language from, Language to, string str, ref Project.TranslationPair.MachineTranslations target, bool overwrite)
        {
            if (_api == null)
                return;

            if (!overwrite && !string.IsNullOrWhiteSpace(target.Bing))
                return;

            target.Bing = _api.Translate(from, to, str);
        }

        public bool IsActive(MainWindow window)
        {
            return window.DoBing.IsChecked;
        }

        [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Members)]
        private struct ApiKeyData
        {
            public string region;
            public string key;
        }
    }
}
