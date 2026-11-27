using System.Text.RegularExpressions;
using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Tests.Docs;

public partial class ReceiptStatusDiagramTests
{
    private static readonly string Diagram = MermaidBlock().Match(
        File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, "receipt-statuses.md")).ReplaceLineEndings("\n")).Groups["diagram"].Value;

    [Fact]
    public void Diagram_ShowsExactlyTheTransitionsOfTheStateMachine()
    {
        var arrows = Arrow().Matches(Diagram).Select(m => (
            Enum.Parse<ReceiptStatus>(m.Groups["from"].Value),
            Enum.Parse<ReceiptAction>(m.Groups["action"].Value),
            Enum.Parse<ReceiptStatus>(m.Groups["to"].Value)));

        Assert.Equivalent(ReceiptStateMachine.Transitions.Select(t => (t.From, t.Action, t.To)), arrows, strict: true);
    }

    [Fact]
    public void Diagram_ShowsTheReasonRequirementOfEachTransition()
    {
        Assert.All(ReceiptStateMachine.Transitions, t => Assert.Equal(
            t.RequiresReason,
            Diagram.Contains($"{t.From} --> {t.To}: {t.Action} (reason required)\n", StringComparison.Ordinal)));
    }

    [Fact]
    public void Diagram_StartsAtDraftAndEndsAtCancelled()
    {
        Assert.Contains("[*] --> Draft\n", Diagram);
        Assert.Contains("Cancelled --> [*]\n", Diagram);
    }

    [GeneratedRegex("```mermaid\n(?<diagram>.*?)```", RegexOptions.Singleline)]
    private static partial Regex MermaidBlock();

    [GeneratedRegex(@"^\s*(?<from>\w+) --> (?<to>\w+): (?<action>\w+)", RegexOptions.Multiline)]
    private static partial Regex Arrow();
}
