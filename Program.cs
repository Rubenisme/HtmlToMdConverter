namespace HtmlToMdConverter;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using HtmlAgilityPack;

internal class Program
{
    public static void Main(string[] args)
    {
        if (args.Length is 0)
        {
            Console.WriteLine("Usage: HtmlToMdConverter <input.html>");
            return;
        }

        var htmlPath = args[0];
        if (!File.Exists(htmlPath))
        {
            Console.WriteLine($"File not found: {htmlPath}");
            return;
        }

        var htmlContent = File.ReadAllText(htmlPath);
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        var markdown = new StringBuilder();
        ConvertNode(doc.DocumentNode, markdown, 0);
        File.WriteAllText("output.md", markdown.ToString());
        Console.WriteLine("Markdown generated: output.md");
    }

    private static void ConvertNode(HtmlNode node, StringBuilder md, int listLevel)
    {
        switch (node.Name)
        {
            case "h1":
                md.AppendLine($"# {node.InnerText.Trim()}");
                md.AppendLine();
                break;
            case "h2":
                md.AppendLine($"## {node.InnerText.Trim()}");
                md.AppendLine();
                break;
            case "h3":
                md.AppendLine($"### {node.InnerText.Trim()}");
                md.AppendLine();
                break;
            case "p":
                md.AppendLine(node.InnerText.Trim());
                md.AppendLine();
                break;
            case "strong":
                md.Append($"**{node.InnerText.Trim()}**");
                break;
            case "em":
                md.Append($"*{node.InnerText.Trim()}*");
                break;
            case "code":
                if (node.ParentNode.Name == "pre" || node.ParentNode.Name == "div")
                    md.Append(node.InnerText); // inside pre block, handled by "pre" case
                else
                    md.Append($"`{node.InnerText}`");
                break;
            case "pre":
                var lang = ExtractCodeLanguage(node);
                var codeNode = node.SelectSingleNode(".//code");
                var codeText = codeNode != null ? codeNode.InnerText : node.InnerText;
                md.AppendLine($"```{lang}");
                md.AppendLine(codeText.Trim());
                md.AppendLine("```");
                md.AppendLine();
                break;
            case "ul":
                foreach (var li in node.SelectNodes("li"))
                    ConvertNode(li, md, listLevel + 1);
                md.AppendLine();
                break;
            case "ol":
                var index = 1;
                foreach (var li in node.SelectNodes("li"))
                {
                    md.Append(new string(' ', listLevel * 2));
                    md.AppendLine($"{index}. {li.InnerText.Trim()}");
                    index++;
                }
                md.AppendLine();
                break;
            case "li":
                md.Append(new string(' ', (listLevel - 1) * 2));
                md.AppendLine($"- {node.InnerText.Trim()}");
                break;
            case "blockquote":
                foreach (var child in node.ChildNodes)
                {
                    if (child.Name == "p")
                        md.AppendLine($"> {child.InnerText.Trim()}");
                    else if (child.NodeType == HtmlNodeType.Text && !string.IsNullOrWhiteSpace(child.InnerText))
                        md.AppendLine($"> {child.InnerText.Trim()}");
                }
                md.AppendLine();
                break;
            case "table":
                ConvertTable(node, md);
                break;
            case "br":
                md.AppendLine();
                break;
            case "button":
            case "svg":
                break; // skip UI elements (ChatGPT copy buttons, icons)
            case "hr":
                md.AppendLine("---");
                md.AppendLine();
                break;
            case "span":
                // ChatGPT renders KaTeX math in <span class="katex-display"> or <span class="katex">
                var cls = node.GetAttributeValue("class", "");
                if (cls.Contains("katex"))
                {
                    var annotation = node.SelectSingleNode(".//annotation");
                    if (annotation != null)
                    {
                        var tex = WebUtility.HtmlDecode(annotation.InnerText).Trim();
                        if (cls.Contains("katex-display"))
                        {
                            md.AppendLine("$$");
                            md.AppendLine(tex);
                            md.AppendLine("$$");
                            md.AppendLine();
                        }
                        else
                        {
                            md.Append($"${tex}$");
                        }
                    }
                }
                else
                {
                    foreach (var child in node.ChildNodes)
                        ConvertNode(child, md, listLevel);
                }
                break;
            default:
                foreach (var child in node.ChildNodes)
                    ConvertNode(child, md, listLevel);
                break;
        }
    }

    private static string ExtractCodeLanguage(HtmlNode preNode)
    {
        // ChatGPT puts the language label in a div like:
        // <div class="flex items-center text-token-text-secondary ...">pgsql</div>
        var labelDiv = preNode.SelectSingleNode(".//div[contains(@class,'text-token-text-secondary')]");
        if (labelDiv != null)
        {
            // The label div may contain child elements (like the copy button sibling),
            // so get only direct text that isn't "Copy code" or empty
            var text = labelDiv.GetDirectInnerText().Trim();
            if (text.Length > 0 && text != "Copy code")
                return text;
        }
        return "";
    }

    private static void ConvertTable(HtmlNode tableNode, StringBuilder md)
    {
        var headerRow = tableNode.SelectSingleNode(".//thead/tr");
        var bodyRows = tableNode.SelectNodes(".//tbody/tr");

        if (headerRow != null)
        {
            var headers = headerRow.SelectNodes("th");
            if (headers != null)
            {
                md.AppendLine("| " + string.Join(" | ", headers.Select(h => h.InnerText.Trim())) + " |");
                md.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
            }
        }

        if (bodyRows != null)
        {
            foreach (var row in bodyRows)
            {
                var cells = row.SelectNodes("td");
                if (cells != null)
                    md.AppendLine("| " + string.Join(" | ", cells.Select(c => c.InnerText.Trim())) + " |");
            }
        }

        md.AppendLine();
    }
}
