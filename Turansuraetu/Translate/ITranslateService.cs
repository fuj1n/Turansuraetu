namespace Turansuraetu.Translate
{
    public interface ITranslateService
    {
        public static readonly ITranslateService[] TranslationServices =
        {
            new GoogleTranslateService(),
            new BingTranslateService(),
            new TransliterateService()
        };

        void Translate(Language from, Language to, string str, ref Project.TranslationPair.MachineTranslations target, bool overwrite);
        bool IsActive(MainWindow window);
    }
}
