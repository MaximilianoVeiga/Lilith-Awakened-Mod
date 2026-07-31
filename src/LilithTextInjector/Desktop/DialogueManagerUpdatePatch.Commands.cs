namespace LilithTextInjector;

// Thin façade forwarding natural-language desktop commands to DesktopCommandRouter.
internal static partial class DialogueManagerUpdatePatch
{
    private static bool TryHandleScreenshotCommand(string text, out string reply) => DesktopCommandRouter.TryHandleScreenshotCommand(text, out reply);

    private static bool TryHandleComputerCommand(string text, out string reply) => DesktopCommandRouter.TryHandleComputerCommand(text, out reply);

    private static bool TryReportSystemStatus(string text, out string reply) => DesktopCommandRouter.TryReportSystemStatus(text, out reply);

    private static bool TryOpenKnownFolder(string text, out string reply) => DesktopCommandRouter.TryOpenKnownFolder(text, out reply);

    private static bool TryHandleWindowCommand(string text, out string reply) => DesktopCommandRouter.TryHandleWindowCommand(text, out reply);

    private static bool TryHandleMediaCommand(string text, out string reply) => DesktopCommandRouter.TryHandleMediaCommand(text, out reply);
}
