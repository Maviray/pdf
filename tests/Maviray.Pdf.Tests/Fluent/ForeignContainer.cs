namespace Maviray.Pdf.Tests.Fluent;

/// <summary>An IContainer from outside the library, which every container call must refuse (L-02).</summary>
internal sealed class ForeignContainer : IContainer;
