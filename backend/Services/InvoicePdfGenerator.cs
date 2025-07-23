using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;

public class InvoicePdfGenerator
{
    public static void GenerateInvoicePdf(string filePath)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Size(PageSizes.A4);

                page.Content()
                    .Text("Test Invoice Content")
                    .FontSize(36)
                    .Bold();
            });
        }).GeneratePdf(filePath);
    }
}
