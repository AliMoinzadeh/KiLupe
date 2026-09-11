namespace KiLupeDemo.Services;

public enum FloatingAnalysisMode
{
    ObjectsCursor,
    TextCursor,
    ObjectsScreen,
    TextScreen
}

public static class FloatingAnalysisModeCatalog
{
    public static FloatingAnalysisMode GetNext(FloatingAnalysisMode mode)
    {
        return mode switch
        {
            FloatingAnalysisMode.ObjectsCursor => FloatingAnalysisMode.TextCursor,
            FloatingAnalysisMode.TextCursor => FloatingAnalysisMode.ObjectsScreen,
            FloatingAnalysisMode.ObjectsScreen => FloatingAnalysisMode.TextScreen,
            _ => FloatingAnalysisMode.ObjectsCursor
        };
    }

    public static string GetLabel(FloatingAnalysisMode mode)
    {
        return mode switch
        {
            FloatingAnalysisMode.ObjectsCursor => "Objekte / Cursor",
            FloatingAnalysisMode.TextCursor => "Text / Cursor",
            FloatingAnalysisMode.ObjectsScreen => "Objekte / Bildschirm",
            FloatingAnalysisMode.TextScreen => "Text / Bildschirm",
            _ => "Objekte / Cursor"
        };
    }

    public static bool UsesTextAnalysis(FloatingAnalysisMode mode)
    {
        return mode is FloatingAnalysisMode.TextCursor or FloatingAnalysisMode.TextScreen;
    }

    public static bool IsFullScreen(FloatingAnalysisMode mode)
    {
        return mode is FloatingAnalysisMode.ObjectsScreen or FloatingAnalysisMode.TextScreen;
    }
}