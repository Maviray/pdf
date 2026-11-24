namespace Maviray.Pdf.Tests.Drawing;

/// <summary>A page the recording canvas saw: its size, and its calls in order.</summary>
internal sealed record RecordedPage(double Width, double Height, List<CanvasCall> Calls);
