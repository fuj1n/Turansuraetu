using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Turansuraetu.Translate.API
{
    public class MicrosoftCognitiveTranslate
    {
        private readonly Dictionary<Language, string> _supportedLanguages;
        private readonly Dictionary<Language, string> _transliterationLanguages;

        // Used to retrieve language list
        private const string ApiEndpoint = "https://api.cognitive.microsofttranslator.com/{0}?api-version=3.0";

        private readonly string _apiKey;
        private readonly string _regionCode;

        public MicrosoftCognitiveTranslate(string apiKey, string regionCode)
        {
            _apiKey = apiKey;
            _regionCode = regionCode;

            // Get supported languages
            string uri = string.Format(ApiEndpoint, "languages");

            Dictionary<string, Dictionary<string, ApiLanguage>> response =
                JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, ApiLanguage>>>(
                    DoSimpleWebRequest(uri));
            _supportedLanguages = response["translation"]
                .Where(x => Enum.TryParse(x.Value.name, true, out Language _))
                .ToDictionary(x => Enum.Parse<Translate.Language>(x.Value.name, true), x => x.Key);

            _transliterationLanguages = response["transliteration"]
                .Where(x => Enum.TryParse(x.Value.name, true, out Language _))
                .ToDictionary(x => Enum.Parse<Translate.Language>(x.Value.name, true), x => x.Key);
        }

        public string Translate(Language from, Language to, string str)
        {
            string fromCode = GetLanguageCodeFor(from);
            string toCode = GetLanguageCodeFor(to);

            if (string.IsNullOrWhiteSpace(str) || fromCode.Equals(toCode))
                return str;

            string endpoint = string.Format(ApiEndpoint, "translate");
            string uri = endpoint + $"&from={fromCode}&to={toCode}";
            string requestBody = JsonConvert.SerializeObject(new[] {new ApiTranslationString(str)});

            string resp = DoSimpleWebRequest(uri, requestBody, true);
            ApiTranslationResponse response = JsonConvert
                .DeserializeObject<ApiTranslationResponse[]>(resp)
                .FirstOrDefault();

            return response.translations.FirstOrDefault().text;
        }

        private string DoSimpleWebRequest(string uri, string body = null, bool appendKey = false)
        {
            HttpWebRequest request = WebRequest.CreateHttp(uri);
            request.Headers.Add(uri);

            if (appendKey)
            {
                request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
                request.Headers.Add("Ocp-Apim-Subscription-Region", _regionCode);
            }

            request.Headers.Add("X-ClientTraceId", Guid.NewGuid().ToString());

            if (body != null)
            {
                request.Method = WebRequestMethods.Http.Post;
                request.ContentType = "application/json";

                Stream stream = request.GetRequestStream();

                byte[] data = Encoding.UTF8.GetBytes(body);
                stream.Write(data, 0, data.Length);
                stream.Close();
            }

            WebResponse response = request.GetResponse();
            
            using StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        [Pure]
        private string GetLanguageCodeFor(Translate.Language language)
        {
            if(!_supportedLanguages.ContainsKey(language))
                throw new ArgumentException($"{language} is not supported.");

            return _supportedLanguages[language];
        }

        // Bing API language request
        [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
        private struct ApiLanguage
        {
            public string name;
            // public string nativeName;
            // public string dir;
        }

        private struct ApiTranslationString
        {
            // ReSharper disable once InconsistentNaming - API encoding
            public string Text;

            public ApiTranslationString(string text)
            {
                Text = text;
            }
        }

        private struct ApiTranslationResponse
        {
            public Translation[] translations;

            public struct Translation
            {
                // public string to;
                public string text;
                public Transliteration transliteration;

                public struct Transliteration
                {
                    public string text;
                }
            }
        }

        private struct ApiTransliterationResponse
        {
            public string text;
            // public string script;
        }
    }
}
