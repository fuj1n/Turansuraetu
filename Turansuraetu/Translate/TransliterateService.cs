using WanaKanaSharp;

namespace Turansuraetu.Translate
{
    public class TransliterateService : ITranslateService
    {
        public void Translate(Language from, Language to, string str, ref Project.TranslationPair.MachineTranslations target, bool overwrite)
        {
            if (!overwrite && !string.IsNullOrWhiteSpace(target.Transliteration))
                return;

            if (from == Language.Japanese)
            {
                target.Transliteration = WanaKana.ToRomaji(str);
            }
        }

        public bool IsActive(MainWindow window)
        {
            return true;
        }
    }
}
