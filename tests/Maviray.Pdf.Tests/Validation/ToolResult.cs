namespace Maviray.Pdf.Tests.Validation;

/// <summary>What an external tool returned.</summary>
internal sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError);
