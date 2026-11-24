namespace Maviray.Pdf;

/// <summary>
/// A place in a page for one element: a page's header, content or footer, a column's item, a row's item, or the inside
/// of a container. The container calls, such as <see cref="ContainerExtensions.Padding(IContainer, double)"/>, fill it.
/// Only the library implements it, and each one takes a single child.
/// </summary>
public interface IContainer;
