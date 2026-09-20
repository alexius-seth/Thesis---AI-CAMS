using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ACPAMS.ViewModels;

namespace ACPAMS.Services
{
    public static class PdfExportService
    {
        private const string PrimaryFont = "Lato";

        static PdfExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = true;
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 1. STUDENT PERFORMANCE & INTEGRITY REPORT
        // ══════════════════════════════════════════════════════════════════════
        public static void ExportStudentReport(StudentReportViewModel report, string filePath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24, Unit.Point);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(PrimaryFont).FontColor("#1E293B"));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(titleCol =>
                            {
                                titleCol.Item().Text("AI-CAMS ASSESSMENT REPORT").FontSize(15).Bold().FontColor("#1565C0");
                                titleCol.Item().Text("Individual Student Performance & Integrity Audit").FontSize(9).FontColor("#64748B");
                            });

                            row.ConstantItem(140).AlignRight().Column(metaCol =>
                            {
                                metaCol.Item().Text($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor("#64748B");
                                metaCol.Item().Text($"Status: {report.Status.ToUpperInvariant()}").FontSize(8).Bold().FontColor("#1565C0");
                            });
                        });

                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E2E8F0");
                    });

                    // Body
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Student & Exam Metadata Box
                        col.Item().Border(1).BorderColor("#E2E8F0").Background("#F8FAFC").Padding(10).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Student: ").Bold(); t.Span(report.StudentName); });
                                c.Item().Text(t => { t.Span("Student ID: ").Bold(); t.Span(report.StudentId); });
                                c.Item().Text(t => { t.Span("Program: ").Bold(); t.Span(report.Program); });
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Exam: ").Bold(); t.Span(report.ExamName); });
                                c.Item().Text(t => { t.Span("Exam Code: ").Bold(); t.Span(report.ExamCode); });
                                c.Item().Text(t => { t.Span("Submitted: ").Bold(); t.Span(report.SubmittedAt?.ToString("yyyy-MM-dd HH:mm") ?? "In Progress"); });
                            });
                        });

                        // Score & Risk KPI Cards
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            // Score Card
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").Padding(8).Column(c =>
                            {
                                c.Item().Text("TOTAL SCORE").FontSize(8).Bold().FontColor("#64748B");
                                c.Item().Text($"{report.TotalScore:0.#} / {report.MaxPossibleScore:0.#}").FontSize(14).Bold().FontColor("#0F172A");
                                c.Item().Text($"Percentage: {report.TotalScorePercent:P1}").FontSize(9).FontColor("#1565C0");
                            });

                            row.ConstantItem(10);

                            // Anomaly Score Card
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").Padding(8).Column(c =>
                            {
                                c.Item().Text("INTEGRITY / ANOMALY SCORE").FontSize(8).Bold().FontColor("#64748B");
                                c.Item().Text($"{report.CheatingScore:0.#} / 100").FontSize(14).Bold().FontColor(report.RiskLevel.ToLowerInvariant() == "high" ? "#DC2626" : report.RiskLevel.ToLowerInvariant() == "medium" ? "#D97706" : "#16A34A");
                                c.Item().Text($"Risk Level: {report.RiskLevel.ToUpperInvariant()} ({report.TotalFlags} flags)").FontSize(9).Bold();
                            });
                        });

                        // AI Performance Assessment (if available)
                        if (report.HasAssessment)
                        {
                            col.Item().PaddingTop(12).Text("AI PERFORMANCE ASSESSMENT & INSIGHTS").FontSize(10).Bold().FontColor("#1565C0");
                            col.Item().Border(1).BorderColor("#DBEAFE").Background("#EFF6FF").Padding(10).Column(c =>
                            {
                                if (!string.IsNullOrWhiteSpace(report.AssessmentSummary))
                                {
                                    c.Item().Text(t => { t.Span("Executive Summary: ").Bold(); t.Span(report.AssessmentSummary); });
                                }

                                if (report.Strengths != null && report.Strengths.Count > 0)
                                {
                                    c.Item().PaddingTop(4).Text("Key Strengths:").Bold().FontColor("#16A34A");
                                    foreach (var str in report.Strengths)
                                    {
                                        c.Item().PaddingLeft(8).Text($"• {str}");
                                    }
                                }

                                if (report.Weaknesses != null && report.Weaknesses.Count > 0)
                                {
                                    c.Item().PaddingTop(4).Text("Areas for Improvement:").Bold().FontColor("#DC2626");
                                    foreach (var w in report.Weaknesses)
                                    {
                                        c.Item().PaddingLeft(8).Text($"• {w}");
                                    }
                                }
                            });
                        }

                        // Question & Answer Breakdown
                        if (report.QuestionAnswers != null && report.QuestionAnswers.Count > 0)
                        {
                            col.Item().PaddingTop(12).Text("QUESTION-BY-QUESTION BREAKDOWN").FontSize(10).Bold().FontColor("#1565C0");
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(24);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(3);
                                    columns.ConstantColumn(50);
                                    columns.ConstantColumn(45);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("#").Bold();
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("Question").Bold();
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("Student Answer").Bold();
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("Points").Bold();
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("Status").Bold();
                                });

                                foreach (var q in report.QuestionAnswers)
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(q.Number.ToString());
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(q.Text ?? "");
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(q.StudentAnswer ?? "");
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text($"{q.ObtainedPoints?.ToString("0.#") ?? "-"}/{q.MaxPoints:0.#}");
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(q.Status);
                                }
                            });
                        }

                        // Flag & Event History (if any)
                        if (report.FlagHistory != null && report.FlagHistory.Count > 0)
                        {
                            col.Item().PaddingTop(12).Text("INTEGRITY & ANOMALY LOG").FontSize(10).Bold().FontColor("#1565C0");
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(100);
                                    columns.ConstantColumn(120);
                                    columns.ConstantColumn(60);
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("Timestamp").Bold();
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("Event").Bold();
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("Severity").Bold();
                                    header.Cell().Background("#F1F5F9").Padding(4).Text("Details").Bold();
                                });

                                foreach (var flag in report.FlagHistory)
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(flag.Timestamp.ToString("HH:mm:ss"));
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(flag.EventType);
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(flag.Severity);
                                    table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(flag.Description);
                                }
                            });
                        }
                    });

                    // Footer
                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text("AI-CAMS — Centralized Assessment Monitoring System").FontSize(8).FontColor("#94A3B8");
                        row.ConstantItem(100).AlignRight().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });
            }).GeneratePdf(filePath);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. CLASS & SECTION PERFORMANCE REPORT
        // ══════════════════════════════════════════════════════════════════════
        public static void ExportClassReport(
            ReportsSummaryDto summary,
            List<SectionAverageDto>? sections,
            string examTitle,
            string instructorName,
            string filePath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24, Unit.Point);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(PrimaryFont).FontColor("#1E293B"));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(titleCol =>
                            {
                                titleCol.Item().Text("AI-CAMS CLASS PERFORMANCE REPORT").FontSize(15).Bold().FontColor("#1565C0");
                                titleCol.Item().Text($"Exam: {examTitle}").FontSize(10).FontColor("#0F172A");
                                titleCol.Item().Text($"Instructor: {instructorName}").FontSize(9).FontColor("#64748B");
                            });

                            row.ConstantItem(130).AlignRight().Column(metaCol =>
                            {
                                metaCol.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor("#64748B");
                                metaCol.Item().Text($"Total Attempts: {summary.TotalAttempts}").FontSize(8).Bold();
                            });
                        });

                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E2E8F0");
                    });

                    // Body
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // 3 KPI Cards
                        col.Item().Row(row =>
                        {
                            // Class Average
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").Padding(8).Column(c =>
                            {
                                c.Item().Text("CLASS AVERAGE").FontSize(8).Bold().FontColor("#64748B");
                                c.Item().Text(summary.HasClassAverage ? $"{summary.ClassAveragePercent:0.#}%" : "N/A").FontSize(14).Bold().FontColor("#1565C0");
                                c.Item().Text($"{summary.GradedCount} graded submissions").FontSize(8).FontColor("#64748B");
                            });

                            row.ConstantItem(8);

                            // Completion Rate
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").Padding(8).Column(c =>
                            {
                                c.Item().Text("COMPLETION RATE").FontSize(8).Bold().FontColor("#64748B");
                                c.Item().Text(summary.HasCompletionRate ? $"{summary.CompletionRatePercent:0.#}%" : "N/A").FontSize(14).Bold().FontColor("#16A34A");
                                c.Item().Text($"{summary.TotalAttempts} total registered").FontSize(8).FontColor("#64748B");
                            });

                            row.ConstantItem(8);

                            // Integrity Index
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").Padding(8).Column(c =>
                            {
                                c.Item().Text("INTEGRITY INDEX").FontSize(8).Bold().FontColor("#64748B");
                                c.Item().Text(summary.HasIntegrityIndex ? $"{summary.IntegrityIndexPercent:0.#}%" : "N/A").FontSize(14).Bold().FontColor(summary.IntegrityIndexPercent >= 80 ? "#16A34A" : "#D97706");
                                c.Item().Text($"{summary.TotalInfractions} total flags").FontSize(8).FontColor("#64748B");
                            });
                        });

                        // Infraction Breakdown & Section Performance Row
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            // Infractions Box
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#F8FAFC").Padding(8).Column(c =>
                            {
                                c.Item().Text("MONITORING INFRACTIONS BREAKDOWN").FontSize(8).Bold().FontColor("#1565C0");
                                c.Item().PaddingTop(4).Text($"• Gaze Deviations: {summary.GazeDeviationCount}");
                                c.Item().Text($"• Window / App Switches: {summary.WindowSwitchCount}");
                                c.Item().Text($"• Clipboard Attempts: {summary.ClipboardCount}");
                                c.Item().Text($"• Total Flags Recorded: {summary.TotalInfractions}").Bold();
                            });

                            row.ConstantItem(10);

                            // Section Averages Box
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#F8FAFC").Padding(8).Column(c =>
                            {
                                c.Item().Text("SECTION PERFORMANCE AVERAGES").FontSize(8).Bold().FontColor("#1565C0");
                                if (sections != null && sections.Count > 0)
                                {
                                    foreach (var s in sections)
                                    {
                                        c.Item().PaddingTop(2).Text($"• {s.SectionName}: {(s.HasData ? $"{s.AveragePercent:0.#}%" : "Pending")}");
                                    }
                                }
                                else
                                {
                                    c.Item().PaddingTop(4).Text("No section breakdown available.").FontColor("#94A3B8");
                                }
                            });
                        });

                        // Detailed Student Performance Table
                        col.Item().PaddingTop(12).Text("STUDENT RESULTS TABLE").FontSize(10).Bold().FontColor("#1565C0");
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(100);
                                columns.RelativeColumn(2);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(80);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#F1F5F9").Padding(4).Text("Student ID").Bold();
                                header.Cell().Background("#F1F5F9").Padding(4).Text("Student Name").Bold();
                                header.Cell().Background("#F1F5F9").Padding(4).Text("Score").Bold();
                                header.Cell().Background("#F1F5F9").Padding(4).Text("Flags").Bold();
                                header.Cell().Background("#F1F5F9").Padding(4).Text("Risk Level").Bold();
                            });

                            foreach (var r in summary.Rows)
                            {
                                table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(r.StudentId);
                                table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(r.StudentName);
                                table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(r.FinalScoreText);
                                table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(r.AnomalyCount.ToString());
                                table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(4).Text(r.RiskLevel);
                            }
                        });
                    });

                    // Footer
                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text("AI-CAMS — Centralized Assessment Monitoring System").FontSize(8).FontColor("#94A3B8");
                        row.ConstantItem(100).AlignRight().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });
            }).GeneratePdf(filePath);
        }
    }
}
