using System;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

if (args.Length == 0) { Console.Error.WriteLine("usage: ExtractPdfText <pdf>"); return 1; }
var path = args[0];
using var reader = new PdfReader(path);
using var pdf = new PdfDocument(reader);
var n = pdf.GetNumberOfPages();
Console.WriteLine($"Pages: {n}");
for (int i = 1; i <= n; i++)
{
    var strat = new SimpleTextExtractionStrategy();
    var text = PdfTextExtractor.GetTextFromPage(pdf.GetPage(i), strat);
    Console.WriteLine($"--- Page {i} ---");
    Console.WriteLine(text);
}
return 0;
