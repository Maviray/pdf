using System.Globalization;

namespace Maviray.Pdf.Tests.Invoice;

/// <summary>Amounts the German way, such as <c>1.234,56 €</c>, without culture data, so every platform agrees.</summary>
internal static class InvoiceAmounts
{
    private static readonly NumberFormatInfo German = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    public static string Format(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("N2", German) + " €";

    /// <summary>A quantity with no trailing zeros, such as <c>3</c> or <c>2,5</c>.</summary>
    public static string Quantity(decimal quantity) => quantity.ToString("0.##", German);
}
