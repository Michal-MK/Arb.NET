public static class Constants {
    public const string ARB_FILE_EXT = ".arb";
    public const string ANY_ARB = "*" + ARB_FILE_EXT;
    
    public const string LOCALIZATION_FILE = "l10n.yaml";

    public const string ARB_META_PREFIX = "@";
    public const string ARB_RESERVED_PREFIX = "@@";
    
    public const string KEY_LOCALE = ARB_RESERVED_PREFIX + "locale";
    public const string KEY_CONTEXT = ARB_RESERVED_PREFIX + "context";
    
    
    public const string ARB_META_CONTENT_DESCRIPTION = "description";
    public const string ARB_MET_CONTENT_PLACEHOLDERS = "placeholders";
    public const string ARB_META_CONTENT_PLACEHOLDER_TYPE = "type";
}